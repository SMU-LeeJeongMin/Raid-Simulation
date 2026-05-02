// 플레이어 HP/MP 관리
// Health.cs, Mana.cs와 연결

using UnityEngine;

public class PlayerStatus : MonoBehaviour
{
    [Header("References")]
    public Health health;
    public Mana mana;
    public PlayerClassInfo classInfo;

    [Header("Health Setup")]
    public bool createHealthIfMissing = true;
    public bool applyHealthOnAwake = true;
    [Min(1f)] public float maxHealth = 100f;
    [Min(0f)] public float startingHealth = 100f;
    public bool startWithFullHealth = true;
    public bool ignoreDamageAfterDeath = true;
    public bool destroyOnDeath = false;
    public bool logDamage = true;

    [Header("Mana Setup")]
    public bool createManaIfMissing = true;
    public bool applyManaOnAwake = true;
    [Min(1f)] public float maxMana = 100f;
    [Min(0f)] public float startingMana = 100f;
    public bool startWithFullMana = true;
    public bool regenerateMana = true;
    [Min(0f)] public float manaRegenPerSecond = 8f;
    [Min(0f)] public float manaRegenDelayAfterUse = 0.5f;
    public bool logManaChanges = true;

    public Health Health => health;
    public Mana Mana => mana;
    public PlayerClassInfo ClassInfo => classInfo;

    private void Awake()
    {
        ResolveReferences();
        ApplyStatusSettings();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    public void ResolveReferences()
    {
        if (health == null)
            health = GetComponent<Health>();

        if (health == null && createHealthIfMissing)
            health = gameObject.AddComponent<Health>();

        if (mana == null)
            mana = GetComponent<Mana>();

        if (mana == null && createManaIfMissing)
            mana = gameObject.AddComponent<Mana>();

        if (classInfo == null)
            classInfo = GetComponent<PlayerClassInfo>();
    }

    public void ApplyStatusSettings()
    {
        ResolveReferences();

        if (health != null && applyHealthOnAwake)
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            health.maxHealth = maxHealth;
            health.startWithFullHealth = startWithFullHealth;
            health.ignoreDamageAfterDeath = ignoreDamageAfterDeath;
            health.destroyOnDeath = destroyOnDeath;
            health.logDamage = logDamage;

            if (startWithFullHealth)
                health.ResetHealth();
            else
                health.SetHealth(Mathf.Clamp(startingHealth, 0f, maxHealth));
        }

        if (mana != null && applyManaOnAwake)
        {
            maxMana = Mathf.Max(1f, maxMana);
            mana.maxMana = maxMana;
            mana.startWithFullMana = startWithFullMana;
            mana.regenerate = regenerateMana;
            mana.regenPerSecond = manaRegenPerSecond;
            mana.regenDelayAfterUse = manaRegenDelayAfterUse;
            mana.logChanges = logManaChanges;

            if (startWithFullMana)
                mana.Refill();
            else
                mana.SetMana(Mathf.Clamp(startingMana, 0f, maxMana));
        }
    }

    public void TakeDamage(float amount, GameObject source = null)
    {
        ResolveReferences();
        if (health != null)
            health.TakeDamage(amount, source);
    }

    public void Heal(float amount, GameObject source = null)
    {
        ResolveReferences();
        if (health != null)
            health.Heal(amount, source);
    }

    public bool ConsumeMana(float amount)
    {
        ResolveReferences();
        return mana != null && mana.Consume(amount);
    }

    public void RestoreMana(float amount)
    {
        ResolveReferences();
        if (mana != null)
            mana.Restore(amount);
    }
}
