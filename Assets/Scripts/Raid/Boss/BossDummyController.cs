using UnityEngine;

/// <summary>
/// 테스트용 보스 더미 컨트롤러입니다.
/// 지금 단계에서는 공격 패턴 없이 Health 이벤트에 반응하고,
/// 피격/사망 애니메이션 또는 Collider 비활성화만 처리합니다.
/// </summary>
[RequireComponent(typeof(Health))]
public class BossDummyController : MonoBehaviour
{
    [Header("Boss Info")]
    public string bossName = "Dragon Boss";

    [Header("References")]
    public Health health;
    public Animator animator;

    [Header("Animator")]
    public bool disableRootMotion = true;
    public string idleStateName = "";
    public string hitTriggerName = "";
    public string deathTriggerName = "";

    [Header("Physics")]
    public bool makeRigidbodiesKinematic = true;
    public bool disableRigidbodyGravity = true;

    [Header("Death")]
    public bool disableCollidersOnDeath = true;
    public bool keepTriggerCollidersOnDeath = false;

    [Header("Debug")]
    public bool logState = true;

    private Collider[] cachedColliders;
    private Rigidbody[] cachedRigidbodies;

    private void Awake()
    {
        if (health == null)
            health = GetComponent<Health>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        cachedColliders = GetComponentsInChildren<Collider>(true);
        cachedRigidbodies = GetComponentsInChildren<Rigidbody>(true);

        ConfigureAnimator();
        ConfigureRigidbodies();
        EnsureDamageReceivers();
    }

    private void OnEnable()
    {
        if (health == null)
            health = GetComponent<Health>();

        if (health != null)
        {
            health.onDamaged.AddListener(OnDamaged);
            health.onDeath.AddListener(OnDeath);
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.onDamaged.RemoveListener(OnDamaged);
            health.onDeath.RemoveListener(OnDeath);
        }
    }

    private void Start()
    {
        PlayIdleIfConfigured();
    }

    /// <summary>
    /// 보스가 피해를 받았을 때 호출됩니다. 지금은 로그와 피격 Trigger 정도만 처리합니다.
    /// </summary>
    private void OnDamaged(Health damagedHealth, float amount, GameObject source)
    {
        if (logState)
            Debug.Log($"[BossDummy] {bossName} damaged: {amount:0.##}. HP {damagedHealth.CurrentHealth:0.##}/{damagedHealth.MaxHealth:0.##}", this);

        TrySetTrigger(hitTriggerName);
    }

    /// <summary>
    /// 보스 HP가 0이 되었을 때 호출됩니다. 사망 애니메이션과 Collider 정리를 처리합니다.
    /// </summary>
    private void OnDeath(Health deadHealth)
    {
        if (logState)
            Debug.Log($"[BossDummy] {bossName} defeated.", this);

        TrySetTrigger(deathTriggerName);

        if (disableCollidersOnDeath)
            SetCollidersEnabled(false);
    }

    private void ConfigureAnimator()
    {
        if (animator == null)
            return;

        if (disableRootMotion)
            animator.applyRootMotion = false;
    }

    private void ConfigureRigidbodies()
    {
        if (cachedRigidbodies == null)
            return;

        foreach (Rigidbody body in cachedRigidbodies)
        {
            if (body == null)
                continue;

            if (makeRigidbodiesKinematic)
                body.isKinematic = true;

            if (disableRigidbodyGravity)
                body.useGravity = false;
        }
    }

    /// <summary>
    /// Collider가 있는 자식 오브젝트에 DamageReceiver가 없으면 자동으로 붙입니다.
    /// 나중에 공격 판정이 Collider를 맞췄을 때 Health로 피해를 넘기기 위함입니다.
    /// </summary>
    private void EnsureDamageReceivers()
    {
        if (cachedColliders == null)
            return;

        foreach (Collider col in cachedColliders)
        {
            if (col == null)
                continue;

            DamageReceiver receiver = col.GetComponent<DamageReceiver>();
            if (receiver == null)
                receiver = col.gameObject.AddComponent<DamageReceiver>();

            receiver.targetHealth = health;
        }
    }

    private void PlayIdleIfConfigured()
    {
        if (animator == null || string.IsNullOrEmpty(idleStateName))
            return;

        animator.Play(idleStateName, 0, 0f);
    }

    private void TrySetTrigger(string triggerName)
    {
        if (animator == null || string.IsNullOrEmpty(triggerName))
            return;

        // Animator에 해당 Trigger가 없으면 Unity가 경고를 출력할 수 있으므로,
        // 실제 파라미터 이름을 모를 때는 Inspector에서 빈 문자열로 두면 됩니다.
        animator.SetTrigger(triggerName);
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (cachedColliders == null)
            return;

        foreach (Collider col in cachedColliders)
        {
            if (col == null)
                continue;

            if (keepTriggerCollidersOnDeath && col.isTrigger)
                continue;

            col.enabled = enabled;
        }
    }
}
