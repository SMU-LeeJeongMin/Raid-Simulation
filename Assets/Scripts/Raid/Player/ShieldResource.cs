// 보호막

using UnityEngine;

public class ShieldResource : MonoBehaviour
{
    [Header("Shield")]
    [SerializeField] private float currentShield;
    [SerializeField] private float shieldExpireTime;

    public float CurrentShield
    {
        get
        {
            RefreshExpiration();
            return currentShield;
        }
    }

    public bool HasShield => CurrentShield > 0f;

    private void Update()
    {
        RefreshExpiration();
    }

    public void AddShield(float amount, float duration)
    {
        amount = Mathf.Max(0f, amount);
        duration = Mathf.Max(0.1f, duration);
        if (amount <= 0f)
            return;

        currentShield += amount;
        shieldExpireTime = Mathf.Max(shieldExpireTime, Time.time + duration);
    }

    public float AbsorbDamage(float damage)
    {
        RefreshExpiration();

        damage = Mathf.Max(0f, damage);
        if (damage <= 0f || currentShield <= 0f)
            return damage;

        float absorbed = Mathf.Min(currentShield, damage);
        currentShield -= absorbed;
        float remaining = damage - absorbed;

        return remaining;
    }

    public void ClearShield()
    {
        currentShield = 0f;
        shieldExpireTime = 0f;
    }

    private void RefreshExpiration()
    {
        if (currentShield <= 0f)
            return;

        if (shieldExpireTime > 0f && Time.time >= shieldExpireTime)
            ClearShield();
    }
}
