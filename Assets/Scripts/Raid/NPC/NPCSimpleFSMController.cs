// FSM NPC AI
// 평소에는 플레이어를 따라다니고, 전투 상태가 되면 보스를 공격
// 이동 실행은 NPCLocomotion, 역할별 의사결정은 INpcRolePolicy 구현 클래스가 담당

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 값은 CSV의 algorithm 이름과 인스펙터 직렬화에 사용되므로 기존 항목의 순서를 바꾸지 않고 뒤에만 추가
public enum NPCFSMMode
{
    BasicFSM,
    PatternAwareFSM,
    Utility,
    GCN,
    RGCN,
    GAT
}

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
    [Tooltip("단일 힐의 시전 접근 거리. 힐러는 대상 아군에게 이 거리까지 다가간 뒤 시전 (다가가서 힐하는 설계의 보장)")]
    public float healerHealCastDistance = 3f;
    public float keepDistanceFromBoss = 3.0f;

    [Header("Boss Distance")]
    [Tooltip("보스 거리 판정을 콜라이더 표면 기준으로 수행할지 여부. 거대 보스 모델에서 중심점 거리 오차로 공격 창에 진입하지 못하는 문제의 보정")]
    public bool useBossColliderDistance = true;
    [Tooltip("퇴각 후 재퇴각 반복을 막기 위한 여유 거리. 퇴각 목적지는 keepDistanceFromBoss + 이 값 지점")]
    public float keepDistanceMargin = 0.5f;
    [Tooltip("주시 지점과 반대 방향으로 이동할 때 몸을 돌려 이동할지 여부 (뒷걸음 문워크 방지). 도착 후 공격 시 다시 보스를 바라봄")]
    public bool turnAroundWhenMovingBackward = true;
    [Tooltip("이동 방향과 주시 방향의 각도가 이 값 이상이면 몸을 돌려 이동. 미만이면 주시 유지(스트레이프)")]
    [Range(90f, 180f)] public float backpedalTurnAngle = 120f;

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

    [Header("Scripted Player")]
    [Tooltip("실험용 자동 플레이어 여부. 실험 셋업의 알고리즘 일괄 변경에서 제외되고, 비전투 시 보스에게 접근하여 전투를 개시")]
    public bool isScriptedPlayer = false;
    [Tooltip("행동을 NPC 지표에 보고할지 여부. 자동 플레이어는 NPC 행동 분포를 오염시키지 않도록 false 사용")]
    public bool reportActionsToMetrics = true;

    [Header("Pattern-aware FSM")]
    public bool avoidDangerZones = true;
    public float dangerCheckPadding = 0.15f;
    public float safeSearchRadius = 5f;
    public int safeSearchRings = 3;
    public int safeSearchSamplesPerRing = 12;
    public float safeDestinationStopDistance = 0.2f;
    [Tooltip("안전 지점이 위험 지역 경계에서 최소 이만큼 떨어지도록 하는 여유 거리. 경계 바로 바깥을 골라 곧바로 재진입하는 반복 방지")]
    public float safeExitMargin = 1f;
    [Tooltip("기본 반경에서 안전 지점을 못 찾으면 반경을 이 배수까지 단계적으로 넓혀 재탐색 (맵 끝의 넓은 장판 탈출용)")]
    [Range(1, 4)] public int safeSearchMaxRadiusMultiplier = 3;
    public bool healerProtectDangerAlly = true;
    public bool prioritizeSlimesWhenBossImmune = true;
    public bool spreadDuringTrackingLightning = true;
    [Min(0.5f)] public float spreadMinSeparation = 2.5f;
    [Min(1f)] public float spreadDistance = 4f;
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

    // 이번 think 틱에 선택된 정책 행동 (그래프 스냅샷의 policy_action_id 입력용)
    public NpcAction CurrentPolicyAction { get; private set; } = NpcAction.None;
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

    // 현재 정책이 만들어질 때의 aiMode (모드 변경 시 정책 재생성 판단용)
    private NPCFSMMode policyBuiltForMode;

    // DAgger 라벨용 교사 조언자: 어떤 모드로 조종 중이든 "교사(Utility)라면 지금 뭘 골랐을까"를 계산.
    // 그래프 스냅샷에 teacher_action_id로 기록되어 학습 모델 롤아웃 상태의 재라벨링에 사용
    private NpcUtilityPolicy teacherAdvisor;
    private NPCRole teacherAdvisorRole;

    public int ComputeTeacherAdviceId()
    {
        if (teacherAdvisor == null || teacherAdvisorRole != role)
        {
            teacherAdvisor = new NpcUtilityPolicy(role == NPCRole.Tank, role == NPCRole.Healer);
            teacherAdvisorRole = role;
        }

        return (int)teacherAdvisor.AdviseBestAction(this);
    }

    // 역할 판정 후 해당 역할과 모드의 의사결정 정책 생성
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
        policyBuiltForMode = aiMode;

        if (aiMode == NPCFSMMode.Utility)
        {
            rolePolicy = new NpcUtilityPolicy(role == NPCRole.Tank, role == NPCRole.Healer);
            return;
        }

        // 학습 모델 조건은 모두 같은 정책 클래스를 사용하고, 어떤 모델로 추론할지는 aiMode로 구분
        if (aiMode == NPCFSMMode.GCN || aiMode == NPCFSMMode.RGCN || aiMode == NPCFSMMode.GAT)
        {
            rolePolicy = new NpcGnnPolicy(role == NPCRole.Tank, role == NPCRole.Healer);
            return;
        }

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
            ReportAction(NpcAction.MoveToSafePosition);
            return;
        }

        // 추적 낙뢰 진행 중이면 파티원과의 간격 확보 (패턴 인지 모드)
        if (IsPatternAware() && spreadDuringTrackingLightning
            && NpcActionMask.IsAvailable(this, NpcAction.SpreadFromParty, includeTacticalChecks: true)
            && TrySpreadFromParty())
        {
            ReportAction(NpcAction.SpreadFromParty);
            return;
        }

        if (!inCombat)
        {
            // 자동 플레이어는 사람이 보스에게 걸어가 전투를 여는 행동을 대신 수행
            if (isScriptedPlayer && boss != null
                && NpcActionMask.IsAvailable(this, NpcAction.MoveToBoss, includeTacticalChecks: false))
            {
                MoveToCombatRange(boss.transform.position, Mathf.Max(1f, combatEnterDistanceFromPlayerToBoss * 0.6f));
                SetStateLabel("Player Approach Boss");
                ReportAction(NpcAction.MoveToBoss);
                return;
            }

            FollowPlayerFormation();
            SetStateLabel("Follow Player");
            ReportAction(NpcAction.FollowPlayer);
            return;
        }

        // 실험 셋업이 실행 중 aiMode를 바꾸면 정책도 그에 맞게 재생성
        if (rolePolicy == null || policyBuiltForMode != aiMode)
            ResolveRole();

        // 역할별 정책이 행동을 결정하고, 보고는 여기서 1회만 수행
        NpcAction action = rolePolicy != null ? rolePolicy.Think(this) : NpcAction.None;
        ReportAction(action);
    }

    // 선택된 행동을 기록하고 지표에 1회 보고
    private void ReportAction(NpcAction action)
    {
        CurrentPolicyAction = action;

        // 자동 플레이어의 행동은 NPC 행동 분포 지표에서 제외
        if (action == NpcAction.None || !reportActionsToMetrics)
            return;

        RaidMetricsEvents.ReportNPCAction(this, NpcActions.GetName(action));
    }

    // 가장 가까운 파티원과의 간격이 기준보다 좁으면 반대 방향으로 산개
    private bool TrySpreadFromParty()
    {
        PlayerStatus nearest = null;
        float nearestDistance = float.PositiveInfinity;

        var allies = CombatRegistry.PlayerStatuses;
        for (int i = 0; i < allies.Count; i++)
        {
            PlayerStatus ally = allies[i];
            if (ally == null || ally.gameObject == gameObject || !PartyTargetUtility.IsValidPlayerTarget(ally))
                continue;

            float distance = FlatDistance(transform.position, ally.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = ally;
            }
        }

        // 이미 충분히 흩어져 있으면 산개 불필요 (일반 판단 계속)
        if (nearest == null || nearestDistance >= spreadMinSeparation)
            return false;

        MoveAwayFrom(nearest.transform.position, spreadDistance);
        SetStateLabel("SpreadFromParty");
        return true;
    }

    public bool IsPatternAware()
    {
        return aiMode == NPCFSMMode.PatternAwareFSM;
    }

    public bool IsUtility()
    {
        return aiMode == NPCFSMMode.Utility;
    }

    // 위험 지역 등 전술 정보를 활용하는 모드인지 (BasicFSM만 순진한 기준선으로 제외)
    public bool UsesTacticalReasoning()
    {
        return aiMode != NPCFSMMode.BasicFSM;
    }

    // 진행 중인 탈출 목적지 (매 틱 재선정으로 인한 갈지자 이동 방지)
    private Vector3 committedSafeDestination;
    private bool hasCommittedSafeDestination;

    private bool TryMoveToSafePosition(string reason)
    {
        if (!DangerZoneRegistry.IsPointInAnyZone(transform.position, dangerCheckPadding))
        {
            // 벽 그림자 덕분에만 안전한 위치(즉사기 차폐 지대)에서는 제자리 대기 유지.
            // 여기서 대피 상태를 풀면 다른 행동(보스 접근 등)으로 그림자를 벗어나
            // 발사 순간 노출되는 경계 왕복이 생기므로, 위험이 끝날 때까지 홀드
            if (DangerZoneRegistry.IsPointShadowProtected(transform.position, dangerCheckPadding))
            {
                hasCommittedSafeDestination = false;
                ClearMovement();
                SetStateLabel(reason + ": HoldBehindWall");
                return true;
            }

            hasCommittedSafeDestination = false;
            return false;
        }

        // 기존 탈출 목적지가 여전히 안전하고 아직 도착 전이면 그대로 유지
        if (hasCommittedSafeDestination)
        {
            bool destinationStillSafe = !DangerZoneRegistry.IsPointInAnyZone(committedSafeDestination, dangerCheckPadding + safeExitMargin * 0.5f);
            bool notArrivedYet = FlatDistance(transform.position, committedSafeDestination) > safeDestinationStopDistance + 0.1f;

            if (destinationStillSafe && notArrivedYet)
            {
                MoveToward(committedSafeDestination, safeDestinationStopDistance);
                SetStateLabel(reason + ": MoveToSafePosition");
                return true;
            }

            hasCommittedSafeDestination = false;
        }

        if (!TryFindSafeDestination(out Vector3 safeDestination))
            return false;

        committedSafeDestination = safeDestination;
        hasCommittedSafeDestination = true;

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

        // 등록된 안전 지점 힌트 우선 검토 (즉사기 벽 그림자처럼 좁은 안전 지대는
        // 원형 표본 추출이 놓칠 수 있으므로 기믹이 등록한 지점을 먼저 확인)
        if (TryPickSafeHint(current, out safeDestination))
            return true;

        // 기본 반경에서 못 찾으면 반경을 단계적으로 넓혀 재탐색 (맵 끝의 넓은 장판 대응)
        int maxMultiplier = Mathf.Max(1, safeSearchMaxRadiusMultiplier);
        for (int multiplier = 1; multiplier <= maxMultiplier; multiplier++)
        {
            if (TryFindSafeDestinationWithinRadius(current, Mathf.Max(1f, safeSearchRadius) * multiplier, out safeDestination))
                return true;
        }

        return false;
    }

    // 안전 지점 힌트 중 실제로 안전하고 도달 가능한 가장 가까운 지점 선택
    private bool TryPickSafeHint(Vector3 current, out Vector3 safeDestination)
    {
        safeDestination = current;

        IReadOnlyList<Vector3> hints = DangerZoneRegistry.SafeHints;
        if (hints == null || hints.Count == 0)
            return false;

        float exitPadding = dangerCheckPadding + Mathf.Max(0f, safeExitMargin);
        float bestDistance = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < hints.Count; i++)
        {
            Vector3 candidate = hints[i];
            if (DangerZoneRegistry.IsPointInAnyZone(candidate, exitPadding))
                continue;

            Vector3 navDestination = candidate;
            if (Locomotion.UsingNavMesh && navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            {
                if (!Locomotion.TryGetReachableNavDestination(candidate, out navDestination))
                    continue;

                if (DangerZoneRegistry.IsPointInAnyZone(navDestination, exitPadding))
                    continue;
            }

            float distance = FlatDistance(current, navDestination);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                safeDestination = navDestination;
                found = true;
            }
        }

        return found;
    }

    private bool TryFindSafeDestinationWithinRadius(Vector3 current, float maxRadius, out Vector3 safeDestination)
    {
        safeDestination = current;

        Vector3 best = current;
        float bestScore = float.NegativeInfinity;
        bool found = false;

        int rings = Mathf.Max(1, safeSearchRings);
        int samples = Mathf.Max(6, safeSearchSamplesPerRing);
        float exitPadding = dangerCheckPadding + Mathf.Max(0f, safeExitMargin);

        for (int ring = 1; ring <= rings; ring++)
        {
            float radius = maxRadius * ring / rings;
            for (int i = 0; i < samples; i++)
            {
                float angle = (Mathf.PI * 2f) * i / samples;
                Vector3 candidate = current + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                // 경계 바로 바깥 지점 제외: 여유 거리까지 확보되어야 탈출 지점으로 인정
                if (DangerZoneRegistry.IsPointInAnyZone(candidate, exitPadding))
                    continue;

                Vector3 navDestination = candidate;
                if (Locomotion.UsingNavMesh && navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
                {
                    if (!Locomotion.TryGetReachableNavDestination(candidate, out navDestination))
                        continue;

                    // NavMesh 클램프로 지점이 이동했으면 안전 조건 재확인
                    if (DangerZoneRegistry.IsPointInAnyZone(navDestination, exitPadding))
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

    // 역할별 교전 거리 (근접 역할은 밀착, 그 외는 사거리 유지). 행동 Mask의 거리 조건에 사용
    public float DesiredCombatDistance => role == NPCRole.Tank ? meleeDistance : rangedDistance;

    public bool IsBossFlyingAndUntargetable()
    {
        return blockOffensiveActionsWhileBossFlying && bossSkillPattern != null && bossSkillPattern.IsFlying;
    }

    // 지정한 슬롯으로 공격 1틱 실행 (스킬 선택을 학습 대상으로 만들기 위한 슬롯별 실행 경로).
    // 슬롯 스킬이 실패하면 기본 공격으로 대체하여 틱이 낭비되지 않도록 함
    public void ExecuteAttackWithSlot(PlayerSkillSlot? slot)
    {
        if (!IsActionReady)
            return;

        ConsumeActionCooldown();

        bool usedSkill = false;
        if (slot.HasValue && skillController != null && IsSkillTickReady)
        {
            ConsumeSkillCooldown();

            switch (slot.Value)
            {
                case PlayerSkillSlot.Skill1:
                    usedSkill = skillController.TryUseSkill1();
                    break;
                case PlayerSkillSlot.Skill2:
                    usedSkill = skillController.TryUseSkill2();
                    break;
                case PlayerSkillSlot.Ultimate:
                    usedSkill = skillController.TryUseUltimate();
                    break;
            }
        }

        if (!usedSkill && basicAttack != null)
            basicAttack.TryBasicAttack();
    }

    // 공격 1틱의 자동 선택 구현: 행동 쿨다운 소비 후 스킬(궁극기 허용 시 우선) 또는 기본 공격 시도.
    // 슬라임 공격과 같이 슬롯 선택이 필요 없는 경로에서 사용
    public void ExecuteAttackTick(bool allowUltimate)
    {
        if (!IsActionReady)
            return;

        ConsumeActionCooldown();

        bool usedSkill = false;
        if (IsSkillTickReady && skillController != null)
        {
            ConsumeSkillCooldown();

            if (allowUltimate && useUltimateWhenReady)
                usedSkill = skillController.TryUseUltimate();

            if (!usedSkill)
                usedSkill = skillController.TryUseSkill1();

            if (!usedSkill)
                usedSkill = skillController.TryUseSkill2();
        }

        if (!usedSkill && basicAttack != null)
            basicAttack.TryBasicAttack();
    }

    // 후퇴 가능 여부: 기준점 반대 방향의 후퇴 목적지가 NavMesh 위에 있고
    // 실제로 기준점에서 더 멀어질 수 있는지. 맵 끝에 몰린 경우 false
    // (이때는 후퇴 대신 근접 교전을 유지해야 제자리 반복이 없음)
    public bool CanRetreatFrom(Vector3 point, float desiredSeparation)
    {
        // NavMesh를 쓰지 않는 폴백 이동은 항상 시도 가능으로 간주
        if (!useNavMeshAgent || navMeshAgent == null || !Locomotion.UsingNavMesh)
            return true;

        Vector3 direction = transform.position - point;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;
        direction.Normalize();

        // MoveAwayFrom과 동일한 목적지 계산
        float currentSeparation = FlatDistance(transform.position, point);
        float targetSeparation = Mathf.Max(desiredSeparation, keepDistanceFromBoss);
        if (currentSeparation >= targetSeparation)
            targetSeparation = currentSeparation + 1f;

        Vector3 destination = point + direction * targetSeparation;

        // 경로가 실제로 도달하는 끝 지점 기준으로 판단
        // (장애물 건너편의 연결되지 않은 NavMesh 섬을 후퇴 지점으로 오인하는 문제 방지)
        if (!Locomotion.TryGetReachableEndpoint(destination, out Vector3 reachableEndpoint))
            return false;

        // 실제 도달 지점이 기준점에서 지금보다 확실히 멀어지는 위치여야 후퇴로 인정
        return FlatDistance(reachableEndpoint, point) > currentSeparation + 0.3f;
    }

    // Utility 정책이 사용하는 실행 진입점 (내부 구현은 패턴 인지 FSM과 공유)
    public bool TryExecuteSafeEscape(string reason)
    {
        return TryMoveToSafePosition(reason);
    }

    public bool TryExecuteSpreadFromParty()
    {
        return TrySpreadFromParty();
    }

    public void FollowPlayerFormation()
    {
        Locomotion.FollowPlayerFormation();
    }

    public void MoveToCombatRange(Vector3 targetPosition, float desiredDistance)
    {
        Locomotion.MoveToCombatRange(targetPosition, desiredDistance);
    }

    // 전투 접근 목적지가 위험 지역이면 같은 거리의 다른 각도 지점으로 회피.
    // 장판을 탈출한 직후 그대로 장판 안의 사거리 지점으로 되돌아가는 반복 방지.
    // BasicFSM은 순진한 접근을 유지해야 하므로 패턴 인지 모드에서만 회피 동작 (기준선 보존)
    private static readonly float[] combatApproachAngles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f };

    public void MoveToCombatRangeAvoidingDanger(Vector3 targetPosition, float desiredDistance)
    {
        if (!UsesTacticalReasoning() || !avoidDangerZones)
        {
            MoveToCombatRange(targetPosition, desiredDistance);
            return;
        }

        Vector3 away = transform.position - targetPosition;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f)
            away = -transform.forward;
        away.Normalize();

        // 현재 방향부터 좌우로 각도를 넓혀가며 위험 지역 밖의 접근 지점 탐색
        for (int i = 0; i < combatApproachAngles.Length; i++)
        {
            Vector3 direction = Quaternion.Euler(0f, combatApproachAngles[i], 0f) * away;
            Vector3 candidate = targetPosition + direction * Mathf.Max(0.1f, desiredDistance);

            if (!DangerZoneRegistry.IsPointInAnyZone(candidate, dangerCheckPadding))
            {
                MoveToward(candidate, 0.2f);
                return;
            }
        }

        // 모든 각도가 위험 지역이면 기존 접근 유지 (다음 틱의 자기 위험 회피가 처리)
        MoveToCombatRange(targetPosition, desiredDistance);
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

    // 이동 중에도 지정 지점을 계속 바라보게 지시 (전투 재배치용, 다음 think 틱의 ClearMovement에서 해제)
    public void FaceWhileMoving(Vector3 position)
    {
        Locomotion.FaceWhileMoving(position);
    }

    // ---------- 보스 거리 판정 (콜라이더 표면 기준) ----------

    private BossDummyController aimColliderBoss;
    private Collider[] aimColliders;

    // 보스를 향한 조준점 반환.
    // 경계 상자 밖: XZ 평면에서 콜라이더 경계에 클램프한 최근접점 (저렴하고 원거리에서 충분히 정확)
    // 경계 상자 안: 몸체 중심 방향 레이캐스트로 실제 표면 지점 산출.
    // 대각선으로 누운 거대 용 모델은 경계 상자가 몸체보다 훨씬 커서
    // 상자 안 어디서나 거리 0으로 오판되고, 그 결과 몸에서 멀리 있어도
    // 상자를 벗어날 때까지 무한 후퇴(KeepDistance)하는 문제의 보정.
    public Vector3 GetBossAimPoint()
    {
        if (boss == null)
            return transform.position;

        Vector3 fallback = boss.transform.position;
        if (!useBossColliderDistance)
            return fallback;

        Collider[] colliders = ResolveBossColliders();
        if (colliders == null || colliders.Length == 0)
            return fallback;

        Vector3 origin = transform.position;
        Vector3 best = fallback;
        float bestSqr = float.MaxValue;
        bool found = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider bossCollider = colliders[i];
            // 패턴 예고용 트리거는 몸체 크기 판정에서 제외
            if (bossCollider == null || !bossCollider.enabled || bossCollider.isTrigger)
                continue;

            Bounds bounds = bossCollider.bounds;
            bool insideBoundsXZ = origin.x >= bounds.min.x && origin.x <= bounds.max.x
                && origin.z >= bounds.min.z && origin.z <= bounds.max.z;

            Vector3 point;
            if (!insideBoundsXZ)
            {
                point = origin;
                point.x = Mathf.Clamp(origin.x, bounds.min.x, bounds.max.x);
                point.z = Mathf.Clamp(origin.z, bounds.min.z, bounds.max.z);
            }
            else if (!TryRaycastBossSurface(bossCollider, origin, out point))
            {
                // 레이가 표면을 찾지 못하면 실제 몸체 내부로 간주 (거리 0)
                point = origin;
            }

            Vector3 flatDelta = point - origin;
            flatDelta.y = 0f;
            float sqr = flatDelta.sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = point;
                found = true;
            }
        }

        return found ? best : fallback;
    }

    // 경계 상자 안에서 몸체 중심 방향으로 수평 레이캐스트하여 실제 표면 지점 탐색.
    // 상자 중심과 보스 피벗 두 방향을 시도 (굽은 몸체로 상자 중심이 빈 공간인 경우 대비)
    private bool TryRaycastBossSurface(Collider bossCollider, Vector3 origin, out Vector3 surfacePoint)
    {
        surfacePoint = origin;
        Bounds bounds = bossCollider.bounds;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            Vector3 target = attempt == 0 ? bounds.center : boss.transform.position;

            Vector3 flatDirection = new Vector3(target.x - origin.x, 0f, target.z - origin.z);
            float flatDistance = flatDirection.magnitude;
            if (flatDistance < 0.05f)
                continue;

            // 몸체 높이(상자 중심 높이)에서 수평으로 발사
            Vector3 rayOrigin = new Vector3(origin.x, bounds.center.y, origin.z);
            Ray ray = new Ray(rayOrigin, flatDirection / flatDistance);

            if (bossCollider.Raycast(ray, out RaycastHit hit, flatDistance + 0.5f))
            {
                surfacePoint = new Vector3(hit.point.x, origin.y, hit.point.z);
                return true;
            }
        }

        return false;
    }

    // 보스 표면까지의 평면 거리 (전투 거리 판단의 단일 기준)
    public float DistanceToBoss()
    {
        return FlatDistance(transform.position, GetBossAimPoint());
    }

    private Collider[] ResolveBossColliders()
    {
        if (boss != aimColliderBoss || aimColliders == null)
        {
            aimColliderBoss = boss;
            aimColliders = boss != null ? boss.GetComponentsInChildren<Collider>(true) : null;
        }

        return aimColliders;
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
