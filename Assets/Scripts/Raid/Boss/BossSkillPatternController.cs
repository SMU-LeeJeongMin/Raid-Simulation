// 보스 패턴
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BossPatternShape
{
    Circle,
    Line,
    Cone
}

public enum BossPatternVFXTiming
{
    None,
    WarningStart,
    Impact
}

[RequireComponent(typeof(Health))]
public class BossSkillPatternController : MonoBehaviour
{
    [Serializable]
    public class GroundPattern
    {
        [Header("Basic")]
        public string displayName;
        public float weight = 1f;
        public bool rarePattern = false;

        [Header("Animation")]
        public AnimationClip animationClip;
        public string animatorStateName;
        [Min(0f)] public float durationOverride = 0f;
        [Range(0f, 1f)] public float impactNormalizedTime = 0.55f;

        [Header("Timing")]
        [Min(0f)] public float warningTime = 1.2f;
        [Min(0f)] public float cooldownAfter = 1.5f;
        public bool autoStretchAnimationToWarning = true;
        [Min(0f)] public float postImpactRecovery = 0.35f;

        [Header("Damage")]
        [Min(0f)] public float damage = 25f;

        [Header("Telegraph")]
        public BossPatternShape shape = BossPatternShape.Circle;
        public Transform originPoint;
        public Vector3 localOffset = Vector3.zero;
        public float radius = 2f;
        public float length = 8f;
        public float width = 2f;
        public float angle = 60f;
        public bool aimAtPlayer = true;
        public bool followPlayerDuringWarning = false;

        [Header("Multi Spawn - Attack9")]
        public int spawnCount = 4;
        public float spawnRadiusAroundPlayer = 5f;
        public float spawnCircleRadius = 1.8f;

        [Header("Projectile VFX - Attack7/8 Optional")]
        public GameObject projectileVFXPrefab;
        public float projectileSpeed = 5f;
        public float projectileLifetime = 5f;
        public float projectileHitRadius = 0.8f;
        public Vector3 projectileOffset = new Vector3(0f, 1f, 0f);
        public Vector3 projectileEuler = Vector3.zero;
        public Vector3 projectileScale = Vector3.one;

        [Header("Pattern VFX")]
        public BossPatternVFXTiming patternVFXTiming = BossPatternVFXTiming.Impact;
        public GameObject patternVFXPrefab;
        public Vector3 patternVFXOffset = Vector3.up;
        public Vector3 patternVFXEuler = Vector3.zero;
        public Vector3 patternVFXScale = Vector3.one;
        public float patternVFXDestroyDelay = 3f;

        [Header("Fire Breath Charge VFX - Attack10")]
        public GameObject fireBreathChargeVFXPrefab;
        public bool attachFireBreathChargeVFXToOrigin = true;
        public Vector3 fireBreathChargeVFXOffset = new Vector3(0f, 1.2f, 0.8f);
        public Vector3 fireBreathChargeVFXEuler = Vector3.zero;
        public Vector3 fireBreathChargeVFXScale = Vector3.one;
        [Min(0.05f)] public float fireBreathChargeVFXDestroyDelay = 2f;

        [Header("Fire Breath Sweep - Attack10")]
        public bool fireBreathTelegraphBeforeAnimation = true;
        [Min(0.1f)] public float fireBreathActiveDuration = 1.8f;
        [Min(0.05f)] public float fireBreathTickInterval = 0.25f;
        [Min(0f)] public float fireBreathSweepAngle = 0f;
        [Min(0.1f)] public float fireBreathSweepCycles = 3f;
        [Min(1f)] public float fireBreathDamageAngle = 18f;
        public bool attachFireBreathVFXToOrigin = false;
    }

    [Serializable]
    public class FlyPhaseSettings
    {
        [Header("Animation")]
        public AnimationClip buffClip;
        public AnimationClip flyUpClip;
        public AnimationClip flyClip;
        public AnimationClip landClip;

        [Header("Animator State Fallback")]
        public string buffStateName = "Buff";
        public string flyUpStateName = "FlyUp";
        public string flyStateName = "Fly";
        public string landStateName = "FlyDown";

        [Min(0.1f)] public float buffDuration = 3f;
        [Min(0.1f)] public float flyUpDuration = 2f;
        [Min(0.1f)] public float flyDuration = 3f;
        [Min(0.1f)] public float landDuration = 2f;

        [Header("Fly Height / Targeting")]
        public bool moveBossUpDuringFly = true;
        public float flyHeight = 7f;
        public bool makeUntargetableWhileFlying = true;
        public string untargetableLayerName = "Ignore Raycast";
        [Min(1)] public int airPatternRepeats = 2;

        [Header("Fly VFX")]
        public GameObject buffVFXPrefab;
        public GameObject flyUpVFXPrefab;
        public GameObject landVFXPrefab;
        public Vector3 bossVFXOffset = new Vector3(0f, 1.5f, 0f);
        public Vector3 bossVFXEuler = Vector3.zero;
        public Vector3 bossVFXScale = Vector3.one;
        public float bossVFXDestroyDelay = 3f;

        [Header("Fly - Random Lightning Strikes")]
        public int randomLightningCount = 5;
        [Min(1)] public int randomLightningWaves = 4;
        [Min(1)] public int randomLightningPerWave = 6;
        [Min(0f)] public float randomLightningMinSeparation = 1.3f;
        [Min(0f)] public float randomLightningWaveInterval = 0.18f;
        public float randomLightningSpreadRadius = 8f;
        public float randomLightningWarningTime = 1f;
        public float randomLightningDamage = 18f;
        public float randomLightningRadius = 1.6f;
        public float randomLightningInterval = 0.35f;
        public GameObject randomLightningVFXPrefab;

        [Header("Fly - DOT Lightning Zones")]
        public int dotZoneCount = 3;
        [Min(1)] public int dotZoneWaves = 3;
        [Min(1)] public int dotZonesPerWave = 5;
        [Min(0f)] public float dotZoneMinSeparation = 2.0f;
        public float dotZoneSpreadRadius = 7f;
        public float dotZoneRadius = 2f;
        public float dotZoneWarningTime = 0.9f;
        public float dotZoneDuration = 3f;
        public float dotZoneTickInterval = 0.5f;
        public float dotZoneTickDamage = 8f;
        public float dotZoneInterval = 0.25f;
        public GameObject dotZoneVFXPrefab;

        [Header("Fly - Tracking Lightning")]
        public float trackingDuration = 3f;
        public float trackingRadius = 1.8f;
        public float trackingDamage = 20f;
        public float trackingImpactDuration = 3f;
        public float trackingTickInterval = 0.5f;
        public float trackingTickDamage = 10f;
        public GameObject trackingWarningVFXPrefab;
        public GameObject trackingImpactVFXPrefab;

        [Header("Lightning VFX Common")]
        public Vector3 lightningVFXOffset = new Vector3(0f, 0.15f, 0f);
        public Vector3 lightningVFXEuler = Vector3.zero;
        public Vector3 lightningVFXScale = Vector3.one;
        public float lightningVFXDestroyDelay = 4f;
    }

    [Header("References")]
    public Health health;
    public Animator animator;
    public PlayerStatus targetPlayer;
    public BossBasicPatternController basicPatternController;
    public RaidCameraFollow raidCamera;

    [Header("Engage")]
    public bool waitForRaidCameraMode = true;
    public bool autoFindRaidCamera = true;
    public bool stayEngagedAfterStart = true;
    public float fallbackEngageDistance = 12f;

    [Header("Phase")]
    [Range(0.05f, 0.95f)] public float phase2HealthRatio = 0.5f;
    public bool runPhase2OnlyOnce = true;

    [Header("Phase 1 Pattern Timing")]
    public float firstPatternDelay = 1.5f;
    public Vector2 patternCooldownRange = new Vector2(1.2f, 2.2f);
    public float postPatternDelay = 1.5f;
    [Range(0f, 1f)] public float fireBreathChance = 0.25f;
    public bool waitForBasicAttackToFinish = true;
    [Min(0)] public int forceFireBreathAfterPhase1Patterns = 3;

    [Header("Phase 1 Patterns")]
    public GroundPattern attack5LeftWing = new GroundPattern { displayName = "Attack5_LeftWing", animatorStateName = "Attack5", shape = BossPatternShape.Circle, radius = 2.4f, damage = 35f, warningTime = 1.2f };
    public GroundPattern attack6RightWing = new GroundPattern { displayName = "Attack6_RightWing", animatorStateName = "Attack6", shape = BossPatternShape.Circle, radius = 2.4f, damage = 35f, warningTime = 1.2f };
    public GroundPattern attack7LeftOrb = new GroundPattern { displayName = "Attack7_LeftOrb", animatorStateName = "Attack7", shape = BossPatternShape.Line, length = 12f, width = 1.1f, damage = 30f, warningTime = 1f, projectileSpeed = 4f };
    public GroundPattern attack8RightOrb = new GroundPattern { displayName = "Attack8_RightOrb", animatorStateName = "Attack8", shape = BossPatternShape.Line, length = 12f, width = 1.1f, damage = 30f, warningTime = 1f, projectileSpeed = 4f };
    public GroundPattern attack9RoarBombs = new GroundPattern { displayName = "Attack9_RoarBombs", animatorStateName = "Attack9", shape = BossPatternShape.Circle, spawnCount = 5, spawnRadiusAroundPlayer = 7f, spawnCircleRadius = 1.8f, damage = 35f, warningTime = 1.4f, patternVFXTiming = BossPatternVFXTiming.Impact };
    public GroundPattern attack10FireBreath = new GroundPattern { displayName = "Attack10_FireBreath", animatorStateName = "Attack10", shape = BossPatternShape.Cone, length = 8f, angle = 55f, damage = 12f, warningTime = 1.2f, weight = 0.25f, rarePattern = true, fireBreathActiveDuration = 1.8f, fireBreathTickInterval = 0.25f, fireBreathDamageAngle = 18f, fireBreathTelegraphBeforeAnimation = true };

    [Header("Phase 2")]
    public FlyPhaseSettings flyPhase = new FlyPhaseSettings();

    [Header("Player Hit VFX")]
    public GameObject playerHitVFXPrefab;
    public string playerHitVFXAnchorName = "VFXHitAnchor";
    public Vector3 playerHitVFXOffset = new Vector3(0f, 0.7f, 0f);
    public Vector3 playerHitVFXEuler = Vector3.zero;
    public Vector3 playerHitVFXScale = Vector3.one;
    public bool attachPlayerHitVFXToTarget = false;
    public float playerHitVFXDestroyDelay = 3f;

    [Header("Telegraph Style")]
    public Color warningColor = new Color(1f, 0f, 0f, 0.45f);
    public Material warningMaterial;
    public float telegraphGroundY = 0.05f;
    public bool usePlayerGroundY = true;

    [Header("Targeting")]
    public bool autoFindPlayer = true;
    public float targetRefreshInterval = 0.5f;
    public LayerMask playerDamageMask = ~0;

    [Header("Animator")]
    public bool useDirectClipPlayback = true;
    public bool disableRootMotion = true;
    public float defaultAnimationDuration = 1.5f;

    [Header("Debug")]
    public bool logPattern = true;
    public bool drawGizmos = true;

    [Header("Runtime Debug")]
    [SerializeField] private bool engaged;
    [SerializeField] private bool inSpecialPattern;
    [SerializeField] private bool inPhase2;
    [SerializeField] private bool flying;
    [SerializeField] private string currentPattern;

    private float nextTargetFindTime;
    private bool phase2Triggered;
    private int phase1PatternsSinceFireBreath;
    private Coroutine patternRoutine;
    private Coroutine phase2PriorityRoutine;
    private bool bossDead;
    private Vector3 groundPositionBeforeFly;
    private readonly Dictionary<GameObject, int> originalLayers = new Dictionary<GameObject, int>();

    // 유효 대상 수집용 재사용 버퍼 (데미지 틱마다 새 리스트 할당 방지)
    private readonly List<PlayerStatus> validTargetBuffer = new List<PlayerStatus>();

    // 페이즈 클립 직접 재생을 담당하는 공용 플레이어
    private SingleClipPlayer clipPlayer;
    private SingleClipPlayer ClipPlayer => clipPlayer ??= new SingleClipPlayer(name + "_BossSkillPatternGraph", "BossSkillPattern");

    public bool IsFlying => flying;
    public bool IsInSpecialPattern => inSpecialPattern;
    public bool IsInPhase2 => inPhase2;

    // ---------- 정책 판단 및 그래프 스냅샷용 상태 노출 ----------

    // 현재 실행 중인 패턴 이름 (없으면 빈 문자열)
    public string CurrentPatternName => currentPattern ?? string.Empty;

    // 추적 낙뢰 진행 여부 (산개 행동 판단용)
    public bool IsTrackingLightningActive { get; private set; }
    public bool IsEngaged => engaged;

    private void Awake()
    {
        CombatRegistry.Register(this);

        if (health == null)
            health = GetComponent<Health>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator != null && disableRootMotion)
            animator.applyRootMotion = false;

        if (basicPatternController == null)
            basicPatternController = GetComponent<BossBasicPatternController>();

        if (autoFindRaidCamera && raidCamera == null)
            raidCamera = FindAnyObjectByType<RaidCameraFollow>();
    }

    private void OnEnable()
    {
        if (health == null)
            health = GetComponent<Health>();

        bossDead = health != null && health.IsDead;

        if (health != null)
        {
            health.onHealthChanged.RemoveListener(OnHealthChanged);
            health.onDeath.RemoveListener(OnBossDeath);
            health.onHealthChanged.AddListener(OnHealthChanged);
            health.onDeath.AddListener(OnBossDeath);
        }

        if (bossDead)
            return;

        if (patternRoutine != null)
            StopCoroutine(patternRoutine);
        patternRoutine = StartCoroutine(PatternLoop(false));
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.onHealthChanged.RemoveListener(OnHealthChanged);
            health.onDeath.RemoveListener(OnBossDeath);
        }

        if (patternRoutine != null)
        {
            StopCoroutine(patternRoutine);
            patternRoutine = null;
        }

        if (phase2PriorityRoutine != null)
        {
            StopCoroutine(phase2PriorityRoutine);
            phase2PriorityRoutine = null;
        }

        StopClip();
        SetUntargetable(false);

        if (!bossDead && (health == null || !health.IsDead))
            SetBasicPatternEnabled(true);
        else
            LockAndStopBasicPattern();
    }

    private void Update()
    {
        TryFindPlayerIfNeeded();
        UpdateEngageState();
        KeepLoopingPhaseClipIfNeeded();
    }

    private void OnHealthChanged(Health changedHealth, float current, float max, float normalized)
    {
        if (bossDead || phase2Triggered || changedHealth == null || changedHealth.IsDead)
            return;

        if (normalized <= phase2HealthRatio)
            RequestImmediatePhase2();
    }

    private void OnBossDeath(Health deadHealth)
    {
        bossDead = true;
        phase2Triggered = true;

        if (patternRoutine != null)
        {
            StopCoroutine(patternRoutine);
            patternRoutine = null;
        }

        if (phase2PriorityRoutine != null)
        {
            StopCoroutine(phase2PriorityRoutine);
            phase2PriorityRoutine = null;
        }

        StopClip();
        DestroyActiveTelegraphs();
        SetUntargetable(false);
        LockAndStopBasicPattern();

        inSpecialPattern = false;
        inPhase2 = false;
        flying = false;
        currentPattern = string.Empty;
        IsTrackingLightningActive = false;

        enabled = false;
    }

    private void RequestImmediatePhase2()
    {
        if (bossDead || phase2Triggered || health == null || health.IsDead || !isActiveAndEnabled)
            return;

        phase2Triggered = true;

        if (patternRoutine != null)
        {
            StopCoroutine(patternRoutine);
            patternRoutine = null;
        }

        if (phase2PriorityRoutine != null)
            StopCoroutine(phase2PriorityRoutine);

        phase2PriorityRoutine = StartCoroutine(Phase2PriorityRoutine());
    }

    private IEnumerator Phase2PriorityRoutine()
    {
        CancelCurrentPatternForPriority();
        yield return RunPhase2Cycle();

        phase2PriorityRoutine = null;

        if (!bossDead && health != null && !health.IsDead && isActiveAndEnabled)
            patternRoutine = StartCoroutine(PatternLoop(true));
    }

    private IEnumerator RunPhase2PriorityInline()
    {
        CancelCurrentPatternForPriority();
        yield return RunPhase2Cycle();
    }

    private void CancelCurrentPatternForPriority()
    {
        StopClip();
        DestroyActiveTelegraphs();
        SetUntargetable(false);
        LockAndStopBasicPattern();

        inSpecialPattern = false;
        currentPattern = string.Empty;
    }

    private void LockAndStopBasicPattern()
    {
        if (basicPatternController == null)
            return;

        basicPatternController.SetSpecialPatternLock(true);
        basicPatternController.ForceStopCurrentAction(false);
    }

    private void DestroyActiveTelegraphs()
    {
        BossTelegraphArea[] telegraphs = FindObjectsByType<BossTelegraphArea>();
        for (int i = 0; i < telegraphs.Length; i++)
        {
            if (telegraphs[i] != null)
                Destroy(telegraphs[i].gameObject);
        }
    }

    private IEnumerator PatternLoop(bool skipFirstPatternDelay = false)
    {
        yield return new WaitForSeconds(0.5f);

        bool firstPatternWaitDone = skipFirstPatternDelay;
        while (enabled)
        {
            if (health != null && health.IsDead)
                yield break;

            if (!engaged || targetPlayer == null)
            {
                yield return null;
                continue;
            }

            if (!firstPatternWaitDone)
            {
                firstPatternWaitDone = true;
                if (firstPatternDelay > 0f)
                    yield return new WaitForSeconds(firstPatternDelay);
            }

            if (!phase2Triggered && health != null && GetHealthRatio() <= phase2HealthRatio)
            {
                phase2Triggered = true;
                yield return RunPhase2PriorityInline();
                yield return new WaitForSeconds(postPatternDelay + UnityEngine.Random.Range(patternCooldownRange.x, patternCooldownRange.y));
                continue;
            }

            yield return WaitForBasicAttackToFinish();
            GroundPattern pattern = ChoosePhase1Pattern();
            if (pattern != null)
                yield return ExecuteGroundPattern(pattern);

            yield return new WaitForSeconds(postPatternDelay + UnityEngine.Random.Range(patternCooldownRange.x, patternCooldownRange.y));
        }
    }

    private void UpdateEngageState()
    {
        if (engaged && stayEngagedAfterStart)
            return;

        bool shouldEngage = false;
        if (waitForRaidCameraMode && raidCamera != null)
        {
            shouldEngage = IsRaidCameraBossModeActive();
        }
        else if (targetPlayer != null)
        {
            Vector3 delta = targetPlayer.transform.position - transform.position;
            delta.y = 0f;
            shouldEngage = delta.magnitude <= fallbackEngageDistance;
        }
        engaged = shouldEngage;
    }

    private bool IsRaidCameraBossModeActive()
    {
        if (raidCamera == null)
            return false;

        if (raidCamera.useManualBossCameraMode)
            return raidCamera.manualBossCameraMode;

        Transform playerTarget = raidCamera.target != null ? raidCamera.target : targetPlayer != null ? targetPlayer.transform : null;
        Transform bossTarget = raidCamera.bossTarget != null ? raidCamera.bossTarget : transform;
        if (playerTarget == null || bossTarget == null)
            return false;

        Vector3 playerPos = playerTarget.position;
        Vector3 bossPos = bossTarget.position;
        if (raidCamera.useHorizontalDistanceToBoss)
        {
            playerPos.y = 0f;
            bossPos.y = 0f;
        }

        float distance = Vector3.Distance(playerPos, bossPos);
        return raidCamera.enableBossCameraMode && distance <= raidCamera.bossCameraEnterDistance;
    }

    private void TryFindPlayerIfNeeded()
    {
        if (!autoFindPlayer)
            return;

        if (Time.time < nextTargetFindTime && PartyTargetUtility.IsValidPlayerTarget(targetPlayer))
            return;

        nextTargetFindTime = Time.time + Mathf.Max(0.05f, targetRefreshInterval);
        targetPlayer = FindClosestPlayerTarget(transform.position);
    }

    private PlayerStatus FindClosestPlayerTarget(Vector3 fromPosition)
    {
        IReadOnlyList<PlayerStatus> players = CombatRegistry.PlayerStatuses;
        float bestDistance = float.PositiveInfinity;
        PlayerStatus best = null;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerStatus player = players[i];
            if (!PartyTargetUtility.IsValidPlayerTarget(player))
                continue;

            Vector3 delta = player.transform.position - fromPosition;
            delta.y = 0f;
            float sqrDistance = delta.sqrMagnitude;
            if (sqrDistance < bestDistance)
            {
                bestDistance = sqrDistance;
                best = player;
            }
        }

        return best;
    }

    private PlayerStatus FindHumanPlayerTarget()
    {
        IReadOnlyList<PlayerStatus> players = CombatRegistry.PlayerStatuses;
        for (int i = 0; i < players.Count; i++)
        {
            PlayerStatus player = players[i];
            if (!PartyTargetUtility.IsValidPlayerTarget(player))
                continue;

            if (player.GetComponent<NPCPartyMember>() == null)
                return player;
        }

        return null;
    }

    private PlayerStatus GetRandomValidPartyTarget()
    {
        PlayerStatus[] players = GetValidPlayerTargets();
        if (players == null || players.Length == 0)
            return null;

        return players[UnityEngine.Random.Range(0, players.Length)];
    }

    private float GetHealthRatio()
    {
        if (health == null || health.MaxHealth <= 0f)
            return 1f;
        return Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);
    }

    private GroundPattern ChoosePhase1Pattern()
    {
        if (forceFireBreathAfterPhase1Patterns > 0
            && phase1PatternsSinceFireBreath >= forceFireBreathAfterPhase1Patterns
            && attack10FireBreath != null
            && attack10FireBreath.weight > 0f)
        {
            return attack10FireBreath;
        }

        List<GroundPattern> patterns = new List<GroundPattern>
        {
            attack5LeftWing,
            attack6RightWing,
            attack7LeftOrb,
            attack8RightOrb,
            attack9RoarBombs,
            attack10FireBreath
        };

        float total = 0f;
        for (int i = 0; i < patterns.Count; i++)
        {
            GroundPattern p = patterns[i];
            if (p == null || p.weight <= 0f)
                continue;

            float w = p.weight;
            if (p == attack10FireBreath)
                w *= Mathf.Clamp01(fireBreathChance);
            total += w;
        }

        if (total <= 0f)
            return null;

        float r = UnityEngine.Random.Range(0f, total);
        for (int i = 0; i < patterns.Count; i++)
        {
            GroundPattern p = patterns[i];
            if (p == null || p.weight <= 0f)
                continue;

            float w = p.weight;
            if (p == attack10FireBreath)
                w *= Mathf.Clamp01(fireBreathChance);

            r -= w;
            if (r <= 0f)
                return p;
        }

        return patterns[0];
    }

    private float CalculateImpactDelay(GroundPattern pattern, float baseDuration, float warningTime)
    {
        if (pattern == null)
            return Mathf.Max(0.05f, warningTime);

        float normalized = Mathf.Clamp01(pattern.impactNormalizedTime);
        float animationImpactTime = baseDuration * normalized;
        return Mathf.Max(0.05f, Mathf.Max(warningTime, animationImpactTime));
    }

    private float CalculatePatternAnimationDuration(GroundPattern pattern, float baseDuration, float impactDelay)
    {
        if (pattern == null)
            return Mathf.Max(0.05f, baseDuration);

        float duration = Mathf.Max(0.05f, baseDuration);
        if (pattern.autoStretchAnimationToWarning)
        {
            float normalized = Mathf.Clamp01(pattern.impactNormalizedTime);
            if (normalized > 0.001f)
                duration = Mathf.Max(duration, impactDelay / normalized);
            else
                duration = Mathf.Max(duration, impactDelay);
        }

        return Mathf.Max(duration, impactDelay + Mathf.Max(0f, pattern.postImpactRecovery));
    }

    private IEnumerator ExecuteGroundPattern(GroundPattern pattern)
    {
        if (pattern == null)
            yield break;

        inSpecialPattern = true;
        currentPattern = pattern.displayName;
        SetBasicPatternEnabled(false);

        float baseDuration = ResolveClipDuration(pattern.animationClip, pattern.durationOverride, defaultAnimationDuration);
        float warningTime = Mathf.Max(0.05f, pattern.warningTime);
        float impactDelay = CalculateImpactDelay(pattern, baseDuration, warningTime);
        float duration = CalculatePatternAnimationDuration(pattern, baseDuration, impactDelay);

        FacePlayer();

        if (pattern != attack10FireBreath)
            PlayClipOrState(pattern.animationClip, pattern.animatorStateName, duration);

        if (pattern == attack10FireBreath)
            yield return ExecuteFireBreathPattern(pattern, baseDuration, warningTime);
        else if (pattern == attack9RoarBombs)
            yield return ExecuteRoarBombs(pattern, duration, impactDelay);
        else if (pattern == attack7LeftOrb || pattern == attack8RightOrb)
            yield return ExecuteLineOrb(pattern, duration, impactDelay);
        else
            yield return ExecuteSingleTelegraphPattern(pattern, duration, impactDelay);

        StopClip();
        UpdatePhase1PatternCounters(pattern);

        if (pattern.cooldownAfter > 0f)
            yield return new WaitForSeconds(pattern.cooldownAfter);

        SetBasicPatternEnabled(true);
        inSpecialPattern = false;
        currentPattern = string.Empty;
    }

    private IEnumerator ExecuteSingleTelegraphPattern(GroundPattern pattern, float duration, float warningTime)
    {
        Vector3 origin = GetPatternOrigin(pattern);
        Vector3 forward = GetPatternForward(pattern, origin);
        BossTelegraphArea telegraph = CreateTelegraph(pattern, origin, forward, warningTime);

        if (pattern.patternVFXTiming == BossPatternVFXTiming.WarningStart)
            SpawnPatternVFX(pattern, GetPatternVFXPosition(pattern, origin));

        yield return new WaitForSeconds(warningTime);

        if (telegraph != null)
            Destroy(telegraph.gameObject);

        ApplyPatternDamage(pattern, origin, forward);

        if (pattern.patternVFXTiming == BossPatternVFXTiming.Impact)
            SpawnPatternVFX(pattern, GetPatternVFXPosition(pattern, origin));

        float remaining = Mathf.Max(0f, duration - warningTime);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
    }


    private IEnumerator ExecuteFireBreathPattern(GroundPattern pattern, float baseDuration, float warningTime)
    {
        if (pattern == null)
            yield break;

        FacePlayer();
        Vector3 origin = GetPatternOrigin(pattern);
        Vector3 lockedForward = GetFireBreathForward(pattern);

        BossTelegraphArea telegraph = CreateTelegraph(pattern, origin, lockedForward, warningTime);

        GameObject chargeVFX = null;
        Transform chargeParent = pattern.originPoint != null ? pattern.originPoint : transform;
        if (pattern.fireBreathChargeVFXPrefab != null)
        {
            Vector3 chargePosition = origin + pattern.fireBreathChargeVFXOffset;
            Quaternion chargeRotation = Quaternion.LookRotation(lockedForward, Vector3.up) * Quaternion.Euler(pattern.fireBreathChargeVFXEuler);
            float chargeDestroyDelay = Mathf.Max(pattern.fireBreathChargeVFXDestroyDelay, warningTime + 0.2f);
            chargeVFX = SpawnVFX(pattern.fireBreathChargeVFXPrefab, chargePosition, chargeRotation, pattern.fireBreathChargeVFXScale, chargeDestroyDelay);
            if (chargeVFX != null && pattern.attachFireBreathChargeVFXToOrigin && chargeParent != null)
                chargeVFX.transform.SetParent(chargeParent, true);
        }

        if (warningTime > 0f)
            yield return new WaitForSeconds(warningTime);

        if (telegraph != null)
            Destroy(telegraph.gameObject);

        if (chargeVFX != null)
            Destroy(chargeVFX);

        origin = GetPatternOrigin(pattern);

        float activeDuration = Mathf.Max(0.1f, pattern.fireBreathActiveDuration);
        float animationDuration = Mathf.Max(baseDuration, activeDuration + Mathf.Max(0f, pattern.postImpactRecovery));
        PlayClipOrState(pattern.animationClip, pattern.animatorStateName, animationDuration);

        GameObject fireVFX = null;
        Transform fireParent = pattern.originPoint != null ? pattern.originPoint : transform;
        if (pattern.patternVFXPrefab != null)
        {
            Vector3 spawnPosition = origin + pattern.patternVFXOffset;
            Quaternion spawnRotation = Quaternion.LookRotation(lockedForward, Vector3.up) * Quaternion.Euler(pattern.patternVFXEuler);
            fireVFX = SpawnVFX(pattern.patternVFXPrefab, spawnPosition, spawnRotation, pattern.patternVFXScale, activeDuration + Mathf.Max(0.2f, pattern.patternVFXDestroyDelay));
            if (fireVFX != null && pattern.attachFireBreathVFXToOrigin && fireParent != null)
                fireVFX.transform.SetParent(fireParent, true);
        }

        float sweepAngle = pattern.fireBreathSweepAngle > 0f ? pattern.fireBreathSweepAngle : pattern.angle;
        float halfSweep = sweepAngle * 0.5f;
        float tickTimer = 0f;
        float elapsed = 0f;
        bool didFirstTick = false;

        // 브레스가 실제로 피해를 주는 구간에도 위험 범위를 유지
        // (예고 장판만 등록하면 예고 종료 직후 NPC가 안전하다고 판단해 재진입하는 문제의 원인)
        // 스윕 전체가 지나가는 각도 + 순간 피해 각도를 덮는 부채꼴로 등록
        float breathCoverAngle = Mathf.Clamp(sweepAngle + Mathf.Max(0f, pattern.fireBreathDamageAngle), 1f, 360f);
        DangerZoneHandle breathZone = RegisterTemporaryConeDangerZone(
            "FireBreath_Active", origin, lockedForward, pattern.length, breathCoverAngle,
            activeDuration, 0f, pattern.damage, DangerZoneCategory.ActiveDamage);

        while (elapsed < activeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / activeDuration);
            float sweepProgress = Mathf.PingPong(t * Mathf.Max(0.1f, pattern.fireBreathSweepCycles), 1f);
            float yaw = Mathf.Lerp(-halfSweep, halfSweep, sweepProgress);
            Vector3 currentForward = Quaternion.AngleAxis(yaw, Vector3.up) * lockedForward;
            currentForward.y = 0f;
            if (currentForward.sqrMagnitude < 0.0001f)
                currentForward = lockedForward;
            currentForward.Normalize();

            origin = GetPatternOrigin(pattern);
            if (fireVFX != null)
            {
                fireVFX.transform.position = origin + pattern.patternVFXOffset;
                fireVFX.transform.rotation = Quaternion.LookRotation(currentForward, Vector3.up) * Quaternion.Euler(pattern.patternVFXEuler);
            }

            // 보스가 움직여도 위험 범위가 브레스 시작점을 따라가도록 갱신
            if (breathZone != null)
                breathZone.transform.position = new Vector3(origin.x, GetGroundY(origin), origin.z);

            tickTimer += Time.deltaTime;
            if (!didFirstTick || tickTimer >= Mathf.Max(0.05f, pattern.fireBreathTickInterval))
            {
                didFirstTick = true;
                tickTimer = 0f;
                ApplyConeDamage(origin, currentForward, pattern.length, pattern.fireBreathDamageAngle, pattern.damage);
            }

            yield return null;
        }

        float remaining = Mathf.Max(0f, animationDuration - activeDuration);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
    }

    private IEnumerator ExecuteLineOrb(GroundPattern pattern, float duration, float warningTime)
    {
        Vector3 origin = GetPatternOrigin(pattern);
        Vector3 forward = GetPatternForward(pattern, origin);
        Vector3 center = origin + forward * (pattern.length * 0.5f);
        center.y = GetGroundY(center);

        BossTelegraphArea telegraph = BossTelegraphArea.CreateRectangle(center, forward, pattern.length, pattern.width, warningTime, warningColor, null, warningMaterial);
        yield return new WaitForSeconds(warningTime);

        if (telegraph != null)
            Destroy(telegraph.gameObject);

        if (pattern.projectileVFXPrefab != null)
        {
            // 투사체가 경로를 지나가는 동안 직선 구간을 위험 범위로 유지
            // (예고 종료 후 경로에 재진입하여 투사체에 맞는 문제 방지)
            float travelTime = pattern.projectileSpeed > 0.01f
                ? Mathf.Min(pattern.projectileLifetime, pattern.length / pattern.projectileSpeed)
                : pattern.projectileLifetime;
            RegisterTemporaryRectangleDangerZone(
                "LineOrb_Active", origin, forward, pattern.length, pattern.width,
                travelTime, pattern.damage, 0f, DangerZoneCategory.ActiveDamage);

            SpawnLineProjectile(pattern, origin, forward);
        }
        else
        {
            ApplyRectangleDamage(origin, forward, pattern.length, pattern.width, pattern.damage);
            if (pattern.patternVFXTiming != BossPatternVFXTiming.None)
                SpawnPatternVFX(pattern, origin + forward * pattern.length);
        }

        float remaining = Mathf.Max(0f, duration - warningTime);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
    }

    private IEnumerator ExecuteRoarBombs(GroundPattern pattern, float duration, float warningTime)
    {
        List<Vector3> positions = GenerateRandomPositionsAroundPartyTargets(
            Mathf.Max(1, pattern.spawnCount),
            Mathf.Max(0f, pattern.spawnRadiusAroundPlayer),
            Mathf.Max(0f, pattern.spawnCircleRadius * 0.75f),
            pattern.spawnCircleRadius,
            includeHumanPlayerPosition: false);

        List<BossTelegraphArea> telegraphs = new List<BossTelegraphArea>();
        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 pos = positions[i];
            telegraphs.Add(BossTelegraphArea.CreateCircle(pos, pattern.spawnCircleRadius, warningTime, warningColor, null, warningMaterial));

            if (pattern.patternVFXTiming == BossPatternVFXTiming.WarningStart)
                SpawnPatternVFX(pattern, pos);
        }

        yield return new WaitForSeconds(warningTime);

        for (int i = 0; i < positions.Count; i++)
        {
            ApplyCircleDamage(positions[i], pattern.spawnCircleRadius, pattern.damage);
            if (pattern.patternVFXTiming == BossPatternVFXTiming.Impact)
                SpawnPatternVFX(pattern, positions[i]);
        }

        for (int i = 0; i < telegraphs.Count; i++)
            if (telegraphs[i] != null)
                Destroy(telegraphs[i].gameObject);

        float remaining = Mathf.Max(0f, duration - warningTime);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
    }

    private IEnumerator RunPhase2Cycle()
    {
        if (bossDead || health == null || health.IsDead)
            yield break;

        phase2Triggered = true;
        inPhase2 = true;
        inSpecialPattern = true;
        currentPattern = "Phase2_FlightCycle";
        SetBasicPatternEnabled(false);

        SpawnBossVFX(flyPhase.buffVFXPrefab);
        yield return PlayTimedLoopingPhaseClip(flyPhase.buffClip, flyPhase.buffStateName, flyPhase.buffDuration);

        flying = true;
        SetUntargetable(true);
        groundPositionBeforeFly = transform.position;

        SpawnBossVFX(flyPhase.flyUpVFXPrefab);
        PlayPhaseClip(flyPhase.flyUpClip, flyPhase.flyUpStateName, flyPhase.flyUpDuration);
        if (flyPhase.moveBossUpDuringFly)
            yield return MoveBossVertical(groundPositionBeforeFly + Vector3.up * flyPhase.flyHeight, flyPhase.flyUpDuration);
        else
            yield return new WaitForSeconds(flyPhase.flyUpDuration);

        PlayLoopingPhaseClip(flyPhase.flyClip, flyPhase.flyStateName);

        for (int repeat = 0; repeat < Mathf.Max(1, flyPhase.airPatternRepeats); repeat++)
        {
            yield return ExecuteRandomLightningStrikes();
            yield return ExecuteLightningDOTZones();
            yield return ExecuteTrackingLightning();
        }

        SpawnBossVFX(flyPhase.landVFXPrefab);
        PlayPhaseClip(flyPhase.landClip, flyPhase.landStateName, flyPhase.landDuration);
        if (flyPhase.moveBossUpDuringFly)
            yield return MoveBossVertical(groundPositionBeforeFly, flyPhase.landDuration);
        else
            yield return new WaitForSeconds(flyPhase.landDuration);

        flying = false;
        SetUntargetable(false);
        StopClip();

        SetBasicPatternEnabled(true);
        inSpecialPattern = false;
        inPhase2 = false;
        currentPattern = string.Empty;
        IsTrackingLightningActive = false;
    }

    private IEnumerator ExecuteRandomLightningStrikes()
    {
        ResolveWaveCounts(flyPhase.randomLightningWaves, flyPhase.randomLightningPerWave, flyPhase.randomLightningCount, out int waves, out int perWave);

        yield return RunTelegraphedWaves(
            waves,
            perWave,
            flyPhase.randomLightningSpreadRadius,
            flyPhase.randomLightningMinSeparation,
            flyPhase.randomLightningRadius,
            flyPhase.randomLightningWarningTime,
            flyPhase.randomLightningWaveInterval,
            pos =>
            {
                ApplyCircleDamage(pos, flyPhase.randomLightningRadius, flyPhase.randomLightningDamage);
                SpawnVFX(flyPhase.randomLightningVFXPrefab, pos + flyPhase.lightningVFXOffset, Quaternion.Euler(flyPhase.lightningVFXEuler), flyPhase.lightningVFXScale, flyPhase.lightningVFXDestroyDelay);
            });
    }

    private IEnumerator ExecuteLightningDOTZones()
    {
        ResolveWaveCounts(flyPhase.dotZoneWaves, flyPhase.dotZonesPerWave, flyPhase.dotZoneCount, out int waves, out int perWave);

        yield return RunTelegraphedWaves(
            waves,
            perWave,
            flyPhase.dotZoneSpreadRadius,
            flyPhase.dotZoneMinSeparation,
            flyPhase.dotZoneRadius,
            flyPhase.dotZoneWarningTime,
            flyPhase.dotZoneInterval,
            pos => StartCoroutine(DOTZoneRoutine(pos)));

        yield return new WaitForSeconds(flyPhase.dotZoneDuration);
    }

    // 구버전 count 설정과의 호환 처리 (waves/perWave 미설정 시 count 사용)
    private static void ResolveWaveCounts(int configuredWaves, int configuredPerWave, int legacyCount, out int waves, out int perWave)
    {
        waves = Mathf.Max(1, configuredWaves);
        perWave = Mathf.Max(1, configuredPerWave);

        if (configuredWaves <= 0 || configuredPerWave <= 0)
        {
            waves = 1;
            perWave = Mathf.Max(1, legacyCount);
        }
    }

    // 두 낙뢰 패턴이 공유하는 웨이브 루틴: 원형 텔레그래프 표시 → 경고 대기 → 위치별 효과 실행
    private IEnumerator RunTelegraphedWaves(int waves, int perWave, float spreadRadius, float minSeparation, float circleRadius, float warningTime, float waveInterval, System.Action<Vector3> onImpact)
    {
        for (int wave = 0; wave < waves; wave++)
        {
            List<Vector3> positions = GenerateRandomPositionsAroundPartyTargets(
                perWave,
                spreadRadius,
                minSeparation,
                circleRadius,
                includeHumanPlayerPosition: false);

            List<BossTelegraphArea> telegraphs = new List<BossTelegraphArea>(positions.Count);
            for (int i = 0; i < positions.Count; i++)
                telegraphs.Add(BossTelegraphArea.CreateCircle(positions[i], circleRadius, warningTime, warningColor, null, warningMaterial));

            yield return new WaitForSeconds(warningTime);

            for (int i = 0; i < telegraphs.Count; i++)
                if (telegraphs[i] != null)
                    Destroy(telegraphs[i].gameObject);

            for (int i = 0; i < positions.Count; i++)
                onImpact(positions[i]);

            yield return new WaitForSeconds(waveInterval);
        }
    }

    private IEnumerator DOTZoneRoutine(Vector3 pos)
    {
        GameObject dotVFX = SpawnVFX(flyPhase.dotZoneVFXPrefab, pos + flyPhase.lightningVFXOffset, Quaternion.Euler(flyPhase.lightningVFXEuler), flyPhase.lightningVFXScale, flyPhase.dotZoneDuration + 0.2f);
        DangerZoneHandle dotZone = RegisterTemporaryCircleDangerZone("Phase2_DOT_Lightning", pos, flyPhase.dotZoneRadius, flyPhase.dotZoneDuration, 0f, flyPhase.dotZoneTickDamage, DangerZoneCategory.PersistentDOT);

        float elapsed = 0f;
        while (elapsed < flyPhase.dotZoneDuration)
        {
            ApplyCircleDamage(pos, flyPhase.dotZoneRadius, flyPhase.dotZoneTickDamage);
            float interval = Mathf.Max(0.05f, flyPhase.dotZoneTickInterval);
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        if (dotZone != null)
            Destroy(dotZone.gameObject);

        if (dotVFX != null)
            Destroy(dotVFX);
    }

    private IEnumerator ExecuteTrackingLightning()
    {
        PlayerStatus[] targets = GetValidPlayerTargets();
        if (targets == null || targets.Length == 0)
            yield break;

        int activeRoutines = 0;
        for (int i = 0; i < targets.Length; i++)
        {
            PlayerStatus target = targets[i];
            if (!PartyTargetUtility.IsValidPlayerTarget(target))
                continue;

            activeRoutines++;
            StartCoroutine(TrackingLightningForTarget(target, () => activeRoutines--));
        }

        // 추적 낙뢰 진행 표시 (NPC 산개 행동 판단용)
        IsTrackingLightningActive = true;

        while (activeRoutines > 0)
            yield return null;

        IsTrackingLightningActive = false;
    }

    private IEnumerator TrackingLightningForTarget(PlayerStatus trackedTarget, System.Action onComplete)
    {
        float endTime = Time.time + Mathf.Max(0.1f, flyPhase.trackingDuration);
        BossTelegraphArea telegraph = null;
        GameObject followVFX = null;

        while (Time.time < endTime && PartyTargetUtility.IsValidPlayerTarget(trackedTarget))
        {
            Vector3 pos = trackedTarget.transform.position;
            pos.y = GetGroundY(pos);

            if (telegraph == null)
            {
                telegraph = BossTelegraphArea.CreateCircle(pos, flyPhase.trackingRadius, flyPhase.trackingDuration, warningColor, null, warningMaterial);
                followVFX = SpawnVFX(flyPhase.trackingWarningVFXPrefab, pos + flyPhase.lightningVFXOffset, Quaternion.Euler(flyPhase.lightningVFXEuler), flyPhase.lightningVFXScale, flyPhase.trackingDuration + 0.2f);
            }
            else
            {
                telegraph.transform.position = pos;
                if (followVFX != null)
                    followVFX.transform.position = pos + flyPhase.lightningVFXOffset;
            }

            yield return null;
        }

        Vector3 finalPos = telegraph != null ? telegraph.transform.position : trackedTarget != null ? trackedTarget.transform.position : transform.position;
        if (telegraph != null)
            Destroy(telegraph.gameObject);
        if (followVFX != null)
            Destroy(followVFX);

        yield return TrackingImpactDOTRoutine(finalPos);
        onComplete?.Invoke();
    }

    private IEnumerator TrackingImpactDOTRoutine(Vector3 finalPos)
    {
        float duration = Mathf.Max(0f, flyPhase.trackingImpactDuration);
        GameObject impactVFX = SpawnVFX(
            flyPhase.trackingImpactVFXPrefab,
            finalPos + flyPhase.lightningVFXOffset,
            Quaternion.Euler(flyPhase.lightningVFXEuler),
            flyPhase.lightningVFXScale,
            duration + Mathf.Max(0.2f, flyPhase.lightningVFXDestroyDelay));

        DangerZoneHandle impactZone = RegisterTemporaryCircleDangerZone("Tracking_Lightning_DOT", finalPos, flyPhase.trackingRadius, duration, flyPhase.trackingDamage, flyPhase.trackingTickDamage, DangerZoneCategory.Tracking);

        // 착탄 순간 1회 데미지. 이후 유지 시간 동안 DOT 데미지.
        ApplyCircleDamage(finalPos, flyPhase.trackingRadius, flyPhase.trackingDamage);

        if (duration > 0f)
        {
            float elapsed = 0f;
            float interval = Mathf.Max(0.05f, flyPhase.trackingTickInterval);
            while (elapsed < duration)
            {
                yield return new WaitForSeconds(interval);
                elapsed += interval;
                ApplyCircleDamage(finalPos, flyPhase.trackingRadius, flyPhase.trackingTickDamage);
            }
        }

        if (impactZone != null)
            Destroy(impactZone.gameObject);

        if (impactVFX != null)
            Destroy(impactVFX);
    }


    private List<Vector3> GenerateRandomPositionsAroundPartyTargets(int count, float spreadRadius, float minSeparation, float telegraphRadius, bool includeHumanPlayerPosition)
    {
        List<Vector3> results = new List<Vector3>();
        float safeMinSeparation = Mathf.Max(0f, minSeparation);
        if (safeMinSeparation <= 0f)
            safeMinSeparation = Mathf.Max(0f, telegraphRadius * 0.75f);

        PlayerStatus humanPlayer = includeHumanPlayerPosition ? FindHumanPlayerTarget() : null;
        if (humanPlayer != null && count > 0)
        {
            Vector3 playerPos = humanPlayer.transform.position;
            playerPos.y = GetGroundY(playerPos);
            results.Add(playerPos);
        }

        int attemptsPerPoint = 30;
        while (results.Count < Mathf.Max(1, count))
        {
            PlayerStatus centerTarget = GetRandomValidPartyTarget();
            Vector3 center = centerTarget != null ? centerTarget.transform.position : targetPlayer != null ? targetPlayer.transform.position : transform.position;
            Vector3 chosen = center;
            bool found = false;

            for (int attempt = 0; attempt < attemptsPerPoint; attempt++)
            {
                Vector2 random = UnityEngine.Random.insideUnitCircle * Mathf.Max(0f, spreadRadius);
                Vector3 candidate = center + new Vector3(random.x, 0f, random.y);
                candidate.y = GetGroundY(candidate);

                bool tooClose = false;
                for (int j = 0; j < results.Count; j++)
                {
                    Vector3 delta = candidate - results[j];
                    delta.y = 0f;
                    if (delta.magnitude < safeMinSeparation)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    chosen = candidate;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Vector2 random = UnityEngine.Random.insideUnitCircle * Mathf.Max(0f, spreadRadius);
                chosen = center + new Vector3(random.x, 0f, random.y);
                chosen.y = GetGroundY(chosen);
            }

            results.Add(chosen);
        }

        return results;
    }

    private DangerZoneHandle RegisterTemporaryCircleDangerZone(string zoneName, Vector3 position, float radius, float duration, float damage, float tickDamage, DangerZoneCategory category)
    {
        GameObject zoneObject = new GameObject(zoneName);
        zoneObject.transform.position = new Vector3(position.x, GetGroundY(position), position.z);
        DangerZoneHandle handle = zoneObject.AddComponent<DangerZoneHandle>();
        handle.ConfigureCircle(zoneName, category, radius, duration, damage, tickDamage, duration <= 0f);
        if (duration > 0f)
            Destroy(zoneObject, duration + 0.1f);
        return handle;
    }

    // 실제 피해 구간 동안 유지되는 부채꼴 위험 범위 등록 (예고 종료 후 재진입 피해 방지)
    private DangerZoneHandle RegisterTemporaryConeDangerZone(string zoneName, Vector3 position, Vector3 forward, float length, float angle, float duration, float damage, float tickDamage, DangerZoneCategory category)
    {
        GameObject zoneObject = new GameObject(zoneName);
        zoneObject.transform.position = new Vector3(position.x, GetGroundY(position), position.z);
        zoneObject.transform.rotation = FlatLookRotation(forward);
        DangerZoneHandle handle = zoneObject.AddComponent<DangerZoneHandle>();
        handle.ConfigureCone(zoneName, category, length, angle, duration, damage, tickDamage, duration <= 0f);
        if (duration > 0f)
            Destroy(zoneObject, duration + 0.1f);
        return handle;
    }

    // 실제 피해 구간 동안 유지되는 직사각형 위험 범위 등록 (origin에서 forward 방향으로 length)
    private DangerZoneHandle RegisterTemporaryRectangleDangerZone(string zoneName, Vector3 origin, Vector3 forward, float length, float width, float duration, float damage, float tickDamage, DangerZoneCategory category)
    {
        GameObject zoneObject = new GameObject(zoneName);
        zoneObject.transform.position = new Vector3(origin.x, GetGroundY(origin), origin.z);
        zoneObject.transform.rotation = FlatLookRotation(forward);
        DangerZoneHandle handle = zoneObject.AddComponent<DangerZoneHandle>();
        handle.ConfigureRectangle(zoneName, category, length, width, duration, damage, tickDamage, duration <= 0f);
        if (duration > 0f)
            Destroy(zoneObject, duration + 0.1f);
        return handle;
    }

    // 수평면 기준 바라보기 회전 (방향이 퇴화하면 월드 전방 사용)
    private static Quaternion FlatLookRotation(Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private IEnumerator MoveBossVertical(Vector3 targetPosition, float duration)
    {
        Vector3 start = transform.position;
        duration = Mathf.Max(0.05f, duration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0f, 1f, t);
            transform.position = Vector3.Lerp(start, targetPosition, t);
            yield return null;
        }
        transform.position = targetPosition;
    }

    private BossTelegraphArea CreateTelegraph(GroundPattern pattern, Vector3 origin, Vector3 forward, float duration)
    {
        switch (pattern.shape)
        {
            case BossPatternShape.Circle:
                return BossTelegraphArea.CreateCircle(new Vector3(origin.x, GetGroundY(origin), origin.z), pattern.radius, duration, warningColor, null, warningMaterial);

            case BossPatternShape.Line:
                Vector3 center = origin + forward * (pattern.length * 0.5f);
                center.y = GetGroundY(center);
                return BossTelegraphArea.CreateRectangle(center, forward, pattern.length, pattern.width, duration, warningColor, null, warningMaterial);

            case BossPatternShape.Cone:
                return BossTelegraphArea.CreateCone(new Vector3(origin.x, GetGroundY(origin), origin.z), forward, pattern.length, pattern.angle, duration, warningColor, null, warningMaterial);
        }
        return null;
    }

    private void ApplyPatternDamage(GroundPattern pattern, Vector3 origin, Vector3 forward)
    {
        switch (pattern.shape)
        {
            case BossPatternShape.Circle:
                ApplyCircleDamage(origin, pattern.radius, pattern.damage);
                break;
            case BossPatternShape.Line:
                ApplyRectangleDamage(origin, forward, pattern.length, pattern.width, pattern.damage);
                break;
            case BossPatternShape.Cone:
                ApplyConeDamage(origin, forward, pattern.length, pattern.angle, pattern.damage);
                break;
        }
    }

    private void ApplyCircleDamage(Vector3 center, float radius, float damage)
    {
        PlayerStatus[] players = GetValidPlayerTargets();
        for (int i = 0; i < players.Length; i++)
        {
            PlayerStatus player = players[i];
            Vector3 delta = player.transform.position - center;
            delta.y = 0f;
            if (delta.magnitude <= radius)
                DamagePlayer(player, damage);
        }
    }

    private void ApplyRectangleDamage(Vector3 origin, Vector3 forward, float length, float width, float damage)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = transform.forward;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        PlayerStatus[] players = GetValidPlayerTargets();
        for (int i = 0; i < players.Length; i++)
        {
            PlayerStatus player = players[i];
            Vector3 delta = player.transform.position - origin;
            delta.y = 0f;
            float z = Vector3.Dot(delta, forward);
            float x = Mathf.Abs(Vector3.Dot(delta, right));
            if (z >= 0f && z <= length && x <= width * 0.5f)
                DamagePlayer(player, damage);
        }
    }

    private void ApplyConeDamage(Vector3 origin, Vector3 forward, float range, float angle, float damage)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = transform.forward;
        forward.Normalize();

        PlayerStatus[] players = GetValidPlayerTargets();
        for (int i = 0; i < players.Length; i++)
        {
            PlayerStatus player = players[i];
            Vector3 delta = player.transform.position - origin;
            delta.y = 0f;
            if (delta.magnitude > range)
                continue;

            if (delta.sqrMagnitude < 0.0001f)
            {
                DamagePlayer(player, damage);
                continue;
            }

            float a = Vector3.Angle(forward, delta.normalized);
            if (a <= angle * 0.5f)
                DamagePlayer(player, damage);
        }
    }

    private PlayerStatus[] GetValidPlayerTargets()
    {
        // 재사용 버퍼에 수집 후 배열화 (씬 전체 탐색 제거)
        PartyTargetUtility.CollectValidPlayerTargets(validTargetBuffer);
        return validTargetBuffer.Count > 0 ? validTargetBuffer.ToArray() : System.Array.Empty<PlayerStatus>();
    }

    private void DamagePlayer(PlayerStatus player, float damage)
    {
        if (!PartyTargetUtility.IsValidPlayerTarget(player))
            return;

        player.TakeDamage(damage, gameObject);
        SpawnPlayerHitVFX(player);
    }

    private void SpawnPlayerHitVFX(PlayerStatus player)
    {
        if (player == null || playerHitVFXPrefab == null)
            return;

        Transform anchor = FindChildByName(player.transform, playerHitVFXAnchorName);
        Transform parent = attachPlayerHitVFXToTarget ? (anchor != null ? anchor : player.transform) : null;
        Vector3 position = anchor != null ? anchor.position + playerHitVFXOffset : player.transform.position + playerHitVFXOffset;
        Quaternion rotation = Quaternion.Euler(playerHitVFXEuler);
        GameObject instance = SpawnVFX(playerHitVFXPrefab, position, rotation, playerHitVFXScale, playerHitVFXDestroyDelay);
        if (instance != null && parent != null)
            instance.transform.SetParent(parent, true);
    }

    private Vector3 GetPatternOrigin(GroundPattern pattern)
    {
        Transform origin = pattern != null && pattern.originPoint != null ? pattern.originPoint : transform;
        Vector3 local = pattern != null ? pattern.localOffset : Vector3.zero;
        return origin.position + origin.TransformDirection(local);
    }

    private Vector3 GetPatternForward(GroundPattern pattern, Vector3 origin)
    {
        if (pattern != null && pattern.aimAtPlayer && targetPlayer != null)
        {
            Vector3 dir = targetPlayer.transform.position - origin;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                return dir.normalized;
        }

        Vector3 forward = pattern != null && pattern.originPoint != null ? pattern.originPoint.forward : transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude < 0.0001f ? transform.forward : forward.normalized;
    }

    private Vector3 GetPatternVFXPosition(GroundPattern pattern, Vector3 origin)
    {
        if (pattern == null)
            return origin;

        if (pattern.shape == BossPatternShape.Line)
        {
            Vector3 forward = GetPatternForward(pattern, origin);
            return origin + forward * Mathf.Max(0.1f, pattern.length);
        }

        if (pattern.shape == BossPatternShape.Cone)
        {
            Vector3 forward = GetPatternForward(pattern, origin);
            return origin + forward * Mathf.Max(0.1f, pattern.length * 0.65f);
        }

        return origin;
    }

    private void SpawnPatternVFX(GroundPattern pattern, Vector3 position)
    {
        if (pattern == null || pattern.patternVFXPrefab == null || pattern.patternVFXTiming == BossPatternVFXTiming.None)
            return;

        Quaternion rotation = Quaternion.Euler(pattern.patternVFXEuler);
        SpawnVFX(pattern.patternVFXPrefab, position + pattern.patternVFXOffset, rotation, pattern.patternVFXScale, pattern.patternVFXDestroyDelay);
    }

    private void SpawnLineProjectile(GroundPattern pattern, Vector3 origin, Vector3 forward)
    {
        if (pattern == null || pattern.projectileVFXPrefab == null)
            return;

        Vector3 start = origin + pattern.projectileOffset;
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(pattern.projectileEuler);
        GameObject projectileObject = SpawnVFX(pattern.projectileVFXPrefab, start, rotation, pattern.projectileScale, pattern.projectileLifetime + 0.25f);
        if (projectileObject == null)
            return;

        BossPatternProjectile projectile = projectileObject.GetComponent<BossPatternProjectile>();
        if (projectile == null)
            projectile = projectileObject.AddComponent<BossPatternProjectile>();

        projectile.hitRadius = pattern.projectileHitRadius;
        projectile.targetPlayer = targetPlayer;
        projectile.hitAnyPartyMember = true;
        projectile.onHitPlayer = p => DamagePlayer(p, pattern.damage);
        projectile.onArrived = () => SpawnPatternVFX(pattern, start + forward * pattern.length);
        projectile.Initialize(start + forward * pattern.length, pattern.projectileSpeed, pattern.projectileLifetime);
    }

    private void SpawnBossVFX(GameObject prefab)
    {
        if (prefab == null)
            return;

        SpawnVFX(prefab, transform.position + transform.TransformDirection(flyPhase.bossVFXOffset), transform.rotation * Quaternion.Euler(flyPhase.bossVFXEuler), flyPhase.bossVFXScale, flyPhase.bossVFXDestroyDelay);
    }

    // 공용 VFX 유틸 위임 (기존 호출부 유지용 래퍼)
    private GameObject SpawnVFX(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, float destroyDelay)
    {
        return VFXUtility.Spawn(prefab, position, rotation, scale, destroyDelay);
    }

    private Transform FindChildByName(Transform root, string childName)
    {
        return VFXUtility.FindChildByName(root, childName);
    }

    private float GetGroundY(Vector3 reference)
    {
        if (usePlayerGroundY && targetPlayer != null)
            return targetPlayer.transform.position.y + telegraphGroundY;

        return reference.y + telegraphGroundY;
    }

    private Vector3 GetFireBreathForward(GroundPattern pattern)
    {
        Vector3 forward = Vector3.zero;

        if (pattern != null && pattern.originPoint != null)
            forward = pattern.originPoint.forward;

        if (forward.sqrMagnitude < 0.0001f)
            forward = transform.forward;

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        return forward.normalized;
    }

    private void FacePlayer()
    {
        if (targetPlayer == null)
            return;

        Vector3 dir = targetPlayer.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private void SetBasicPatternEnabled(bool value)
    {
        if (basicPatternController != null)
            basicPatternController.SetSpecialPatternLock(!value);
    }

    private void SetUntargetable(bool value)
    {
        if (!flyPhase.makeUntargetableWhileFlying)
            return;

        if (!value)
        {
            foreach (KeyValuePair<GameObject, int> pair in originalLayers)
            {
                if (pair.Key != null)
                    pair.Key.layer = pair.Value;
            }
            originalLayers.Clear();
            return;
        }

        int layer = LayerMask.NameToLayer(flyPhase.untargetableLayerName);
        if (layer < 0)
        {
            if (logPattern)
                Debug.LogWarning($"[BossSkillPattern] Untargetable layer not found: {flyPhase.untargetableLayerName}", this);
            return;
        }

        originalLayers.Clear();
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] == null)
                continue;

            GameObject go = children[i].gameObject;
            originalLayers[go] = go.layer;
            go.layer = layer;
        }
    }

    private float ResolveClipDuration(AnimationClip clip, float overrideDuration, float fallback)
    {
        if (overrideDuration > 0.01f)
            return overrideDuration;
        if (clip != null)
            return Mathf.Max(0.05f, clip.length);
        return fallback;
    }

    private IEnumerator WaitForBasicAttackToFinish()
    {
        if (basicPatternController == null)
            yield break;

        basicPatternController.SetSpecialPatternLock(true);

        if (!waitForBasicAttackToFinish)
            yield break;

        while (basicPatternController.IsAttacking)
            yield return null;
    }

    private void UpdatePhase1PatternCounters(GroundPattern pattern)
    {
        if (pattern == attack10FireBreath)
            phase1PatternsSinceFireBreath = 0;
        else
            phase1PatternsSinceFireBreath++;
    }

    private IEnumerator PlayTimedLoopingPhaseClip(AnimationClip clip, string stateName, float duration)
    {
        duration = Mathf.Max(0.05f, duration);

        if (animator == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        animator.enabled = true;
        if (disableRootMotion)
            animator.applyRootMotion = false;

        if (useDirectClipPlayback && clip != null)
        {
            ClipPlayer.Play(animator, clip, 1f, true);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                ClipPlayer.Tick();
                yield return null;
            }

            ClipPlayer.Stop();
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(stateName))
        {
            StopClip();
            animator.CrossFadeInFixedTime(stateName, 0.08f, 0, 0f);
            animator.Update(0f);
        }

        yield return new WaitForSeconds(duration);
    }

    private void PlayPhaseClip(AnimationClip clip, string stateName, float duration)
    {
        // 새 클립 재생 시 공용 플레이어가 기존 루프 상태를 스스로 해제
        PlayClipOrState(clip, stateName, duration);
    }

    private void PlayLoopingPhaseClip(AnimationClip clip, string stateName)
    {
        if (animator == null)
            return;

        animator.enabled = true;
        if (disableRootMotion)
            animator.applyRootMotion = false;

        if (useDirectClipPlayback && clip != null)
        {
            // 루프 유지는 Update의 KeepLoopingPhaseClipIfNeeded에서 Tick으로 처리
            ClipPlayer.Play(animator, clip, 1f, true);
            return;
        }

        if (!string.IsNullOrWhiteSpace(stateName))
        {
            StopClip();
            animator.CrossFadeInFixedTime(stateName, 0.08f, 0, 0f);
            animator.Update(0f);
        }
    }

    private void KeepLoopingPhaseClipIfNeeded()
    {
        clipPlayer?.Tick();
    }

    private void PlayClipOrState(AnimationClip clip, string stateName, float duration)
    {
        if (animator == null)
            return;

        animator.enabled = true;
        if (disableRootMotion)
            animator.applyRootMotion = false;

        if (useDirectClipPlayback && clip != null)
        {
            ClipPlayer.PlayTimed(animator, clip, duration);
            return;
        }

        if (!string.IsNullOrWhiteSpace(stateName))
        {
            StopClip();
            animator.CrossFadeInFixedTime(stateName, 0.08f, 0, 0f);
            animator.Update(0f);
        }
    }

    private void StopClip()
    {
        clipPlayer?.Stop();
    }

    private void OnDestroy()
    {
        CombatRegistry.Unregister(this);

        if (health != null)
        {
            health.onHealthChanged.RemoveListener(OnHealthChanged);
            health.onDeath.RemoveListener(OnBossDeath);
        }

        clipPlayer?.Dispose();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        DrawPatternGizmo(attack5LeftWing, Color.yellow);
        DrawPatternGizmo(attack6RightWing, Color.cyan);
        DrawPatternGizmo(attack10FireBreath, Color.red);
    }

    private void DrawPatternGizmo(GroundPattern p, Color color)
    {
        if (p == null)
            return;

        Vector3 origin = p.originPoint != null ? p.originPoint.position : transform.position;
        Gizmos.color = color;
        if (p.shape == BossPatternShape.Circle)
            Gizmos.DrawWireSphere(origin, p.radius);
        else
            Gizmos.DrawLine(origin, origin + transform.forward * p.length);
    }
}
