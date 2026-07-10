// 플레이어 궁극기 게이지

using UnityEngine;

public class PlayerUltimateGauge : MonoBehaviour
{
    [Header("Ultimate Gauge")]
    [Min(1f)] public float maxGauge = 100f;
    [SerializeField] private float currentGauge = 0f;
    public bool startFullForTesting = false;

    [Header("Tolerance")]
    [Tooltip("UI가 100%처럼 보이는데 실제 값이 99.999처럼 아주 조금 부족해서 궁극기가 막히는 것을 방지하는 허용 오차입니다.")]
    [Min(0f)] public float consumeTolerance = 0.01f;

    public float CurrentGauge => currentGauge;
    public float MaxGauge => maxGauge;
    public float Normalized => maxGauge <= 0f ? 0f : Mathf.Clamp01(currentGauge / maxGauge);
    public bool IsFull => currentGauge + consumeTolerance >= maxGauge;

    private void Awake()
    {
        maxGauge = Mathf.Max(1f, maxGauge);
        currentGauge = startFullForTesting ? maxGauge : Mathf.Clamp(currentGauge, 0f, maxGauge);
    }

    public void Gain(float amount)
    {
        amount = Mathf.Max(0f, amount);
        if (amount <= 0f)
            return;

        float before = currentGauge;
        currentGauge = Mathf.Clamp(currentGauge + amount, 0f, maxGauge);
    }

    public bool CanConsume(float amount)
    {
        amount = Mathf.Max(0f, amount);
        return currentGauge + consumeTolerance >= amount;
    }

    public bool Consume(float amount)
    {
        amount = Mathf.Max(0f, amount);
        if (amount <= 0f)
            return true;

        if (currentGauge + consumeTolerance < amount)
        {
            return false;
        }

        currentGauge = Mathf.Clamp(currentGauge - amount, 0f, maxGauge);
        return true;
    }

    public void SetGauge(float value)
    {
        currentGauge = Mathf.Clamp(value, 0f, maxGauge);
    }

    public void Refill()
    {
        currentGauge = maxGauge;
    }

    public void Clear()
    {
        currentGauge = 0f;
    }
}
