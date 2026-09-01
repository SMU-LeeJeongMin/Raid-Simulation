// 보스 패턴 - 슬라임 소환
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Health))]
public class SlimeAddEnemy : MonoBehaviour
{
    [Header("References")]
    public Health health;
    public DamageReceiver damageReceiver;
    public WorldHealthBarUI healthBarUI;
    public Animator animator;
    public NavMeshAgent agent;

    [Header("Lifecycle")]
    public float explodeAfterSeconds = 15f;
    public bool explodeWhenTimerEnds = true;
    public bool destroyWhenKilled = true;
    public float destroyDelayAfterKilled = 0.4f;

    [Header("Explosion / DOT Zone")]
    public float explosionDamage = 25f;
    public float explosionRadius = 2.5f;
    public GameObject explosionVFXPrefab;
    public GameObject persistentZoneVFXPrefab;
    public float persistentZoneRadius = 2.2f;
    public float persistentZoneTickDamage = 8f;
    public float persistentZoneTickInterval = 0.5f;
    public float persistentZoneDuration = -1f;

    [Header("Chase Movement")]
    public bool chaseRandomPartyMember = true;
    public bool useNavMeshAgent = true;
    public bool addNavMeshAgentIfMissing = true;
    public float moveSpeed = 1.5f;
    public float retargetInterval = 2.5f;
    public bool lockInitialTarget = true;
    public bool chooseNewTargetIfInitialTargetLost = false;
    public float navMeshSampleRadius = 4f;
    public int navMeshAreaMask = NavMesh.AllAreas;
    public float attackDistance = 1.2f;
    public float meleeDamage = 5f;
    public float meleeInterval = 1.5f;

    [Header("Animation")]
    public AnimationClip walkClip;
    public string walkStateName = "Walk";
    public AnimationClip idleClip;
    public string idleStateName = "Idle";
    public bool useDirectClipPlayback = false;
    public float walkClipSpeed = 1f;
    public float idleClipSpeed = 1f;

    [Header("Death / Knockdown")]
    public AnimationClip knockdownClip;
    public string knockdownStateName = "Knockdown";
    public bool useDirectKnockdownClipPlayback = true;
    [Min(0f)] public float knockdownDurationOverride = 0f;
    [Min(0f)] public float destroyDelayAfterKnockdown = 0.1f;
    public bool disableCollidersOnKnockdown = true;
    public bool hideHealthBarOnKnockdown = true;

    [Header("Movement / Animation Safety")]
    public bool disableRootMotion = true;
    public bool makeRigidbodiesKinematic = true;
    public bool disableCharacterController = true;
    public bool lockAnimatorRootLocalTransform = true;
    public bool forceTransformToAgentPositionAfterAnimation = false;

    [Header("Debug")]
    public bool logState = true;

    private float spawnTime;
    private float nextMeleeTime;
    private float nextRetargetTime;
    private bool resolved;
    private bool deathListenerRegistered;
    private bool spawnMetricReported;
    private bool killedMetricReported;
    private bool explodedMetricReported;
    private PlayerStatus currentTarget;
    // 대상 후보 수집용 재사용 버퍼 (틱마다 새 리스트 할당 방지)
    private readonly System.Collections.Generic.List<PlayerStatus> targetCandidateBuffer = new System.Collections.Generic.List<PlayerStatus>();

    // 슬라임 클립 직접 재생을 담당하는 공용 플레이어
    private SingleClipPlayer clipPlayer;
    private SingleClipPlayer ClipPlayer => clipPlayer ??= new SingleClipPlayer(name + "_SlimeAnimationGraph", "SlimeAnimation");
    private bool playingWalk;
    private Coroutine knockdownRoutine;
    private Transform animatorTransform;
    private Vector3 animatorInitialLocalPosition;
    private Quaternion animatorInitialLocalRotation;
    private Vector3 animatorInitialLocalScale;
    private bool hasAnimatorInitialLocalTransform;

    public bool IsResolved => resolved || this == null;

    // ---------- 정책 판단 및 그래프 스냅샷용 상태 노출 ----------

    // 생존 여부 (넉다운 처리 중이거나 사망하면 false)
    public bool IsAlive => !resolved && health != null && !health.IsDead;

    // 폭발까지 남은 시간 (초). 폭발 타이머 미사용 시 무한대
    public float ExplosionTimeRemaining =>
        explodeWhenTimerEnds ? Mathf.Max(0f, spawnTime + explodeAfterSeconds - Time.time) : float.PositiveInfinity;

    // 폭발까지 남은 시간 비율 (1 = 방금 소환, 0 = 폭발 직전)
    public float ExplosionTimeRatio =>
        explodeWhenTimerEnds && explodeAfterSeconds > 0f
            ? Mathf.Clamp01((spawnTime + explodeAfterSeconds - Time.time) / explodeAfterSeconds)
            : 1f;

    // 현재 추적 중인 파티원 (없으면 null)
    public PlayerStatus CurrentTarget => currentTarget;

    // 폭발 시 지속 DOT 장판을 남기는지 여부
    public bool CreatesPersistentZone => persistentZoneRadius > 0f && persistentZoneTickDamage > 0f;

    private void Awake()
    {
        CombatRegistry.Register(this);
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        spawnTime = Time.time;
        nextRetargetTime = float.PositiveInfinity;
        resolved = false;
        spawnMetricReported = false;
        killedMetricReported = false;
        explodedMetricReported = false;
        knockdownRoutine = null;
        RegisterDeathListener();
        PrepareAgent();
        ChooseRandomTarget();
    }

    private void OnDisable()
    {
        UnregisterDeathListener();
    }

    private void ResolveReferences()
    {
        RemoveDemoAnimationPlayerComponents();

        if (health == null)
            health = GetComponent<Health>();
        if (health == null)
            health = gameObject.AddComponent<Health>();
        health.EnsureEvents();

        if (damageReceiver == null)
            damageReceiver = GetComponent<DamageReceiver>();
        if (damageReceiver == null)
            damageReceiver = gameObject.AddComponent<DamageReceiver>();
        damageReceiver.targetHealth = health;

        if (healthBarUI == null)
            healthBarUI = GetComponent<WorldHealthBarUI>();
        if (healthBarUI == null)
            healthBarUI = gameObject.AddComponent<WorldHealthBarUI>();
        healthBarUI.targetHealth = health;

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator != null)
        {
            if (disableRootMotion)
                animator.applyRootMotion = false;

            CacheAnimatorLocalTransformIfNeeded();
        }

        if (makeRigidbodiesKinematic)
        {
            Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] == null)
                    continue;
                bodies[i].isKinematic = true;
                bodies[i].useGravity = false;
            }
        }

        if (disableCharacterController)
        {
            CharacterController controller = GetComponent<CharacterController>();
            if (controller != null)
                controller.enabled = false;
        }

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
    }

    private void PrepareAgent()
    {
        if (!useNavMeshAgent)
            return;

        if (agent == null && addNavMeshAgentIfMissing)
            agent = gameObject.AddComponent<NavMeshAgent>();

        if (agent == null)
            return;

        agent.speed = moveSpeed;
        agent.angularSpeed = 720f;
        agent.acceleration = 16f;
        agent.stoppingDistance = attackDistance;
        agent.updateRotation = false;
        agent.updatePosition = true;

        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSampleRadius, navMeshAreaMask))
            {
                agent.enabled = false;
                transform.position = hit.position;
                agent.enabled = true;
            }
            else
            {
                agent.enabled = false;
            }
        }

        if (agent.enabled && agent.isOnNavMesh)
            agent.nextPosition = transform.position;
    }

    private void RegisterDeathListener()
    {
        if (deathListenerRegistered || health == null)
            return;

        health.EnsureEvents();
        if (health.onDeath == null)
            return;

        health.onDeath.AddListener(OnDeath);
        deathListenerRegistered = true;
    }

    private void UnregisterDeathListener()
    {
        if (!deathListenerRegistered || health == null)
        {
            deathListenerRegistered = false;
            return;
        }

        health.EnsureEvents();
        if (health.onDeath != null)
            health.onDeath.RemoveListener(OnDeath);
        deathListenerRegistered = false;
    }

    public void Configure(
        float maxHealth,
        float explodeDelay,
        float explosionDamageValue,
        float explosionRadiusValue,
        float zoneRadius,
        float zoneTickDamage,
        float zoneTickInterval,
        float zoneDuration,
        GameObject explosionVFX,
        GameObject zoneVFX,
        bool chase,
        float slimeMoveSpeed,
        float slimeRetargetInterval,
        float slimeAttackDistance,
        float slimeMeleeDamage,
        float slimeMeleeInterval,
        AnimationClip slimeWalkClip,
        string slimeWalkStateName,
        AnimationClip slimeIdleClip,
        string slimeIdleStateName,
        AnimationClip slimeKnockdownClip,
        string slimeKnockdownStateName,
        bool slimeUseDirectKnockdownClipPlayback,
        float slimeKnockdownDurationOverride,
        float slimeDestroyDelayAfterKnockdown)
    {
        ResolveReferences();

        health.maxHealth = Mathf.Max(1f, maxHealth);
        health.startWithFullHealth = true;
        health.destroyOnDeath = false;
        health.SetDamageImmune(false);
        health.ResetHealth();

        explodeAfterSeconds = explodeDelay;
        explosionDamage = explosionDamageValue;
        explosionRadius = explosionRadiusValue;
        persistentZoneRadius = zoneRadius;
        persistentZoneTickDamage = zoneTickDamage;
        persistentZoneTickInterval = zoneTickInterval;
        persistentZoneDuration = zoneDuration;
        explosionVFXPrefab = explosionVFX;
        persistentZoneVFXPrefab = zoneVFX;
        chaseRandomPartyMember = chase;
        moveSpeed = slimeMoveSpeed > 0f ? slimeMoveSpeed : moveSpeed;
        retargetInterval = Mathf.Max(0.25f, slimeRetargetInterval);
        attackDistance = Mathf.Max(0.1f, slimeAttackDistance);
        meleeDamage = Mathf.Max(0f, slimeMeleeDamage);
        meleeInterval = Mathf.Max(0.1f, slimeMeleeInterval);
        walkClip = slimeWalkClip;
        if (!string.IsNullOrWhiteSpace(slimeWalkStateName))
            walkStateName = slimeWalkStateName;
        idleClip = slimeIdleClip;
        if (!string.IsNullOrWhiteSpace(slimeIdleStateName))
            idleStateName = slimeIdleStateName;

        knockdownClip = slimeKnockdownClip;
        if (!string.IsNullOrWhiteSpace(slimeKnockdownStateName))
            knockdownStateName = slimeKnockdownStateName;
        useDirectKnockdownClipPlayback = slimeUseDirectKnockdownClipPlayback;
        knockdownDurationOverride = Mathf.Max(0f, slimeKnockdownDurationOverride);
        destroyDelayAfterKnockdown = Mathf.Max(0f, slimeDestroyDelayAfterKnockdown);

        spawnTime = Time.time;
        nextRetargetTime = float.PositiveInfinity;
        resolved = false;
        killedMetricReported = false;
        explodedMetricReported = false;
        knockdownRoutine = null;
        RegisterDeathListener();
        PrepareAgent();
        ChooseRandomTarget();
        ReportSpawnedOnce();
    }

    private void ReportSpawnedOnce()
    {
        if (spawnMetricReported)
            return;

        spawnMetricReported = true;
        RaidMetricsEvents.ReportSlimeSpawned(this);
    }


    private void LateUpdate()
    {
        if (resolved)
            return;

        if (lockAnimatorRootLocalTransform && hasAnimatorInitialLocalTransform && animatorTransform != null && animatorTransform != transform)
        {
            animatorTransform.localPosition = animatorInitialLocalPosition;
            animatorTransform.localRotation = animatorInitialLocalRotation;
            animatorTransform.localScale = animatorInitialLocalScale;
        }

        if (forceTransformToAgentPositionAfterAnimation && agent != null && agent.enabled && agent.isOnNavMesh && !agent.updatePosition)
            transform.position = agent.nextPosition;
    }

    private void CacheAnimatorLocalTransformIfNeeded()
    {
        if (animator == null)
            return;

        animatorTransform = animator.transform;
        if (animatorTransform == null || animatorTransform == transform || hasAnimatorInitialLocalTransform)
            return;

        animatorInitialLocalPosition = animatorTransform.localPosition;
        animatorInitialLocalRotation = animatorTransform.localRotation;
        animatorInitialLocalScale = animatorTransform.localScale;
        hasAnimatorInitialLocalTransform = true;
    }

    private void Update()
    {
        if (resolved || knockdownRoutine != null)
            return;

        if (health != null && health.IsDead)
        {
            OnDeath(health);
            return;
        }

        if (explodeWhenTimerEnds && Time.time >= spawnTime + explodeAfterSeconds)
        {
            Explode();
            return;
        }

        if (chaseRandomPartyMember)
            ChaseAndAttack();
        else
            PlayIdleAnimation();

        KeepClipLoopingIfNeeded();
    }

    private void ChaseAndAttack()
    {
        bool targetLost = currentTarget == null || currentTarget.Health == null || currentTarget.Health.IsDead;

        if (targetLost)
        {
            if (!lockInitialTarget || chooseNewTargetIfInitialTargetLost)
                ChooseRandomTarget();
        }
        else if (!lockInitialTarget && Time.time >= nextRetargetTime)
        {
            ChooseRandomTarget();
        }

        if (currentTarget == null || currentTarget.Health == null || currentTarget.Health.IsDead)
        {
            StopMoving();
            PlayIdleAnimation();
            return;
        }

        Vector3 delta = currentTarget.transform.position - transform.position;
        delta.y = 0f;
        float distance = delta.magnitude;

        if (distance > attackDistance)
        {
            MoveTo(currentTarget.transform.position, delta);
            PlayWalkAnimation();
        }
        else
        {
            StopMoving();
            PlayIdleAnimation();
            FaceDirection(delta);

            if (Time.time >= nextMeleeTime)
            {
                currentTarget.TakeDamage(meleeDamage, gameObject);
                nextMeleeTime = Time.time + meleeInterval;
            }
        }
    }

    private void ChooseRandomTarget()
    {
        // 레지스트리 순회 + 통합 유효성 검사, 재사용 버퍼로 할당 제거
        PartyTargetUtility.CollectValidPlayerTargets(targetCandidateBuffer);
        System.Collections.Generic.List<PlayerStatus> candidates = targetCandidateBuffer;

        if (candidates.Count == 0)
        {
            currentTarget = null;
            nextRetargetTime = Time.time + retargetInterval;
            return;
        }

        currentTarget = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        nextRetargetTime = lockInitialTarget ? float.PositiveInfinity : Time.time + retargetInterval;
    }

    private void MoveTo(Vector3 destination, Vector3 flatDelta)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = attackDistance;

            Vector3 sampledDestination = destination;
            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, navMeshSampleRadius, navMeshAreaMask))
                sampledDestination = hit.position;

            agent.isStopped = false;
            agent.SetDestination(sampledDestination);

            Vector3 velocity = agent.velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude > 0.01f)
                FaceDirection(velocity);
            else
                FaceDirection(flatDelta);
        }
        else if (flatDelta.sqrMagnitude > 0.001f)
        {
            transform.position += flatDelta.normalized * moveSpeed * Time.deltaTime;
            FaceDirection(flatDelta);
        }
    }

    private void StopMoving()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void PlayWalkAnimation()
    {
        if (playingWalk)
            return;

        playingWalk = true;
        PlayAnimation(walkClip, walkStateName, walkClipSpeed);
    }

    private void PlayIdleAnimation()
    {
        if (!playingWalk)
            return;

        playingWalk = false;
        PlayAnimation(idleClip, idleStateName, idleClipSpeed);
    }

    private void PlayAnimation(AnimationClip clip, string stateName, float speed)
    {
        if (animator == null)
            return;

        animator.enabled = true;
        if (disableRootMotion)
            animator.applyRootMotion = false;

        if (useDirectClipPlayback && clip != null)
        {
            // 슬라임 클립은 항상 반복 재생 (Update의 KeepClipLoopingIfNeeded에서 유지)
            ClipPlayer.Play(animator, clip, speed, true);
            return;
        }

        StopDirectClip();

        string resolved = ResolveAnimatorStateName(stateName);
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            animator.speed = Mathf.Max(0.01f, speed);
            animator.CrossFadeInFixedTime(resolved, 0.1f, 0, 0f);
        }
    }

    private string ResolveAnimatorStateName(string stateName)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return string.Empty;

        return AnimatorStateUtility.ResolveStateName(animator, stateName);
    }

    private void RemoveDemoAnimationPlayerComponents()
    {
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            System.Type type = behaviour.GetType();
            string typeName = type != null ? type.Name : string.Empty;
            string fullName = type != null ? type.FullName : string.Empty;

            if (typeName.Contains("SimpleAnimationPlayer") || fullName.Contains("SimpleAnimationPlayer"))
                behaviour.enabled = false;
        }
    }

    private void KeepClipLoopingIfNeeded()
    {
        clipPlayer?.Tick();
    }

    private void StopDirectClip()
    {
        clipPlayer?.Stop();
    }

    private void OnDeath(Health deadHealth)
    {
        if (resolved || knockdownRoutine != null)
            return;

        if (!killedMetricReported)
        {
            killedMetricReported = true;
            RaidMetricsEvents.ReportSlimeKilled(this);
        }

        if (hideHealthBarOnKnockdown)
            HideHealthBarImmediately();

        StopMoving();
        DisableRuntimeCollisionAfterKnockdown();
        knockdownRoutine = StartCoroutine(KnockdownAndDestroyRoutine());
    }

    private IEnumerator KnockdownAndDestroyRoutine()
    {
        StopDirectClip();
        PlayKnockdownAnimation();

        float duration = ResolveKnockdownDuration();
        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        resolved = true;
        knockdownRoutine = null;

        if (destroyWhenKilled)
            Destroy(gameObject, Mathf.Max(0f, destroyDelayAfterKnockdown));
    }

    private void PlayKnockdownAnimation()
    {
        if (animator == null)
            return;

        animator.enabled = true;
        animator.applyRootMotion = false;

        bool canUseClip = useDirectKnockdownClipPlayback && knockdownClip != null;
        if (canUseClip)
        {
            // 기존 구현과 동일하게 넉다운 클립도 반복 유지 대상에 포함
            ClipPlayer.Play(animator, knockdownClip, 1f, true);
            return;
        }

        StopDirectClip();
        string resolvedState = ResolveAnimatorStateName(knockdownStateName);
        if (!string.IsNullOrWhiteSpace(resolvedState))
        {
            animator.speed = 1f;
            animator.CrossFadeInFixedTime(resolvedState, 0.08f, 0, 0f);
            animator.Update(0f);
        }
    }

    private float ResolveKnockdownDuration()
    {
        if (knockdownDurationOverride > 0.01f)
            return knockdownDurationOverride;

        if (knockdownClip != null)
            return Mathf.Max(0.05f, knockdownClip.length);

        return Mathf.Max(0.05f, destroyDelayAfterKilled);
    }

    private void HideHealthBarImmediately()
    {
        if (healthBarUI == null)
            healthBarUI = GetComponent<WorldHealthBarUI>();

        if (healthBarUI == null)
            return;

        healthBarUI.hideWhenDead = true;
        healthBarUI.enabled = false;

        Canvas[] canvases = healthBarUI.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null)
                canvases[i].gameObject.SetActive(false);
        }
    }

    private void DisableRuntimeCollisionAfterKnockdown()
    {
        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            agent.enabled = false;
        }

        if (damageReceiver != null)
            damageReceiver.enabled = false;

        if (hideHealthBarOnKnockdown)
            HideHealthBarImmediately();

        if (!disableCollidersOnKnockdown)
            return;

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }
    }

    private void Explode()
    {
        if (resolved)
            return;

        resolved = true;
        if (!explodedMetricReported)
        {
            explodedMetricReported = true;
            RaidMetricsEvents.ReportSlimeExploded(this);
        }
        StopMoving();
        StopDirectClip();

        if (explosionVFXPrefab != null)
            Destroy(Instantiate(explosionVFXPrefab, transform.position, Quaternion.identity), 4f);

        ApplyExplosionDamage();
        SlimeDotZone.Create(transform.position, persistentZoneRadius, persistentZoneTickDamage, persistentZoneTickInterval, persistentZoneDuration, persistentZoneVFXPrefab);

        Destroy(gameObject);
    }

    private void ApplyExplosionDamage()
    {
        // 통합 유효성 검사 적용 (폭발이 보스 더미를 때리던 문제 수정)
        System.Collections.Generic.IReadOnlyList<PlayerStatus> statuses = CombatRegistry.PlayerStatuses;
        for (int i = 0; i < statuses.Count; i++)
        {
            PlayerStatus status = statuses[i];
            if (!PartyTargetUtility.IsValidPlayerTarget(status))
                continue;

            float distance = Vector3.Distance(transform.position, status.transform.position);
            if (distance <= explosionRadius)
                status.TakeDamage(explosionDamage, gameObject);
        }
    }

    private void OnDestroy()
    {
        CombatRegistry.Unregister(this);

        if (knockdownRoutine != null)
        {
            StopCoroutine(knockdownRoutine);
            knockdownRoutine = null;
        }

        clipPlayer?.Dispose();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, persistentZoneRadius);
    }
}
