// FSM NPC AI
// 평소에는 플레이어를 따라다니고, 전투 상태가 되면 보스를 공격
// 이동 실행은 NPCLocomotion, 역할별 의사결정은 INpcRolePolicy 구현 클래스가 담당

using UnityEngine;
using UnityEngine.AI;

public enum NPCFSMMode
{
    BasicFSM,
    PatternAwareFSM
}

[RequireComponent(typeof(NPCPartyMember))]
public class NPCSimpleFSMController : MonoBehaviour
{
    private enum NPCRole
    {
        Tank,
        DPS,
        Healer
    }

    private enum MotionState
    {
        None,
        Idle,
        Run,
        Suppressed
    }

    [Header("References")]
    public NPCPartyMember member;
    public PlayerClassInfo classInfo;
    public PlayerStatus status;
    public Health health;
    public PlayerBasicAttack basicAttack;
    public PlayerSkillController skillController;
    public CharacterController characterController;
    public NavMeshAgent navMeshAgent;
    public Animator animator;
    public Transform playerTarget;
    public BossDummyController boss;
    public BossSkillPatternController bossSkillPattern;

    [Header("Role")]
    public bool autoResolveRole = true;
    public string roleOverride = "";

    [Header("Follow Player")]
    public Vector3 formationOffset = new Vector3(0f, 0f, -1.5f);
    public float formationStopDistance = 0.6f;
    public bool faceSameDirectionAsPlayerWhenIdle = true;

    [Header("Combat Detection")]
    public bool enterCombatWhenBossDamaged = true;
    public float combatEnterDistanceFromPlayerToBoss = 12f;
    public float combatExitDistanceFromPlayerToBoss = 18f;
    public bool stayInCombatAfterEnter = false;

    [Header("Movement")]
    public float moveSpeed = 2f;
    public bool copyMoveSpeedFromPlayerMovement = true;
    public float rotationSpeed = 540f;
    public float gravity = -25f;
    public float groundedStickVelocity = -3f;
    public float meleeDistance = 2.4f;
    public float rangedDistance = 5.5f;
    public float healerFollowDistance = 6f;
    public float keepDistanceFromBoss = 3.0f;

    [Header("NavMesh Movement")]
    public bool useNavMeshAgent = true;
    public bool addNavMeshAgentIfMissing = true;
    public float navMeshSampleRadius = 4f;
    public float destinationSampleRadius = 3f;
    public int navMeshAreaMask = NavMesh.AllAreas;
    public float agentRadius = 0.35f;
    public float agentHeight = 1.2f;
    public float agentAcceleration = 16f;
    public float agentAngularSpeed = 720f;
    public float agentAvoidancePriority = 50f;
    public bool disableCharacterControllerWhenUsingAgent = true;
    public bool validateNavMeshPath = true;
    public float alternateFormationRadius = 1.8f;
    public int alternateFormationSamples = 12;
    public float stuckRepathTime = 1.2f;

    [Header("AI Policy")]
    public NPCFSMMode aiMode = NPCFSMMode.BasicFSM;

    [Header("Pattern-aware FSM")]
    public bool avoidDangerZones = true;
    public float dangerCheckPadding = 0.15f;
    public float safeSearchRadius = 5f;
    public int safeSearchRings = 3;
    public int safeSearchSamplesPerRing = 12;
    public float safeDestinationStopDistance = 0.2f;
    public bool healerProtectDangerAlly = true;
    public float healerDangerShieldHpThreshold = 0.95f;
    public bool logPatternAwareDecisions = false;

    [Header("Decision")]
    public float thinkInterval = 0.25f;
    public float actionInterval = 0.8f;
    public float skillInterval = 1.2f;
    public float healerLowHpThreshold = 0.55f;
    public float healerShieldThreshold = 0.75f;
    public bool blockOffensiveActionsWhileBossFlying = true;
    public bool useUltimateWhenReady = true;

    [Header("Animation")]
    public bool useDirectClipPlayback = true;
    public AnimationClip idleClip;
    public AnimationClip runClip;
    public string idleStateName = "IdleA";
    public string runStateName = "Run";
    public float idleClipSpeed = 1f;
    public float runClipSpeed = 1f;
    public bool copyMovementClipsFromPlayerMovement = true;
    public float animationCrossFade = 0.08f;

    [Header("Debug")]
    [SerializeField] private bool inCombat;
    [SerializeField] private string currentState;
    [SerializeField] private string currentRoleLabel;
    [SerializeField] private bool usingNavMesh;
    [SerializeField] private string navMeshState;

    private NPCRole role;
    private INpcRolePolicy rolePolicy;
    private float nextThinkTime;
    private float nextActionTime;
    private float nextSkillTime;
    private MotionState motionState;

    // 이동 실행 계층
    private NPCLocomotion locomotion;
    private NPCLocomotion Locomotion => locomotion ??= new NPCLocomotion(this);

    // 이동 클립 직접 재생을 담당하는 공용 플레이어
    private SingleClipPlayer motionClipPlayer;
    private SingleClipPlayer MotionClipPlayer => motionClipPlayer ??= new SingleClipPlayer(name + "_NPCMotionGraph", "NPCMotion");

    public void Initialize(NPCPartyMember newMember, Transform newPlayerTarget, Vector3 newFormationOffset)
    {
        member = newMember;
        playerTarget = newPlayerTarget;
        formationOffset = newFormationOffset;
        SetupAll();
    }

    private void Awake()
    {
        SetupAll();
    }

    // Awake와 Initialize에 중복되어 있던 초기화 순서의 단일 구현
    private void SetupAll()
    {
        ResolveReferences();
        PrepareComponentsForAI();
        Locomotion.PrepareAgent();
        ResolveRole();
    }

    private void Start()
    {
        Locomotion.TryPlaceAgentOnNavMesh();
        UpdateMotionAnimation(false);
    }

    private void Update()
    {
        ResolveMissingReferences();

        if (health != null && health.IsDead)
        {
            Locomotion.ClearMovement();
            Locomotion.StopAgent();
            UpdateMotionAnimation(false);
            Locomotion.ApplyGravityOnly();
            SyncNavDebug();
            return;
        }

        if (IsActionBusy())
        {
            Locomotion.ClearMovement();
            Locomotion.StopAgent();
            StopDirectMotionClip();
            motionState = MotionState.Suppressed;
            Locomotion.ApplyGravityOnly();
            SyncNavDebug();
            return;
        }

        Locomotion.EnsureAgentState();
        UpdateCombatState();

        if (Time.time >= nextThinkTime)
        {
            nextThinkTime = Time.time + Mathf.Max(0.05f, thinkInterval);
            Think();
        }

        Locomotion.MoveAndRotate();
        KeepMotionClipLoopingIfNeeded();
        SyncNavDebug();
    }

    // 인스펙터 디버그 필드에 이동 계층 상태 반영
    private void SyncNavDebug()
    {
        usingNavMesh = Locomotion.UsingNavMesh;
        navMeshState = Locomotion.NavMeshState;
    }

    private void ResolveReferences()
    {
        if (member == null)
            member = GetComponent<NPCPartyMember>();

        if (classInfo == null)
            classInfo = GetComponent<PlayerClassInfo>();

        if (status == null)
            status = GetComponent<PlayerStatus>();

        if (status != null)
        {
            status.ResolveReferences();
            if (health == null)
                health = status.Health;
        }

        if (health == null)
            health = GetComponent<Health>();

        if (basicAttack == null)
            basicAttack = GetComponent<PlayerBasicAttack>();

        if (skillController == null)
            skillController = GetComponent<PlayerSkillController>();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (navMeshAgent == null)
            navMeshAgent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }

    private void ResolveMissingReferences()
    {
        // 매 프레임 씬 전체 탐색 대신 레지스트리 조회 (보스 부재 시에도 비용이 거의 없음)
        if (boss == null)
            boss = CombatRegistry.FirstBossDummy;

        if (bossSkillPattern == null && boss != null)
            bossSkillPattern = boss.GetComponent<BossSkillPatternController>();

        if (bossSkillPattern == null)
            bossSkillPattern = CombatRegistry.FirstBossSkillController;

        if (playerTarget == null)
        {
            RaidSelectedCharacterSpawner spawner = FindAnyObjectByType<RaidSelectedCharacterSpawner>();
            if (spawner != null && spawner.SpawnedPlayer != null)
                playerTarget = spawner.SpawnedPlayer.transform;
        }
    }

    private void PrepareComponentsForAI()
    {
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            if (copyMoveSpeedFromPlayerMovement && movement.moveSpeed > 0f)
                moveSpeed = movement.moveSpeed;

            if (copyMovementClipsFromPlayerMovement)
            {
                if (idleClip == null)
                    idleClip = movement.idleClip;

                if (runClip == null)
                    runClip = movement.runClip;

                idleStateName = string.IsNullOrWhiteSpace(movement.idleStateName) ? idleStateName : movement.idleStateName;
                runStateName = string.IsNullOrWhiteSpace(movement.runStateName) ? runStateName : movement.runStateName;
                idleClipSpeed = movement.idleClipSpeed;
                runClipSpeed = movement.runClipSpeed;
            }

            movement.enabled = false;
        }

        if (basicAttack != null)
        {
            basicAttack.enabled = true;
            basicAttack.useLeftMouseButton = false;
            basicAttack.useKeyboardNumber1 = false;
            basicAttack.ignoreInputWhenPointerIsOverUI = false;
            basicAttack.SetExternalActionLock(false);
        }

        if (skillController != null)
        {
            skillController.enabled = true;
            skillController.useKeyboardInput = false;
            skillController.ignoreInputWhenPointerIsOverUI = false;
        }

        if (animator != null)
            animator.applyRootMotion = false;
    }

    // 역할 판정 후 해당 역할의 의사결정 정책 생성
    private void ResolveRole()
    {
        string id = roleOverride;
        if (string.IsNullOrWhiteSpace(id) && classInfo != null)
            id = classInfo.characterId;
        if (string.IsNullOrWhiteSpace(id) && member != null)
            id = member.characterId;

        id = (id ?? string.Empty).ToLowerInvariant();

        if (id.Contains("healer"))
            role = NPCRole.Healer;
        else if (id.Contains("warrior") || id.Contains("tank"))
            role = NPCRole.Tank;
        else
            role = NPCRole.DPS;

        currentRoleLabel = role.ToString();

        rolePolicy = role == NPCRole.Healer
            ? new NpcHealerPolicy()
            : (INpcRolePolicy)new NpcFighterPolicy(role == NPCRole.Tank);
    }

    private void UpdateCombatState()
    {
        if (boss == null || playerTarget == null)
        {
            inCombat = false;
            return;
        }

        Health bossHealth = boss.health != null ? boss.health : boss.GetComponent<Health>();
        if (bossHealth == null || bossHealth.IsDead)
        {
            inCombat = false;
            return;
        }

        if (stayInCombatAfterEnter && inCombat)
            return;

        float playerBossDistance = FlatDistance(playerTarget.position, boss.transform.position);
        bool bossDamaged = enterCombatWhenBossDamaged && bossHealth.CurrentHealth < bossHealth.MaxHealth;

        if (!inCombat)
        {
            if (bossDamaged || playerBossDistance <= combatEnterDistanceFromPlayerToBoss)
                inCombat = true;
        }
        else
        {
            if (!bossDamaged && playerBossDistance >= combatExitDistanceFromPlayerToBoss)
                inCombat = false;
        }
    }

    private void Think()
    {
        ClearMovement();

        // 자기 자신이 위험 지역 안이면 회피가 최우선 (패턴 인지 모드)
        if (IsPatternAware() && avoidDangerZones && TryMoveToSafePosition("Self Danger"))
        {
            RaidMetricsEvents.ReportNPCAction(this, NpcActionNames.MoveToSafePosition);
            return;
        }

        if (!inCombat)
        {
            FollowPlayerFormation();
            SetStateLabel("Follow Player");
            RaidMetricsEvents.ReportNPCAction(this, NpcActionNames.FollowPlayer);
            return;
        }

        if (rolePolicy == null)
            ResolveRole();

        // 역할별 정책이 행동을 결정하고, 보고는 여기서 1회만 수행
        string action = rolePolicy != null ? rolePolicy.Think(this) : null;
        if (!string.IsNullOrEmpty(action))
            RaidMetricsEvents.ReportNPCAction(this, action);
    }

    public bool IsPatternAware()
    {
        return aiMode == NPCFSMMode.PatternAwareFSM;
    }

    private bool TryMoveToSafePosition(string reason)
    {
        if (!DangerZoneRegistry.IsPointInAnyZone(transform.position, dangerCheckPadding))
            return false;

        if (!TryFindSafeDestination(out Vector3 safeDestination))
            return false;

        MoveToward(safeDestination, safeDestinationStopDistance);
        SetStateLabel(reason + ": MoveToSafePosition");

        if (logPatternAwareDecisions)
            Debug.Log($"[NPCPatternAwareFSM] {name} moves to safe position. Reason={reason}", this);

        return true;
    }

    private bool TryFindSafeDestination(out Vector3 safeDestination)
    {
        safeDestination = transform.position;

        Vector3 current = transform.position;
        if (!DangerZoneRegistry.IsPointInAnyZone(current, dangerCheckPadding))
            return false;

        Vector3 best = current;
        float bestScore = float.NegativeInfinity;
        bool found = false;

        int rings = Mathf.Max(1, safeSearchRings);
        int samples = Mathf.Max(6, safeSearchSamplesPerRing);
        float maxRadius = Mathf.Max(1f, safeSearchRadius);

        for (int ring = 1; ring <= rings; ring++)
        {
            float radius = maxRadius * ring / rings;
            for (int i = 0; i < samples; i++)
            {
                float angle = (Mathf.PI * 2f) * i / samples;
                Vector3 candidate = current + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                if (DangerZoneRegistry.IsPointInAnyZone(candidate, dangerCheckPadding))
                    continue;

                Vector3 navDestination = candidate;
                if (Locomotion.UsingNavMesh && navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
                {
                    if (!Locomotion.TryGetReachableNavDestination(candidate, out navDestination))
                        continue;
                }

                float score = 0f;
                score -= FlatDistance(current, navDestination) * 0.15f;
                if (playerTarget != null)
                    score -= FlatDistance(playerTarget.position, navDestination) * 0.05f;

                DangerZoneRegistry.FindNearestZone(navDestination, out float nearestSqr);
                if (!float.IsInfinity(nearestSqr))
                    score += Mathf.Sqrt(nearestSqr) * 0.5f;

                if (!found || score > bestScore)
                {
                    found = true;
                    bestScore = score;
                    best = navDestination;
                }
            }
        }

        if (!found)
            return false;

        safeDestination = best;
        return true;
    }

    // ---------- 정책 클래스가 사용하는 공개 API ----------

    // 인스펙터 디버그용 상태 문자열 기록
    public void SetStateLabel(string label)
    {
        currentState = label;
    }

    public bool IsActionReady => Time.time >= nextActionTime;

    public void ConsumeActionCooldown()
    {
        nextActionTime = Time.time + Mathf.Max(0.1f, actionInterval);
    }

    public bool IsSkillTickReady => Time.time >= nextSkillTime;

    public void ConsumeSkillCooldown()
    {
        nextSkillTime = Time.time + Mathf.Max(0.2f, skillInterval);
    }

    public bool IsBossFlyingAndUntargetable()
    {
        return blockOffensiveActionsWhileBossFlying && bossSkillPattern != null && bossSkillPattern.IsFlying;
    }

    public void FollowPlayerFormation()
    {
        Locomotion.FollowPlayerFormation();
    }

    public void MoveToCombatRange(Vector3 targetPosition, float desiredDistance)
    {
        Locomotion.MoveToCombatRange(targetPosition, desiredDistance);
    }

    public void MoveToward(Vector3 position, float stoppingDistance = 0.1f)
    {
        Locomotion.MoveToward(position, stoppingDistance);
    }

    public void MoveAwayFrom(Vector3 position, float desiredSeparation)
    {
        Locomotion.MoveAwayFrom(position, desiredSeparation);
    }

    public void ClearMovement()
    {
        Locomotion.ClearMovement();
    }

    public void FacePosition(Vector3 position)
    {
        Locomotion.FacePosition(position);
    }

    public static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ---------- 애니메이션 ----------

    private bool IsActionBusy()
    {
        if (basicAttack != null && basicAttack.IsAttacking)
            return true;

        if (skillController != null && skillController.IsSkillInProgress)
            return true;

        return false;
    }

    // 이동 계층이 이동 여부에 따라 호출
    public void UpdateMotionAnimation(bool moving)
    {
        MotionState next = moving ? MotionState.Run : MotionState.Idle;
        if (motionState == next)
            return;

        motionState = next;

        if (useDirectClipPlayback)
        {
            AnimationClip clip = moving ? runClip : idleClip;
            float speed = moving ? runClipSpeed : idleClipSpeed;
            if (clip != null)
            {
                PlayDirectMotionClip(clip, speed);
                return;
            }
        }

        StopDirectMotionClip();
        string stateName = moving ? runStateName : idleStateName;
        TryPlayAnimatorState(stateName);
    }

    private void PlayDirectMotionClip(AnimationClip clip, float speed)
    {
        if (animator == null || clip == null)
            return;

        // NPC 이동 클립은 항상 반복 재생 (KeepMotionClipLoopingIfNeeded에서 유지)
        MotionClipPlayer.PlayIfChanged(animator, clip, speed, true);
    }

    private void StopDirectMotionClip()
    {
        motionClipPlayer?.Stop();
    }

    private void KeepMotionClipLoopingIfNeeded()
    {
        motionClipPlayer?.Tick();
    }

    private void TryPlayAnimatorState(string stateName)
    {
        if (animator == null)
            return;

        string resolved = AnimatorStateUtility.ResolveStateName(animator, stateName);
        if (!string.IsNullOrEmpty(resolved))
            animator.CrossFadeInFixedTime(resolved, animationCrossFade, 0, 0f);
    }

    private void OnDisable()
    {
        locomotion?.StopAgent();
        StopDirectMotionClip();
    }

    private void OnDestroy()
    {
        motionClipPlayer?.Dispose();
    }
}
