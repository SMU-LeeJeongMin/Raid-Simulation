using UnityEngine;

/// <summary>
/// Collider가 붙은 자식 오브젝트가 실제 Health 컴포넌트로 피해를 전달할 수 있게 해주는 컴포넌트입니다.
/// Dragon 모델처럼 루트와 피격 Collider가 분리된 경우에 유용합니다.
/// </summary>
public class DamageReceiver : MonoBehaviour
{
    [Header("Target")]
    public Health targetHealth;
    public bool findHealthInParent = true;

    private void Awake()
    {
        AutoFindHealthIfNeeded();
    }

    private void Reset()
    {
        AutoFindHealthIfNeeded();
    }

    /// <summary>
    /// 외부 공격 스크립트가 이 함수를 호출하면 실제 Health에 피해가 전달됩니다.
    /// </summary>
    public void ReceiveDamage(float amount, GameObject source = null)
    {
        if (targetHealth == null)
            AutoFindHealthIfNeeded();

        if (targetHealth == null)
        {
            Debug.LogWarning($"[DamageReceiver] Target Health is missing on {name}.", this);
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
