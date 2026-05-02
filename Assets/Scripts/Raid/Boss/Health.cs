using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// HP를 관리하는 공용 컴포넌트입니다.
/// 현재 단계에서는 BossDummy에 붙여서 보스 체력 테스트에 사용하고,
/// 나중에는 플레이어/NPC/잡몹에도 재사용할 수 있습니다.
/// </summary>
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

    [Header("Debug")]
    public bool logDamage = false;

    [Header("Events")]
    public HealthChangedEvent onHealthChanged;
    public HealthAmountEvent onDamaged;
    public HealthAmountEvent onHealed;
    public HealthStateEvent onDeath;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float NormalizedHealth => maxHealth <= 0f ? 0f : currentHealth / maxHealth;
    public bool IsDead { get; private set; }

    private bool deathEventSent;

    private void Awake()
    {
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

    /// <summary>
    /// 대상에게 피해를 줍니다.
    /// source는 피해를 준 오브젝트로, 지금은 비워도 되고 나중에 플레이어/스킬 추적에 사용합니다.
    /// </summary>
    public void TakeDamage(float amount, GameObject source = null)
    {
        if (amount <= 0f)
            return;

        if (deathEventSent && ignoreDamageAfterDeath)
            return;

        float previousHealth = currentHealth;
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        float actualDamage = previousHealth - currentHealth;

        if (actualDamage <= 0f)
            return;

        if (logDamage)
            Debug.Log($"[Health] {name} took {actualDamage:0.##} damage. HP: {currentHealth:0.##}/{maxHealth:0.##}", this);

        onDamaged?.Invoke(this, actualDamage, source);
        NotifyHealthChanged();

        if (currentHealth <= 0f)
            Die();
    }

    /// <summary>
    /// 체력을 회복합니다. 지금 단계에서는 필수는 아니지만, 추후 힐러 NPC 구현 때 재사용합니다.
    /// </summary>
    public void Heal(float amount, GameObject source = null)
    {
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

    /// <summary>
    /// 테스트나 에피소드 리셋 때 HP를 최대치로 되돌립니다.
    /// </summary>
    public void ResetHealth()
    {
        IsDead = false;
        deathEventSent = false;
        currentHealth = maxHealth;
        NotifyHealthChanged();
    }

    /// <summary>
    /// 외부에서 HP를 직접 지정할 때 사용합니다.
    /// </summary>
    public void SetHealth(float value)
    {
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
        onHealthChanged?.Invoke(this, currentHealth, maxHealth, NormalizedHealth);
    }
}
