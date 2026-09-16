using System.Collections.Generic;
using UnityEngine;

public enum CoordinatorMode
{
    None = 0,   // 조율 없음 (기존 조건과 동일)
    Rule = 1,   // 규칙 기반 조율 (8-B 1단계)
    RL = 2      // 학습형 조율 (8-B 2단계 예약)
}

/// <summary>
/// 파티 전체 상태를 보고 NPC별 지시를 발행하는 상위 조율자 (8-B 1단계: 규칙 기반).
///
/// 설계 원칙.
/// - 지시는 강제가 아니라 편향 (NpcCommandBias). Mask와 안전 행동은 불가침
/// - 하위 정책(Utility, GNN) 불변. FSM 계열은 편향을 읽지 않으므로 자연히 무영향
/// - 스크립트 플레이어는 지시 대상에서 제외 (플레이어 행동은 조건 간 통제 변수)
///
/// 규칙 v1의 우선순위.
/// 1. 즉사기 충전 중: 전원 Free (개별 안전 로직의 그림자 홀드가 최선)
/// 2. 슬라임 생존 + 보스 무적: 힐러 아닌 NPC 중 슬라임과 가까운 순으로 전담 지정
/// 3. 아군 HP 임계 이하 + NPC 힐러 존재: 힐러에게 보호 지시
/// 4. 화력 페이스 판정: 타임아웃 궤적이면 화력 집중, 파티 소모전이면 수비 대기
/// </summary>
public class RaidCoordinator : MonoBehaviour
{
    public static RaidCoordinator Instance { get; private set; }

    [Header("Mode")]
    public CoordinatorMode mode = CoordinatorMode.None;

    [Header("Evaluation")]
    [Min(0.5f)] public float evaluateInterval = 3f;
    [Tooltip("국면 전환(보스 무적, 슬라임, 즉사기) 감지 시 즉시 재평가")]
    public bool reevaluateOnPhaseChange = true;

    [Header("Rule Parameters")]
    public float episodeTimeLimit = 600f;
    [Tooltip("타임아웃 궤적 판정: 보스 진행도 < 경과 비율 x 이 계수면 화력 집중")]
    public float paceMargin = 1.15f;
    [Range(0.05f, 0.95f)] public float protectHpThreshold = 0.5f;
    // v1.1: 0.4 -> 0.25. 수비 대기가 너무 자주 걸려 화력을 깎아 먹는 문제의 완화
    // (전멸을 타임아웃으로 바꿀 뿐 클리어로 전환하지 못함)
    [Range(0.05f, 0.95f)] public float defensivePartyHpThreshold = 0.25f;
    [Min(1)] public int slimeHandlerCount = 1;

    [Header("Debug")]
    public bool logCommands = false;
    [SerializeField] private string lastDecisionSummary;

    private float nextEvaluateTime;
    private bool lastBossAttackable = true;
    private bool lastSlimesAlive;
    private bool lastOneShotActive;

    private readonly List<NPCSimpleFSMController> npcs = new List<NPCSimpleFSMController>(4);
    private readonly HashSet<NPCSimpleFSMController> issuedThisTick = new HashSet<NPCSimpleFSMController>();

    private void Awake()
    {
        Instance = this;
        CommandBoard.ClearAll();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        CommandBoard.ClearAll();
    }

    private void Update()
    {
        if (mode == CoordinatorMode.None)
            return;

        CollectNpcs();
        if (npcs.Count == 0)
            return;

        bool oneShotActive = IsOneShotActive();
        bool slimesAlive = SlimeTargetSelector.AnyAlive();
        bool bossAttackable = NpcActionMask.IsBossTacticallyAttackable(npcs[0]);

        bool phaseChanged = oneShotActive != lastOneShotActive
            || slimesAlive != lastSlimesAlive
            || bossAttackable != lastBossAttackable;

        if (Time.time < nextEvaluateTime && !(reevaluateOnPhaseChange && phaseChanged))
            return;

        nextEvaluateTime = Time.time + evaluateInterval;
        lastOneShotActive = oneShotActive;
        lastSlimesAlive = slimesAlive;
        lastBossAttackable = bossAttackable;

        Evaluate(oneShotActive, slimesAlive, bossAttackable);
    }

    // ---------- 규칙 조율 v1 ----------

    private void Evaluate(bool oneShotActive, bool slimesAlive, bool bossAttackable)
    {
        issuedThisTick.Clear();

        // 1. 즉사기 충전 중: 전원 무개입 (그림자 홀드가 개별 안전 로직으로 동작)
        if (oneShotActive)
        {
            IssueRemaining(NpcCommandType.Free, "OneShot: all free");
            return;
        }

        // 2. 슬라임 생존 + 보스 무적: 슬라임 전담 배분
        if (slimesAlive && !bossAttackable)
        {
            AssignSlimeHandlers();
            IssueRemaining(NpcCommandType.Free, "AddPhase: slime handlers assigned");
            return;
        }

        // 3. 아군 보호: NPC 힐러가 있고 위험한 아군이 있으면 힐러에게 보호 지시
        PlayerStatus lowest = NpcHealerPolicy.FindLowestHpAlly(out float lowestRatio);
        NPCSimpleFSMController healer = FindHealer();
        if (healer != null && lowest != null && lowestRatio <= protectHpThreshold)
        {
            Issue(healer, NpcCommandType.ProtectAlly, lowest);
        }

        // 4. 화력 페이스 판정
        float progress = 1f - GetBossHpRatio();
        float pace = episodeTimeLimit > 0f ? Time.timeSinceLevelLoad / episodeTimeLimit : 0f;
        float partyHp = GetPartyAverageHp();

        if (bossAttackable && progress < pace * paceMargin)
        {
            // 타임아웃 궤적: 힐러를 제외한 전원 화력 집중
            for (int i = 0; i < npcs.Count; i++)
            {
                if (!issuedThisTick.Contains(npcs[i]) && !npcs[i].IsHealerRole)
                    Issue(npcs[i], NpcCommandType.FocusBoss);
            }
            IssueRemaining(NpcCommandType.Free, $"BehindPace: focus boss (progress {progress:0.00} < pace {pace:0.00})");
            return;
        }

        if (partyHp < defensivePartyHpThreshold)
        {
            IssueRemaining(NpcCommandType.HoldDefensive, $"LowPartyHp {partyHp:0.00}: hold defensive");
            return;
        }

        IssueRemaining(NpcCommandType.Free, "Normal: free");
    }

    // 슬라임 전담: 힐러가 아닌 NPC 중 가장 시급한 슬라임과 가까운 순으로 지정
    private void AssignSlimeHandlers()
    {
        int assigned = 0;
        while (assigned < Mathf.Max(1, slimeHandlerCount))
        {
            NPCSimpleFSMController best = null;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < npcs.Count; i++)
            {
                NPCSimpleFSMController npc = npcs[i];
                if (issuedThisTick.Contains(npc) || npc.IsHealerRole)
                    continue;

                SlimeAddEnemy slime = SlimeTargetSelector.FindMostUrgent(npc.transform.position);
                if (slime == null)
                    return;

                float distance = NPCSimpleFSMController.FlatDistance(npc.transform.position, slime.transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = npc;
                }
            }

            if (best == null)
                return;

            Issue(best, NpcCommandType.HandleSlimes);
            assigned++;
        }
    }

    // ---------- 발행과 상태 조회 ----------

    private void Issue(NPCSimpleFSMController npc, NpcCommandType type, PlayerStatus target = null)
    {
        CommandBoard.Issue(npc, type, target);
        issuedThisTick.Add(npc);

        if (logCommands)
            Debug.Log($"[RaidCoordinator] {npc.name} <- {type}{(target != null ? " (" + target.name + ")" : "")}", this);
    }

    private void IssueRemaining(NpcCommandType type, string summary)
    {
        for (int i = 0; i < npcs.Count; i++)
        {
            if (!issuedThisTick.Contains(npcs[i]))
                Issue(npcs[i], type);
        }

        lastDecisionSummary = summary;
    }

    private void CollectNpcs()
    {
        npcs.Clear();
        CommandBoard.CleanupDestroyed();

        IReadOnlyList<PlayerStatus> statuses = CombatRegistry.PlayerStatuses;
        for (int i = 0; i < statuses.Count; i++)
        {
            PlayerStatus status = statuses[i];
            if (status == null || status.Health == null || status.Health.IsDead)
                continue;

            NPCSimpleFSMController controller = status.GetComponent<NPCSimpleFSMController>();
            if (controller == null || !controller.isActiveAndEnabled || controller.isScriptedPlayer)
                continue;

            npcs.Add(controller);
        }
    }

    private NPCSimpleFSMController FindHealer()
    {
        for (int i = 0; i < npcs.Count; i++)
            if (npcs[i].IsHealerRole)
                return npcs[i];

        return null;
    }

    private float GetBossHpRatio()
    {
        for (int i = 0; i < npcs.Count; i++)
        {
            var boss = npcs[i].boss;
            if (boss == null)
                continue;

            Health health = boss.health != null ? boss.health : boss.GetComponent<Health>();
            if (health != null && health.MaxHealth > 0f)
                return Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);
        }

        return 1f;
    }

    private float GetPartyAverageHp()
    {
        IReadOnlyList<PlayerStatus> statuses = CombatRegistry.PlayerStatuses;
        float sum = 0f;
        int count = 0;

        for (int i = 0; i < statuses.Count; i++)
        {
            PlayerStatus status = statuses[i];
            if (status == null || status.Health == null || status.Health.IsDead || status.Health.MaxHealth <= 0f)
                continue;

            sum += status.Health.CurrentHealth / status.Health.MaxHealth;
            count++;
        }

        return count > 0 ? sum / count : 1f;
    }

    // 즉사기 충전 여부: 시선 차폐 판정을 가진 위험 지역의 존재로 감지
    private static bool IsOneShotActive()
    {
        IReadOnlyList<DangerZoneHandle> zones = DangerZoneRegistry.GetActiveZones();
        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i] != null && zones[i].losOrigin != null)
                return true;
        }

        return false;
    }
}
