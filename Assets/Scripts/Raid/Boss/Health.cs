// HP 관리 공용 컴포넌트
// Boss + Player + NPC + AddEnemy

using UnityEngine;
using UnityEngine.Events;

public class Health : MonoBehaviour
{
    [System.Serializable]
    public class HealthChangedEvent : UnityEvent<Health, float, float, float> { }

    [System.Serializable]
    public class HealthAmountEvent : UnityEvent<Health, float, GameObject> { }

    [System.Serializable]
    public class HealthStateEvent : UnityEvent<Health> { }

    [Header("Health")]
    [Min(1f)] public float maxHealth = 1000f;
    [SerializeField] private float currentHealth = 1000f;
    public bool startWithFullHealth = true;

    [Header("Death")]
    public bool ignoreDamageAfterDeath = true;
    public bool destroyOnDeath = false;
    public float destroyDelay = 3f;

    [Header("Damage Immunity")]
    public bool damageImmune = false;

    [Header("Events")]
    public HealthChangedEvent onHealthChanged = new HealthChangedEvent();
    public HealthAmountEvent onDamaged = new HealthAmountEvent();
    public HealthAmountEvent onHealed = new HealthAmountEvent();
    public HealthStateEvent onDeath = new HealthStateEvent();

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float NormalizedHealth => maxHealth <= 0f ? 0f : currentHealth / maxHealth;
    public bool IsDead { get; private set; }
    public bool DamageImmune => damageImmune;

    private bool deathEventSent;

    private void Awake()
    {
        EnsureEvents();

        if (startWithFullHealth)
            currentHealth = maxHealth;
        else
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        IsDead = currentHealth <= 0f;
        deathEventSent = false;
        NotifyHealthChanged();

        if (IsDead)
            Die();
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
    }

    public void EnsureEvents()
    {
        onHealthChanged ??= new HealthChangedEvent();
        onDamaged ??= new HealthAmountEvent();
        onHealed ??= new HealthAmountEvent();
        onDeath ??= new HealthStateEvent();
    }

    public void TakeDamage(float amount, GameObject source = null)
    {
        EnsureEvents();

        if (amount <= 0f)
            return;

        if (deathEventSent && ignoreDamageAfterDeath)
            return;

        if (damageImmune)
        {
            return;
        }

        float previousHealth = currentHealth;
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        float actualDamage = previousHealth - currentHealth;

        if (actualDamage <= 0f)
            return;

        onDamaged?.Invoke(this, actualDamage, source);
        NotifyHealthChanged();

        if (currentHealth <= 0f)
            Die();
    }

    public void Heal(float amount, GameObject source = null)
    {
        EnsureEvents();

        if (amount <= 0f || IsDead)
            return;

        float previousHealth = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        float actualHeal = currentHealth - previousHealth;

        if (actualHeal <= 0f)
            return;

        onHealed?.Invoke(this, actualHeal, source);
        NotifyHealthChanged();
    }

    public void ResetHealth()
    {
        EnsureEvents();
        IsDead = false;
        deathEventSent = false;
        currentHealth = maxHealth;
        NotifyHealthChanged();
    }

    public void SetDamageImmune(bool immune)
    {
        damageImmune = immune;
    }

    public void SetHealth(float value)
    {
        EnsureEvents();
        currentHealth = Mathf.Clamp(value, 0f, maxHealth);

        if (currentHealth > 0f)
        {
            IsDead = false;
            deathEventSent = false;
            NotifyHealthChanged();
            return;
        }

        Die();
    }

    private void Die()
    {
        EnsureEvents();

        if (deathEventSent)
            return;

        IsDead = true;
        deathEventSent = true;
        currentHealth = 0f;

        NotifyHealthChanged();
        onDeath?.Invoke(this);

        if (destroyOnDeath)
            Destroy(gameObject, destroyDelay);
    }

    private void NotifyHealthChanged()
    {
        EnsureEvents();
        onHealthChanged?.Invoke(this, currentHealth, maxHealth, NormalizedHealth);
    }
}
