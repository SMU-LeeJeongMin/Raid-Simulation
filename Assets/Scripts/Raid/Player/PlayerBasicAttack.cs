// 플레이어 기본 공격
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerBasicAttack : MonoBehaviour
{
    [Header("Stats")]
    public BasicAttackStatsDatabase statsDatabase;
    public PlayerClassInfo classInfo;
    public string characterIdOverride = "";

    [Header("References")]
    public Animator animator;
    public Transform attackOrigin;
    public PlayerMovement playerMovement;
    [FormerlySerializedAs("vfxPlayer")]
    public BasicAttackVFXPlayer VFXPlayer;
    public PlayerUltimateGauge ultimateGauge;

    [Header("Ultimate Gain")]
    public bool gainUltimateOnBasicAttackHit = true;
    [Min(0f)] public float fallbackUltimateGainOnHit = 4f;

    [Header("Input")]
    public bool useLeftMouseButton = true;
    public bool useKeyboardNumber1 = true;
    public bool ignoreInputWhenPointerIsOverUI = true;

    [Header("Target")]
    public LayerMask targetMask = ~0;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Boss Phase Rules")]
    [FormerlySerializedAs("blockBasicAttackWhileBossFlying")]
    public bool skipFlyingBossTarget = true;

    [Header("Fallback Origin")]
    public float fallbackAttackHeight = 0.8f;

    [Header("Debug")]
    public bool drawAttackGizmo = true;
    public bool drawHorizontalAutoTargetGizmo = true;

    [Header("Runtime Debug")]
    [SerializeField] private bool externalActionLock;
    [SerializeField] private bool isAttacking;
    [SerializeField] private string resolvedCharacterId;
    [SerializeField] private string currentAttackInfo;
    [SerializeField] private string lastTargetingResult;

    private BasicAttackStats currentStats;
    private float nextAttackAllowedTime;

    // 공격 클립 직접 재생을 담당하는 공용 플레이어
    private SingleClipPlayer attackClipPlayer;
    private SingleClipPlayer AttackClipPlayer => attackClipPlayer ??= new SingleClipPlayer(name + "_BasicAttackGraph", "BasicAttack");

    private float originalAnimatorSpeed = 1f;
    private bool originalRootMotion;
    private bool hasOriginalAnimatorValues;

    private Vector3 lastAttackStart;
    private Vector3 lastAttackEnd;
    private float lastAttackRadius;
    private float lastHorizontalAutoRange;

    public bool IsAttacking => isAttacking;
    public BasicAttackStats CurrentStats => currentStats;

    private void Awake()
    {
        if (classInfo == null)
            classInfo = GetComponent<PlayerClassInfo>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (VFXPlayer == null)
            VFXPlayer = GetComponent<BasicAttackVFXPlayer>();

        if (ultimateGauge == null)
            ultimateGauge = GetComponent<PlayerUltimateGauge>();

        RefreshStats();
    }

    private void Update()
    {
        if (WasAttackPressed())
            TryBasicAttack();
    }

    public void RefreshStats()
    {
        resolvedCharacterId = ResolveCharacterId();
        currentStats = statsDatabase != null ? statsDatabase.GetStats(resolvedCharacterId) : null;

        if (currentStats == null)
        {
            currentStats = new BasicAttackStats
            {
                characterId = string.IsNullOrEmpty(resolvedCharacterId) ? "default" : resolvedCharacterId,
                displayName = string.IsNullOrEmpty(resolvedCharacterId) ? "Default" : resolvedCharacterId,
                damage = 50f,
                attackDuration = 0.8f,
                extraRecoveryTime = 0f,
                hitNormalizedTime = 0.45f,
                range = 2f,
                hitRadius = 0.5f,
                useHorizontalAutoTarget = true,
                useColliderBoundsForAutoTarget = true,
                autoTargetExtraRange = 0.35f,
                animatorStateName = string.Equals(resolvedCharacterId, "archer", System.StringComparison.OrdinalIgnoreCase) ? "Shoot" : "ATK0",
                returnStateName = "IdleA",
                useDirectClipPlayback = false,
                lockMovementDuringAttack = true,
                suppressMovementAnimationDuringAttack = true
            };
        }
    }

    private string ResolveCharacterId()
    {
        if (!string.IsNullOrWhiteSpace(characterIdOverride))
            return characterIdOverride.Trim();

        if (classInfo != null && !string.IsNullOrWhiteSpace(classInfo.characterId))
            return classInfo.characterId.Trim();

        return SelectedCharacterMemory.LoadSelectedId("warrior");
    }

    private bool WasAttackPressed()
    {
        if (ignoreInputWhenPointerIsOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return false;

#if ENABLE_INPUT_SYSTEM
        bool mousePressed = useLeftMouseButton && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool keyPressed = useKeyboardNumber1 && Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame;
        return mousePressed || keyPressed;
#else
        bool mousePressed = useLeftMouseButton && Input.GetMouseButtonDown(0);
        bool keyPressed = useKeyboardNumber1 && Input.GetKeyDown(KeyCode.Alpha1);
        return mousePressed || keyPressed;
#endif
    }
    public bool TryBasicAttack()
    {
        if (externalActionLock)
            return false;

        if (isAttacking)
            return false;

        if (Time.time < nextAttackAllowedTime)
            return false;

        RefreshStats();
        StartCoroutine(BasicAttackRoutine(currentStats));
        return true;
    }

    public void SetExternalActionLock(bool locked)
    {
        externalActionLock = locked;
    }

    public DamageReceiver FindTargetForSkill()
    {
        RefreshStats();
        return FindBestTarget(currentStats);
    }

    public Vector3 GetTargetAimPositionForSkill(DamageReceiver target)
    {
        return GetTargetAimPosition(target);
    }

    private IEnumerator BasicAttackRoutine(BasicAttackStats stats)
    {
        if (stats == null)
            yield break;

        isAttacking = true;

        try
        {
            DamageReceiver targetAtStart = FindBestTarget(stats);
            if (targetAtStart != null && stats.faceTargetOnAttack)
                FaceTarget(GetTargetAimPosition(targetAtStart));

            float attackDuration = Mathf.Max(0.05f, stats.attackDuration);
            float hitTime = Mathf.Clamp01(stats.hitNormalizedTime) * attackDuration;
            float totalLockTime = attackDuration + Mathf.Max(0f, stats.extraRecoveryTime);
            nextAttackAllowedTime = Time.time + totalLockTime;

            PrepareAnimatorForAttack(stats);
            SetMovementStateForAttack(stats, true);
            PlayAttackAnimation(stats, attackDuration);

            string animationName = stats.attackClip != null ? stats.attackClip.name : stats.animatorStateName;
            currentAttackInfo = $"{stats.displayName} {animationName} damage={stats.damage:0.##}, duration={attackDuration:0.###}s";

            if (hitTime > 0f)
                yield return new WaitForSeconds(hitTime);

            ApplyBasicAttackDamage(stats, targetAtStart);

            float remaining = Mathf.Max(0f, totalLockTime - hitTime);
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);
        }
        finally
        {
            StopDirectAttackClipIfNeeded();
            RestoreAnimatorAfterAttack(stats);
            SetMovementStateForAttack(stats, false);
            isAttacking = false;
        }
    }

    private void PrepareAnimatorForAttack(BasicAttackStats stats)
    {
        if (animator == null)
            return;

        originalAnimatorSpeed = animator.speed;
        originalRootMotion = animator.applyRootMotion;
        hasOriginalAnimatorValues = true;

        if (stats.disableRootMotionDuringAttack)
            animator.applyRootMotion = false;
    }

    private void RestoreAnimatorAfterAttack(BasicAttackStats stats)
    {
        if (animator == null)
            return;

        animator.speed = originalAnimatorSpeed;

        if (hasOriginalAnimatorValues)
            animator.applyRootMotion = originalRootMotion;

        if (!string.IsNullOrWhiteSpace(stats.returnStateName) && playerMovement == null)
        {
            string returnState = ResolveAnimatorStateName(stats.returnStateName, false);
            if (!string.IsNullOrWhiteSpace(returnState))
                animator.CrossFadeInFixedTime(returnState, 0.08f, 0, 0f);
        }
    }

    private void PlayAttackAnimation(BasicAttackStats stats, float attackDuration)
    {
        if (animator == null)
            return;

        animator.enabled = true;

        if (stats.useDirectClipPlayback && stats.attackClip != null)
        {
            PlayDirectClip(stats.attackClip, attackDuration);
            return;
        }

        StopDirectAttackClipIfNeeded();

        string stateName = ResolveAnimatorStateName(stats.animatorStateName, true);
        if (string.IsNullOrWhiteSpace(stateName))
            return;

        animator.speed = 1f;
        animator.CrossFadeInFixedTime(stateName, 0.05f, 0, 0f);
        animator.Update(0f);
    }

    private void PlayDirectClip(AnimationClip clip, float attackDuration)
    {
        AttackClipPlayer.PlayTimed(animator, clip, attackDuration);
    }

    private void StopDirectAttackClipIfNeeded()
    {
        attackClipPlayer?.Stop();
    }

    private void SetMovementStateForAttack(BasicAttackStats stats, bool attacking)
    {
        if (playerMovement == null)
            return;

        if (stats.lockMovementDuringAttack)
            playerMovement.SetExternalMovementLock(attacking);

        if (stats.suppressMovementAnimationDuringAttack)
            playerMovement.SetAnimationSuppressed(attacking);
    }

    private void ApplyBasicAttackDamage(BasicAttackStats stats, DamageReceiver targetAtStart)
    {
        if (stats == null)
            return;

        DamageReceiver target = FindBestTarget(stats);
        if (!IsReceiverValidAtHit(target))
            target = targetAtStart;

        if (!IsReceiverValidAtHit(target))
        {
            // 빗나감은 지표로만 집계 (AI 전투에서 일상적으로 발생하므로 콘솔 로그 미출력)
            RaidMetricsEvents.ReportBasicAttackMiss(this, stats, "no_valid_target_at_hit_timing");
            return;
        }

        target.ReceiveDamage(stats.damage, gameObject);
        RaidMetricsEvents.ReportBasicAttackHit(this, stats, target);

        if (gainUltimateOnBasicAttackHit && ultimateGauge != null)
        {
            float gain = stats.ultimateGainOnHit > 0f ? stats.ultimateGainOnHit : fallbackUltimateGainOnHit;
            ultimateGauge.Gain(gain);
        }

        if (VFXPlayer != null)
            VFXPlayer.PlayAttackHit(stats, target);
    }

    private bool IsReceiverValidAtHit(DamageReceiver receiver)
    {
        if (receiver == null)
            return false;

        if (receiver.transform.root == transform.root)
            return false;

        if (receiver.targetHealth == null)
            receiver.targetHealth = receiver.GetComponentInParent<Health>();

        Health targetHealth = receiver.targetHealth;
        if (targetHealth == null || targetHealth.IsDead)
            return false;

        if (targetHealth.transform.root == transform.root)
            return false;

        if (targetHealth.DamageImmune)
            return false;

        if (skipFlyingBossTarget && IsFlyingBossHealth(targetHealth))
            return false;

        if (!IsAllowedByTargetMask(receiver, targetHealth))
            return false;

        return true;
    }

    private DamageReceiver FindBestTarget(BasicAttackStats stats)
    {
        Vector3 origin = GetAttackOrigin();
        Vector3 forward = GetAttackForward();
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = transform.forward;
        forward.Normalize();

        lastAttackStart = origin;
        lastAttackEnd = origin + forward * stats.range;
        lastAttackRadius = stats.hitRadius;
        lastHorizontalAutoRange = stats.range + stats.hitRadius + stats.autoTargetExtraRange;
        lastTargetingResult = "None";

        DamageReceiver best = null;
        float bestScore = float.MaxValue;
        HashSet<Health> visitedHealth = new HashSet<Health>();

        // 1. 기존 물리 판정: 실제 공격선이 Collider에 닿는 경우.
        RaycastHit[] sphereHits = Physics.SphereCastAll(origin, stats.hitRadius, forward, stats.range, targetMask, triggerInteraction);
        for (int i = 0; i < sphereHits.Length; i++)
            TrySetBestTargetFromCollider(sphereHits[i].collider, origin, forward, stats, ref best, ref bestScore, visitedHealth, "SphereCast");

        Collider[] overlapHits = Physics.OverlapSphere(origin, stats.hitRadius, targetMask, triggerInteraction);
        for (int i = 0; i < overlapHits.Length; i++)
            TrySetBestTargetFromCollider(overlapHits[i], origin, forward, stats, ref best, ref bestScore, visitedHealth, "OverlapSphere");

        // 2. RPG식 자동 타겟팅: Collider에 직접 닿지 않아도 XZ 평면상 사거리 안이면 맞음
        if (stats.useHorizontalAutoTarget)
            ScanSceneTargetsHorizontally(origin, forward, stats, ref best, ref bestScore, visitedHealth);

        return best;
    }

    private void TrySetBestTargetFromCollider(
        Collider hitCollider,
        Vector3 origin,
        Vector3 forward,
        BasicAttackStats stats,
        ref DamageReceiver best,
        ref float bestScore,
        HashSet<Health> visitedHealth,
        string source)
    {
        if (hitCollider == null)
            return;

        if (hitCollider.transform.root == transform.root)
            return;

        DamageReceiver receiver = ResolveDamageReceiver(hitCollider);
        TrySetBestTargetFromReceiver(receiver, origin, forward, stats, ref best, ref bestScore, visitedHealth, source);
    }

    private void ScanSceneTargetsHorizontally(
        Vector3 origin,
        Vector3 forward,
        BasicAttackStats stats,
        ref DamageReceiver best,
        ref float bestScore,
        HashSet<Health> visitedHealth)
    {
        // 씬 전체 탐색 대신 레지스트리 순회 (공격 및 UI 폴링마다 호출되는 핫패스)
        IReadOnlyList<DamageReceiver> receivers = CombatRegistry.DamageReceivers;
        for (int i = 0; i < receivers.Count; i++)
            TrySetBestTargetFromReceiver(receivers[i], origin, forward, stats, ref best, ref bestScore, visitedHealth, "HorizontalAutoTarget-DamageReceiver");

        IReadOnlyList<Health> healths = CombatRegistry.Healths;
        for (int i = 0; i < healths.Count; i++)
        {
            Health health = healths[i];
            if (health == null)
                continue;

            DamageReceiver receiver = health.GetComponent<DamageReceiver>();
            if (receiver == null)
                receiver = health.gameObject.AddComponent<DamageReceiver>();

            receiver.targetHealth = health;
            TrySetBestTargetFromReceiver(receiver, origin, forward, stats, ref best, ref bestScore, visitedHealth, "HorizontalAutoTarget-Health");
        }
    }

    private DamageReceiver ResolveDamageReceiver(Collider hitCollider)
    {
        if (hitCollider == null)
            return null;

        DamageReceiver receiver = hitCollider.GetComponentInParent<DamageReceiver>();
        if (receiver != null)
            return receiver;

        Health health = hitCollider.GetComponentInParent<Health>();
        if (health == null)
            return null;

        receiver = health.GetComponent<DamageReceiver>();
        if (receiver == null)
            receiver = health.gameObject.AddComponent<DamageReceiver>();

        receiver.targetHealth = health;
        return receiver;
    }

    private void TrySetBestTargetFromReceiver(
        DamageReceiver receiver,
        Vector3 origin,
        Vector3 forward,
        BasicAttackStats stats,
        ref DamageReceiver best,
        ref float bestScore,
        HashSet<Health> visitedHealth,
        string source)
    {
        if (receiver == null)
            return;

        if (receiver.transform.root == transform.root)
            return;

        if (receiver.targetHealth == null)
            receiver.targetHealth = receiver.GetComponentInParent<Health>();

        Health targetHealth = receiver.targetHealth;
        if (targetHealth == null || targetHealth.IsDead)
            return;

        if (targetHealth.DamageImmune)
            return;

        if (skipFlyingBossTarget && IsFlyingBossHealth(targetHealth))
            return;

        if (targetHealth.transform.root == transform.root)
            return;

        if (!IsAllowedByTargetMask(receiver, targetHealth))
            return;

        if (visitedHealth != null && visitedHealth.Contains(targetHealth))
            return;

        Vector3 targetPoint = GetBestPlanarTargetPoint(receiver, origin, stats);
        Vector3 flatToTarget = targetPoint - origin;
        flatToTarget.y = 0f;

        float distance = flatToTarget.magnitude;
        float allowedRange = stats.range + stats.hitRadius + stats.autoTargetExtraRange;
        if (distance > allowedRange)
            return;

        if (stats.requireTargetInFront && flatToTarget.sqrMagnitude > 0.0001f)
        {
            Vector3 flatForward = forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude > 0.0001f)
            {
                float angle = Vector3.Angle(flatForward.normalized, flatToTarget.normalized);
                if (angle > stats.maxTargetAngle * 0.5f)
                    return;
            }
        }

        if (visitedHealth != null)
            visitedHealth.Add(targetHealth);

        if (distance < bestScore)
        {
            bestScore = distance;
            best = receiver;
            lastTargetingResult = source + $" distance={distance:0.##}/{allowedRange:0.##}";
        }
    }

    private Vector3 GetBestPlanarTargetPoint(DamageReceiver receiver, Vector3 origin, BasicAttackStats stats)
    {
        Health health = receiver != null ? receiver.targetHealth : null;
        Transform root = health != null ? health.transform : receiver != null ? receiver.transform : null;

        Vector3 fallback = root != null ? root.position : Vector3.zero;
        fallback.y = origin.y;

        if (!stats.useColliderBoundsForAutoTarget || root == null)
            return fallback;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        if (colliders == null || colliders.Length == 0)
            return fallback;

        Vector3 bestPoint = fallback;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || !col.enabled)
                continue;

            if (col.transform.root == transform.root)
                continue;

            if (!IsLayerInMask(col.gameObject.layer, targetMask))
                continue;

            Bounds bounds = col.bounds;
            Vector3 point = origin;
            point.x = Mathf.Clamp(origin.x, bounds.min.x, bounds.max.x);
            point.y = origin.y;
            point.z = Mathf.Clamp(origin.z, bounds.min.z, bounds.max.z);

            Vector3 flatDelta = point - origin;
            flatDelta.y = 0f;
            float sqr = flatDelta.sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                bestPoint = point;
            }
        }

        return bestPoint;
    }

    private bool IsAllowedByTargetMask(DamageReceiver receiver, Health health)
    {
        if (targetMask.value == ~0)
            return true;

        if (receiver != null && IsLayerInMask(receiver.gameObject.layer, targetMask))
            return true;

        if (health != null)
        {
            if (IsLayerInMask(health.gameObject.layer, targetMask))
                return true;

            Collider[] colliders = health.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (col != null && IsLayerInMask(col.gameObject.layer, targetMask))
                    return true;
            }
        }

        return false;
    }

    private bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private Vector3 GetTargetAimPosition(DamageReceiver target)
    {
        if (target == null)
            return transform.position + transform.forward;

        Health health = target.targetHealth;
        if (health != null)
            return health.transform.position;

        return target.transform.position;
    }

    private string GetTargetLogName(DamageReceiver target)
    {
        if (target == null)
            return "Unknown";

        if (target.targetHealth != null)
            return target.targetHealth.name;

        return target.name;
    }

    private Vector3 GetAttackOrigin()
    {
        if (attackOrigin != null)
            return attackOrigin.position;

        return transform.position + Vector3.up * fallbackAttackHeight;
    }

    private Vector3 GetAttackForward()
    {
        if (attackOrigin != null)
        {
            Vector3 attackForward = attackOrigin.forward;
            attackForward.y = 0f;
            return attackForward.sqrMagnitude < 0.0001f ? transform.forward : attackForward.normalized;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude < 0.0001f ? transform.forward : forward.normalized;
    }

    private void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private string ResolveAnimatorStateName(string stateName, bool warnIfMissing)
    {
        return AnimatorStateUtility.ResolveStateName(animator, stateName);
    }

    private bool IsFlyingBossHealth(Health targetHealth)
    {
        if (targetHealth == null)
            return false;

        BossSkillPatternController boss = targetHealth.GetComponentInParent<BossSkillPatternController>();
        return boss != null && boss.IsFlying;
    }

    private void OnDisable()
    {
        StopDirectAttackClipIfNeeded();

        if (playerMovement != null)
        {
            playerMovement.SetExternalMovementLock(false);
            playerMovement.SetAnimationSuppressed(false);
        }

        externalActionLock = false;
        isAttacking = false;
    }

    private void OnDestroy()
    {
        attackClipPlayer?.Dispose();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawAttackGizmo)
            return;

        BasicAttackStats stats = currentStats;
        if (stats == null)
            return;

        Vector3 origin = Application.isPlaying ? lastAttackStart : GetAttackOrigin();
        Vector3 forward = GetAttackForward();
        Vector3 end = Application.isPlaying ? lastAttackEnd : origin + forward * stats.range;
        float radius = Application.isPlaying ? lastAttackRadius : stats.hitRadius;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin, radius);
        Gizmos.DrawLine(origin, end);
        Gizmos.DrawWireSphere(end, radius);

        if (drawHorizontalAutoTargetGizmo && stats.useHorizontalAutoTarget)
        {
            float autoRange = Application.isPlaying ? lastHorizontalAutoRange : stats.range + stats.hitRadius + stats.autoTargetExtraRange;
            Gizmos.color = new Color(1f, 0.15f, 0.05f, 0.9f);
            DrawXZCircle(origin, autoRange, 48);
        }
    }

    private void DrawXZCircle(Vector3 center, float radius, int segments)
    {
        if (radius <= 0f || segments < 3)
            return;

        Vector3 previous = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (Mathf.PI * 2f) * i / segments;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
