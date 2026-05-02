// 플레이어 MP

using UnityEngine;
public class Mana : MonoBehaviour
{
    [Header("Mana")]
    [Min(1f)] public float maxMana = 100f;
    [SerializeField] private float currentMana = 100f;
    public bool startWithFullMana = true;

    [Header("Regeneration")]
    public bool regenerate = true;
    [Min(0f)] public float regenPerSecond = 8f;
    [Min(0f)] public float regenDelayAfterUse = 0.5f;

    [Header("Debug")]
    public bool logChanges = false;

    private float nextRegenAllowedTime;

    public float CurrentMana => currentMana;
    public float MaxMana => maxMana;
    public float Normalized => maxMana <= 0f ? 0f : Mathf.Clamp01(currentMana / maxMana);

    private void Awake()
    {
        maxMana = Mathf.Max(1f, maxMana);

        if (startWithFullMana)
            currentMana = maxMana;
        else
            currentMana = Mathf.Clamp(currentMana, 0f, maxMana);
    }

    private void Update()
    {
        if (!regenerate || regenPerSecond <= 0f)
            return;

        if (Time.time < nextRegenAllowedTime)
            return;

        if (currentMana >= maxMana)
            return;

        Restore(regenPerSecond * Time.deltaTime);
    }

    public bool CanConsume(float amount)
    {
        return currentMana >= Mathf.Max(0f, amount);
    }

    public bool Consume(float amount)
    {
        amount = Mathf.Max(0f, amount);
        if (amount <= 0f)
            return true;

        if (currentMana < amount)
        {
            if (logChanges)
                Debug.Log($"[Mana] {name} cannot consume {amount:0.##}. MP {currentMana:0.##}/{maxMana:0.##}", this);
            return false;
        }

        currentMana = Mathf.Clamp(currentMana - amount, 0f, maxMana);
        nextRegenAllowedTime = Time.time + regenDelayAfterUse;

        if (logChanges)
            Debug.Log($"[Mana] {name} consumed {amount:0.##}. MP {currentMana:0.##}/{maxMana:0.##}", this);

        return true;
    }

    public void Restore(float amount)
    {
        amount = Mathf.Max(0f, amount);
        if (amount <= 0f)
            return;

        float before = currentMana;
        currentMana = Mathf.Clamp(currentMana + amount, 0f, maxMana);

        if (logChanges && !Mathf.Approximately(before, currentMana))
            Debug.Log($"[Mana] {name} restored {amount:0.##}. MP {currentMana:0.##}/{maxMana:0.##}", this);
    }

    public void Refill()
    {
        currentMana = maxMana;
    }

    public void SetMana(float value)
    {
        currentMana = Mathf.Clamp(value, 0f, maxMana);
    }
}
