// Collider가 붙은 자식 오브젝트 -> 실제 Health 컴포넌트로 피해 전달

using UnityEngine;

public class DamageReceiver : MonoBehaviour
{
    [Header("Target")]
    public Health targetHealth;
    public bool findHealthInParent = true;

    private void Awake()
    {
        CombatRegistry.Register(this);
        AutoFindHealthIfNeeded();
    }

    private void OnDestroy()
    {
        CombatRegistry.Unregister(this);
    }

    private void Reset()
    {
        AutoFindHealthIfNeeded();
    }

    // Health에 피해 전달
    public void ReceiveDamage(float amount, GameObject source = null)
    {
        if (targetHealth == null)
            AutoFindHealthIfNeeded();

        if (targetHealth == null)
        {
            return;
        }

        targetHealth.TakeDamage(amount, source);
    }

    private void AutoFindHealthIfNeeded()
    {
        if (targetHealth != null)
            return;

        targetHealth = findHealthInParent
            ? GetComponentInParent<Health>()
            : GetComponent<Health>();
    }
}
