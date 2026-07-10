// FSM NPC AI
// 평소에는 플레이어를 따라다니고, 전투 상태가 되면 보스를 공격
// NavMeshAgent를 사용해 장애물을 피해 이동

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;

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
    public float formationRunDistance = 1.0f;
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
    private float verticalVelocity;
    private float nextThinkTime;
    private float nextActionTime;
    private float nextSkillTime;
    private MotionState motionState;
    private Vector3 desiredMoveDirection;
    private Vector3 desiredDestination;
    private bool hasDestination;
    private float currentStoppingDistance;
    private Vector3 lastAgentPosition;
    private float stuckTimer;
    private NavMeshPath reusablePath;

    private PlayableGraph motionGraph;
    private AnimationPlayableOutput motionOutput;
    private AnimationClipPlayable motionPlayable;
    private AnimationClip currentMotionClip;

    public void Initialize(NPCPartyMember newMember, Transform newPlayerTarget, Vector3 newFormationOffset)
    {
        member = newMember;
        playerTarget = newPlayerTarget;
        formationOffset = newFormationOffset;
        EnsureReusablePathCreated();
        ResolveReferences();
        PrepareComponentsForAI();
        PrepareNavMeshAgent();
        ResolveRole();
    }


    private void EnsureReusablePathCreated()
    {
        if (reusablePath == null)
            reusablePath = new NavMeshPath();
    }

    private void Awake()
    {
        EnsureReusablePathCreated();
        ResolveReferences();
        PrepareComponentsForAI();
        PrepareNavMeshAgent();
        ResolveRole();
    }

    private void Start()
    {
        EnsureReusablePathCreated();
        TryPlaceAgentOnNavMesh();
        UpdateMotionAnimation(false);
    }

    private void Update()
    {
        ResolveMissingReferences();

        if (health != null && health.IsDead)
        {
            ClearMovement();
            StopAgent();
            UpdateMotionAnimation(false);
            ApplyGravityOnly();
            return;
        }

        if (IsActionBusy())
        {
            ClearMovement();
            StopAgent();
            StopDirectMotionClip();
            motionState = MotionState.Suppressed;
            ApplyGravityOnly();
            return;
        }

        EnsureNavMeshAgentState();
        UpdateCombatState();

        if (Time.time >= nextThinkTime)
        {
            nextThinkTime = Time.time + Mathf.Max(0.05f, thinkInterval);
            Think();
        }

        MoveAndRotate();
        KeepMotionClipLoopingIfNeeded();
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
        if (boss == null)
            boss = FindAnyObjectByType<BossDummyController>();

        if (bossSkillPattern == null && boss != null)
            bossSkillPattern = boss.GetComponent<BossSkillPatternController>();

        if (bossSkillPattern == null)
            bossSkillPattern = FindAnyObjectByType<BossSkillPatternController>();

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

    private void PrepareNavMeshAgent()
    {
        if (!useNavMeshAgent)
        {
            usingNavMesh = false;
            RestoreCharacterControllerForFallback();
            return;
        }

        if (navMeshAgent == null && addNavMeshAgentIfMissing)
            navMeshAgent = gameObject.AddComponent<NavMeshAgent>();

        if (navMeshAgent == null)
        {
            usingNavMesh = false;
            navMeshState = "No NavMeshAgent";
            RestoreCharacterControllerForFallback();
            return;
        }

        navMeshAgent.speed = moveSpeed;
        navMeshAgent.angularSpeed = agentAngularSpeed;
        navMeshAgent.acceleration = agentAcceleration;
        navMeshAgent.radius = Mathf.Max(0.01f, agentRadius);
        navMeshAgent.height = Mathf.Max(0.1f, agentHeight);
        navMeshAgent.avoidancePriority = Mathf.Clamp(Mathf.RoundToInt(agentAvoidancePriority), 0, 99);
        navMeshAgent.autoBraking = true;
        navMeshAgent.updateRotation = false;
        navMeshAgent.updatePosition = true;
        navMeshAgent.stoppingDistance = formationStopDistance;
    }

    private void TryPlaceAgentOnNavMesh()
    {
        if (!useNavMeshAgent || navMeshAgent == null)
        {
            usingNavMesh = false;
            RestoreCharacterControllerForFallback();
            return;
        }

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, Mathf.Max(0.1f, navMeshSampleRadius), navMeshAreaMask))
        {
            navMeshAgent.enabled = false;
            usingNavMesh = false;
            navMeshState = "No NavMesh Nearby - Fallback";
            RestoreCharacterControllerForFallback();
            return;
        }
        if (navMeshAgent.enabled)
            navMeshAgent.enabled = false;

        transform.position = hit.position;
        navMeshAgent.enabled = true;

        navMeshAgent.speed = moveSpeed;
        navMeshAgent.angularSpeed = agentAngularSpeed;
        navMeshAgent.acceleration = agentAcceleration;
        navMeshAgent.radius = Mathf.Max(0.01f, agentRadius);
        navMeshAgent.height = Mathf.Max(0.1f, agentHeight);
        navMeshAgent.avoidancePriority = Mathf.Clamp(Mathf.RoundToInt(agentAvoidancePriority), 0, 99);
        navMeshAgent.autoBraking = true;
        navMeshAgent.updateRotation = false;
        navMeshAgent.updatePosition = true;

        usingNavMesh = navMeshAgent.isOnNavMesh;
        navMeshState = usingNavMesh ? "Using NavMeshAgent" : "Fallback CharacterController";

        if (usingNavMesh && characterController != null && disableCharacterControllerWhenUsingAgent)
            characterController.enabled = false;
        else if (!usingNavMesh)
            RestoreCharacterControllerForFallback();
    }


    private void RestoreCharacterControllerForFallback()
    {
        if (characterController != null && !characterController.enabled)
            characterController.enabled = true;
    }

    private void EnsureNavMeshAgentState()
    {
        if (!useNavMeshAgent || navMeshAgent == null)
        {
            usingNavMesh = false;
            RestoreCharacterControllerForFallback();
            return;
        }

        if (!navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            TryPlaceAgentOnNavMesh();
            return;
        }

        usingNavMesh = true;
        navMeshAgent.speed = moveSpeed;
        navMeshAgent.angularSpeed = agentAngularSpeed;
        navMeshAgent.acceleration = agentAcceleration;
    }

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

        if (IsPatternAware() && avoidDangerZones && TryMoveToSafePosition("Self Danger"))
            return;

        if (!inCombat)
        {
            FollowPlayerFormation();
            currentState = "Follow Player";
            RaidMetricsEvents.ReportNPCAction(this, "FollowPlayer");
            return;
        }

        if (role == NPCRole.Healer)
            ThinkHealer();
        else if (role == NPCRole.Tank)
            ThinkFighter(meleeDistance, true);
        else
            ThinkFighter(rangedDistance, false);
    }

    private void ThinkFighter(float desiredDistance, bool tank)
    {
        if (boss == null)
        {
            FollowPlayerFormation();
            currentState = "No Boss: Follow Player";
            RaidMetricsEvents.ReportNPCAction(this, "FollowPlayer");
            return;
        }

        if (IsBossFlyingAndUntargetable())
        {
            FollowPlayerFormation();
            currentState = tank ? "Tank Wait: Boss Flying" : "DPS Wait: Boss Flying";
            RaidMetricsEvents.ReportNPCAction(this, "RegroupDuringBossFly");
            return;
        }

        Vector3 bossPosition = boss.transform.position;
        float distance = FlatDistance(transform.position, bossPosition);

        if (distance > desiredDistance)
        {
            MoveToCombatRange(bossPosition, desiredDistance);
            currentState = tank ? "Tank MoveToBoss" : "DPS MoveToBoss";
            RaidMetricsEvents.ReportNPCAction(this, "MoveToBoss");
            return;
        }

        if (!tank && distance < keepDistanceFromBoss)
        {
            MoveAwayFrom(bossPosition, keepDistanceFromBoss);
            currentState = "DPS KeepDistance";
            RaidMetricsEvents.ReportNPCAction(this, "KeepDistance");
            return;
        }

        FacePosition(bossPosition);
        currentState = tank ? "Tank Attack" : "DPS Attack";
        RaidMetricsEvents.ReportNPCAction(this, "AttackBoss");

        if (Time.time < nextActionTime)
            return;

        nextActionTime = Time.time + Mathf.Max(0.1f, actionInterval);

        bool usedSkill = false;
        if (Time.time >= nextSkillTime && skillController != null)
        {
            nextSkillTime = Time.time + Mathf.Max(0.2f, skillInterval);

            if (useUltimateWhenReady)
                usedSkill = skillController.TryUseUltimate();

            if (!usedSkill)
                usedSkill = skillController.TryUseSkill1();

            if (!usedSkill)
                usedSkill = skillController.TryUseSkill2();
        }

        if (!usedSkill && basicAttack != null)
            basicAttack.TryBasicAttack();
    }

    private void ThinkHealer()
    {
        if (IsPatternAware() && healerProtectDangerAlly)
        {
            PlayerStatus dangerAlly = FindMostThreatenedAlly(out DangerZoneHandle dangerZone);
            if (dangerAlly != null)
            {
                bool moved = MoveNearAllyIfNeeded(dangerAlly);
                if (moved)
                {
                    currentState = $"Healer MoveToDangerAlly {dangerAlly.name}";
                    RaidMetricsEvents.ReportNPCAction(this, "MoveToAllyAndHeal");
                    return;
                }

                bool usedSupport = false;
                float ratio = GetHealthRatio(dangerAlly);
                if (ratio <= healerDangerShieldHpThreshold)
                    usedSupport = TryUseHealerSkill2();

                if (!usedSupport && ratio <= healerLowHpThreshold)
                    usedSupport = TryUseHealerSkill1();

                if (usedSupport)
                {
                    currentState = $"Healer ProtectDangerAlly {dangerAlly.name}";
                    RaidMetricsEvents.ReportNPCAction(this, "ShieldDangerAlly");
                    return;
                }
            }
        }

        PlayerStatus lowAlly = FindLowestHpAlly(out float lowRatio);

        if (lowAlly != null && lowRatio <= healerLowHpThreshold)
        {
            bool movedToAlly = MoveNearAllyIfNeeded(lowAlly);
            bool usedHeal = !movedToAlly && TryUseHealerSkill1();

            if (movedToAlly || usedHeal)
            {
                currentState = $"Healer Heal {lowAlly.name}";
                RaidMetricsEvents.ReportNPCAction(this, "MoveToAllyAndHeal");
                return;
            }
        }

        if (lowAlly != null && lowRatio <= healerShieldThreshold)
        {
            bool movedToAlly = MoveNearAllyIfNeeded(lowAlly);
            bool usedShield = !movedToAlly && TryUseHealerSkill2();

            if (movedToAlly || usedShield)
            {
                currentState = $"Healer Shield {lowAlly.name}";
                RaidMetricsEvents.ReportNPCAction(this, "ShieldParty");
                return;
            }
        }

        if (IsBossFlyingAndUntargetable())
        {
            FollowPlayerFormation();
            currentState = "Healer Support Only: Boss Flying";
            RaidMetricsEvents.ReportNPCAction(this, "RegroupDuringBossFly");
            return;
        }

        if (boss != null)
        {
            ThinkFighter(rangedDistance, false);
            currentState = "Healer Attack Boss";
            RaidMetricsEvents.ReportNPCAction(this, "AttackBoss");
            return;
        }

        FollowPlayerFormation();
        currentState = "Healer Follow Player";
        RaidMetricsEvents.ReportNPCAction(this, "FollowPlayer");
    }

    private bool IsPatternAware()
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
        currentState = reason + ": MoveToSafePosition";
        RaidMetricsEvents.ReportNPCAction(this, "MoveToSafePosition");

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
                if (usingNavMesh && navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
                {
                    if (!TryGetReachableNavDestination(candidate, out navDestination))
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

    private PlayerStatus FindMostThreatenedAlly(out DangerZoneHandle zone)
    {
        zone = null;
        PlayerStatus[] statuses = FindObjectsByType<PlayerStatus>();
        PlayerStatus best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < statuses.Length; i++)
        {
            PlayerStatus ally = statuses[i];
            if (ally == null || ally.Health == null || ally.Health.IsDead)
                continue;

            if (ally.GetComponent<BossDummyController>() != null)
                continue;

            DangerZoneHandle containingZone = DangerZoneRegistry.FindFirstZoneContaining(ally.transform.position, dangerCheckPadding);
            if (containingZone == null)
                continue;

            float hpRatio = GetHealthRatio(ally);
            float distance = FlatDistance(transform.position, ally.transform.position);
            float zoneUrgency = containingZone.TimeRemaining >= 0f ? Mathf.Clamp01(1f - containingZone.TimeRemaining / 2f) : 0.4f;
            float score = (1f - hpRatio) * 2f + zoneUrgency - distance * 0.03f;

            if (score > bestScore)
            {
                bestScore = score;
                best = ally;
                zone = containingZone;
            }
        }

        return best;
    }

    private float GetHealthRatio(PlayerStatus target)
    {
        if (target == null || target.Health == null || target.Health.MaxHealth <= 0f)
            return 1f;

        return Mathf.Clamp01(target.Health.CurrentHealth / target.Health.MaxHealth);
    }

    private bool TryUseHealerSkill1()
    {
        if (Time.time < nextActionTime)
            return false;

        nextActionTime = Time.time + Mathf.Max(0.1f, actionInterval);
        return skillController != null && skillController.TryUseSkill1();
    }

    private bool TryUseHealerSkill2()
    {
        if (Time.time < nextActionTime)
            return false;

        nextActionTime = Time.time + Mathf.Max(0.1f, actionInterval);
        return skillController != null && skillController.TryUseSkill2();
    }

    private PlayerStatus FindLowestHpAlly(out float lowestRatio)
    {
        lowestRatio = 1f;
        PlayerStatus lowest = null;

        PlayerStatus[] statuses = FindObjectsByType<PlayerStatus>();
        for (int i = 0; i < statuses.Length; i++)
        {
            PlayerStatus ally = statuses[i];
            if (ally == null || ally.Health == null || ally.Health.IsDead)
                continue;

            if (ally.GetComponent<BossDummyController>() != null)
                continue;

            float ratio = ally.Health.MaxHealth <= 0f ? 1f : ally.Health.CurrentHealth / ally.Health.MaxHealth;
            if (ratio < lowestRatio)
            {
                lowestRatio = ratio;
                lowest = ally;
            }
        }

        return lowest;
    }

    private bool MoveNearAllyIfNeeded(PlayerStatus ally)
    {
        if (ally == null)
            return false;

        float distance = FlatDistance(transform.position, ally.transform.position);
        if (distance > healerFollowDistance)
        {
            MoveToward(ally.transform.position, Mathf.Min(1.0f, healerFollowDistance * 0.25f));
            return true;
        }

        ClearMovement();
        return false;
    }

    private void FollowPlayerFormation()
    {
        if (playerTarget == null)
            return;

        Vector3 targetPosition = playerTarget.position + playerTarget.rotation * formationOffset;
        float distance = FlatDistance(transform.position, targetPosition);

        if (distance > formationStopDistance)
            MoveToward(targetPosition, formationStopDistance);
        else
        {
            ClearMovement();
            if (faceSameDirectionAsPlayerWhenIdle)
                RotateToward(playerTarget.forward);
        }
    }


    private void MoveToCombatRange(Vector3 targetPosition, float desiredDistance)
    {
        Vector3 awayFromTarget = transform.position - targetPosition;
        awayFromTarget.y = 0f;
        if (awayFromTarget.sqrMagnitude < 0.0001f)
            awayFromTarget = -transform.forward;

        awayFromTarget.Normalize();
        Vector3 desiredPosition = targetPosition + awayFromTarget * Mathf.Max(0.1f, desiredDistance);
        MoveToward(desiredPosition, 0.2f);
    }

    private void MoveToward(Vector3 position, float stoppingDistance = 0.1f)
    {
        Vector3 direction = position - transform.position;
        direction.y = 0f;
        desiredMoveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        desiredDestination = position;
        currentStoppingDistance = Mathf.Max(0.05f, stoppingDistance);
        hasDestination = true;
    }

    private void MoveAwayFrom(Vector3 position, float desiredSeparation)
    {
        Vector3 direction = transform.position - position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;

        direction.Normalize();
        Vector3 destination = position + direction * Mathf.Max(desiredSeparation, keepDistanceFromBoss);
        desiredMoveDirection = direction;
        desiredDestination = destination;
        currentStoppingDistance = 0.15f;
        hasDestination = true;
    }

    private void ClearMovement()
    {
        desiredMoveDirection = Vector3.zero;
        hasDestination = false;
    }

    private void MoveAndRotate()
    {
        if (usingNavMesh && navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            MoveWithNavMeshAgent();
        else
            MoveWithCharacterControllerFallback();
    }

    private void MoveWithNavMeshAgent()
    {
        if (hasDestination)
        {
            Vector3 destination;
            bool foundDestination = TryGetReachableNavDestination(desiredDestination, out destination);

            if (!foundDestination && playerTarget != null)
                foundDestination = TryFindAlternateFormationDestination(out destination);

            if (foundDestination)
            {
                navMeshAgent.stoppingDistance = Mathf.Max(0.05f, currentStoppingDistance);
                navMeshAgent.isStopped = false;

                if (!navMeshAgent.hasPath || (navMeshAgent.destination - destination).sqrMagnitude > 0.04f)
                    navMeshAgent.SetDestination(destination);
            }
            else
            {
                StopAgent();
            }
        }
        else
        {
            StopAgent();
        }

        bool moving = IsAgentMoving();
        Vector3 velocity = navMeshAgent.velocity;
        velocity.y = 0f;

        if (hasDestination && moving)
            DetectAndRecoverAgentStuck();
        else
        {
            stuckTimer = 0f;
            lastAgentPosition = transform.position;
        }

        if (moving && velocity.sqrMagnitude > 0.01f)
            RotateToward(velocity.normalized);

        UpdateMotionAnimation(moving);
    }

    private bool TryGetReachableNavDestination(Vector3 requestedDestination, out Vector3 destination)
    {
        destination = requestedDestination;

        if (!NavMesh.SamplePosition(requestedDestination, out NavMeshHit hit, Mathf.Max(0.1f, destinationSampleRadius), navMeshAreaMask))
            return false;

        destination = hit.position;

        if (!validateNavMeshPath || navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
            return true;

        EnsureReusablePathCreated();
        if (reusablePath == null)
            return false;

        bool hasPath = false;
        try
        {
            hasPath = navMeshAgent.CalculatePath(destination, reusablePath);
        }
        catch (System.Exception exception)
        {
            navMeshState = "CalculatePath failed: " + exception.GetType().Name;
            return false;
        }

        return hasPath && reusablePath.status == NavMeshPathStatus.PathComplete;
    }

    private bool TryFindAlternateFormationDestination(out Vector3 destination)
    {
        destination = desiredDestination;

        if (playerTarget == null)
            return false;

        float radius = Mathf.Max(0.5f, alternateFormationRadius);
        int samples = Mathf.Max(4, alternateFormationSamples);

        for (int i = 0; i < samples; i++)
        {
            float angle = (Mathf.PI * 2f) * i / samples;
            Vector3 localOffset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Vector3 candidate = playerTarget.position + playerTarget.TransformDirection(localOffset);

            if (TryGetReachableNavDestination(candidate, out destination))
                return true;
        }

        return TryGetReachableNavDestination(playerTarget.position, out destination);
    }

    private void DetectAndRecoverAgentStuck()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
            return;

        float movedSqr = (transform.position - lastAgentPosition).sqrMagnitude;
        bool shouldBeMoving = hasDestination && navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance + 0.35f;

        if (shouldBeMoving && movedSqr < 0.0009f && navMeshAgent.velocity.sqrMagnitude < 0.01f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= Mathf.Max(0.2f, stuckRepathTime))
            {
                stuckTimer = 0f;
                if (TryGetReachableNavDestination(desiredDestination, out Vector3 repathDestination))
                    navMeshAgent.SetDestination(repathDestination);
            }
        }
        else
        {
            stuckTimer = 0f;
            lastAgentPosition = transform.position;
        }
    }

    private void MoveWithCharacterControllerFallback()
    {
        bool moving = desiredMoveDirection.sqrMagnitude > 0.0001f;
        Vector3 move = moving ? desiredMoveDirection * moveSpeed : Vector3.zero;

        if (characterController != null && characterController.enabled)
        {
            if (characterController.isGrounded && verticalVelocity < 0f)
                verticalVelocity = groundedStickVelocity;
            else
                verticalVelocity += gravity * Time.deltaTime;

            move.y = verticalVelocity;
            characterController.Move(move * Time.deltaTime);
        }
        else
        {
            transform.position += move * Time.deltaTime;
        }

        if (moving)
            RotateToward(desiredMoveDirection);

        UpdateMotionAnimation(moving);
    }

    private bool IsAgentMoving()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
            return false;

        if (navMeshAgent.pathPending)
            return true;

        if (navMeshAgent.velocity.sqrMagnitude > 0.02f)
            return true;

        if (hasDestination && navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance + 0.05f)
            return true;

        return false;
    }

    private void StopAgent()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
            return;

        navMeshAgent.isStopped = true;
        navMeshAgent.ResetPath();
    }

    private void ApplyGravityOnly()
    {
        if (usingNavMesh)
            return;

        if (characterController == null || !characterController.enabled)
            return;

        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = groundedStickVelocity;
        else
            verticalVelocity += gravity * Time.deltaTime;

        characterController.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

    private bool IsActionBusy()
    {
        if (basicAttack != null && basicAttack.IsAttacking)
            return true;

        if (skillController != null && skillController.IsSkillInProgress)
            return true;

        return false;
    }

    private void RotateToward(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void FacePosition(Vector3 position)
    {
        Vector3 direction = position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void UpdateMotionAnimation(bool moving)
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

        if (motionGraph.IsValid() && currentMotionClip == clip && motionPlayable.IsValid())
            return;

        CreateMotionGraphIfNeeded();

        if (motionPlayable.IsValid())
            motionPlayable.Destroy();

        currentMotionClip = clip;
        motionPlayable = AnimationClipPlayable.Create(motionGraph, clip);
        motionPlayable.SetApplyFootIK(false);
        motionPlayable.SetApplyPlayableIK(false);
        motionPlayable.SetTime(0d);
        motionPlayable.SetSpeed(Mathf.Max(0.01f, speed));
        motionPlayable.SetDone(false);

        motionOutput.SetSourcePlayable(motionPlayable);
        motionGraph.Play();
    }

    private void CreateMotionGraphIfNeeded()
    {
        if (motionGraph.IsValid())
            return;

        motionGraph = PlayableGraph.Create(name + "_NPCMotionGraph");
        motionGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        motionOutput = AnimationPlayableOutput.Create(motionGraph, "NPCMotion", animator);
    }

    private void StopDirectMotionClip()
    {
        currentMotionClip = null;

        if (motionPlayable.IsValid())
            motionPlayable.Destroy();

        if (motionGraph.IsValid())
            motionGraph.Stop();
    }

    private void KeepMotionClipLoopingIfNeeded()
    {
        if (!motionPlayable.IsValid() || currentMotionClip == null)
            return;

        double length = Mathf.Max(0.01f, currentMotionClip.length);
        if (motionPlayable.GetTime() >= length)
            motionPlayable.SetTime(0d);
    }

    private void TryPlayAnimatorState(string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        int hash = Animator.StringToHash(stateName);
        string baseLayerState = "Base Layer." + stateName;
        int baseHash = Animator.StringToHash(baseLayerState);

        if (animator.HasState(0, hash))
            animator.CrossFadeInFixedTime(stateName, animationCrossFade, 0, 0f);
        else if (animator.HasState(0, baseHash))
            animator.CrossFadeInFixedTime(baseLayerState, animationCrossFade, 0, 0f);
    }

    private bool IsBossFlyingAndUntargetable()
    {
        return blockOffensiveActionsWhileBossFlying && bossSkillPattern != null && bossSkillPattern.IsFlying;
    }

    private float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void OnDisable()
    {
        StopAgent();
        StopDirectMotionClip();
    }

    private void OnDestroy()
    {
        if (motionGraph.IsValid())
            motionGraph.Destroy();
    }
}
