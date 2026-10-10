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
///
/// 규칙 v2 (7단계 진단 반영, RGCN 하위 정책 기준).
/// - v1은 Utility와 FSM 기준으로 만든 규칙이라 RGCN 위에서 전사 블록 지연(평균 +45초, 시간 초과 0 → 8),
///   힐러 블록 무효(1% → 4%)
/// - 경계 태세(HoldDefensive v2)를 파티 평균 체력이 아닌 NPC 개인 상태로 발행.
///   NPC 힐러가 없으면 체력 0.7 미만 또는 보스 비행 중, 있으면 체력 0.3 미만일 때만
/// - NPC 힐러가 없는 파티에는 화력 집중을 내리지 않음 (공격성 증가가 전멸을 앞당김)
/// - 보호 지시 임계를 0.4로 낮춤 (불필요한 힐러 화력 손실 감소)
/// - 쫄 단계에서 힐러 제외 NPC 전원을 슬라임 전담으로 지정 (v2c, RGCN은 쫄 단계에 플레이어 추종 47%로 슬라임 방치)
/// - v2d: 보호 지시와 화력 집중은 기본 끔. 총책임자의 지시는 국면 단위 운영(쫄 단계 슬라임 전담, 위험 시 회피 우선, 비행 중 재집결)에 한정
/// </summary>
public class RaidCoordinator : MonoBehaviour
{
    public static RaidCoordinator Instance { get; private set; }

    [Header("Mode")]
    public CoordinatorMode mode = CoordinatorMode.None;

    [Header("Evaluation")]
    [Min(0.5f)] public float evaluateInterval = 3f;
    [Tooltip("국면 전환(보스 무적, 슬라임, 즉사기, 아군 위험) 감지 시 즉시 재평가")]
    public bool reevaluateOnPhaseChange = true;

    [Tooltip("파티 최저 체력이 이 값 아래로 내려가는 순간 즉시 재평가. 0이면 사용 안 함")]
    [Range(0f, 0.95f)] public float lowHpTriggerThreshold = 0.5f;

    [Header("Rule Parameters")]
    public float episodeTimeLimit = 600f;
    [Tooltip("타임아웃 궤적 판정: 보스 진행도 < 경과 비율 x 이 계수면 화력 집중")]
    public float paceMargin = 1.15f;
    [Range(0.05f, 0.95f)] public float protectHpThreshold = 0.5f;
    // v1 값으로 고정 (규칙 조율의 기준선). 수동 튜닝은 v1.1 실험으로 종료
    [Range(0.05f, 0.95f)] public float defensivePartyHpThreshold = 0.4f;
    [Min(1)] public int slimeHandlerCount = 1;

    [Header("Rule v2 / Command Set")]
    [Tooltip("지시 계수표 버전. 1 = v1, 2 = 경계 태세(안전 가산, 긴 시전 억제) 포함 v2")]
    [Range(1, 2)] public int commandSet = 2;
    [Tooltip("규칙 버전. 1 = v1 (파티 평균 기준 수비, 힐러 무관 화력 집중), 2 = NPC별 경계 태세")]
    [Range(1, 2)] public int ruleVersion = 2;
    [Tooltip("v2: NPC 힐러가 없는 파티에서 경계 태세를 내리는 자기 체력 기준")]
    [Range(0.05f, 0.95f)] public float cautionHpNoHealer = 0.7f;
    [Tooltip("v2: NPC 힐러가 있는 파티에서 경계 태세를 내리는 자기 체력 기준")]
    [Range(0.05f, 0.95f)] public float cautionHpWithHealer = 0.3f;
    [Tooltip("v2: NPC 힐러가 없는 파티는 보스 비행 국면 동안 전원 경계 태세")]
    public bool cautionDuringFlyNoHealer = true;
    [Tooltip("v2: 쫄 단계(슬라임 생존 + 보스 무적)의 슬라임 전담 인원. 힐러 제외 NPC 수보다 크면 힐러 제외 전원")]
    [Min(1)] public int slimeHandlerCountV2 = 3;
    [Tooltip("v2: 힐러 보호 지시 사용 여부. v1과 RL 조율 모두 NPC 힐러가 있는 전사 블록에서 단독보다 유의하게 나빴고 공통 요소가 보호 지시라 기본 끔")]
    public bool useProtectV2 = false;
    [Tooltip("v2: 타임아웃 궤적 화력 집중 사용 여부 (기본 끔, 지시는 국면 단위 운영에 한정)")]
    public bool useFocusV2 = false;
    [Tooltip("v2: 힐러 보호 지시 임계 (파티 최저 체력)")]
    [Range(0.05f, 0.95f)] public float protectHpThresholdV2 = 0.4f;

    [Header("RL Data Collection")]
    [Tooltip("Rule 모드에서 각 NPC의 지시를 이 확률로 무작위 지시로 교체. RL 학습용 롤아웃 수집 전용 (평가와 본실험은 0 유지)")]
    [Range(0f, 1f)] public float explorationEpsilon = 0f;
    [Tooltip("탐험 지시 유지 판단 수. 무작위 지시를 고르면 같은 NPC의 이후 판단에도 이 횟수만큼 같은 지시를 유지 (1 = 기존 방식, 판단 1회만 교체). 지시 1회의 효과가 작아 학습 신호가 약했던 문제 대응")]
    [Min(1)] public int explorationHoldDecisions = 1;

    [Header("RL Mode")]
    [Tooltip("학습된 조율자 정책 (coordinator_policy.onnx, COORD_RL_V1). RL 모드에서만 사용")]
    public Unity.InferenceEngine.ModelAsset coordinatorModel;

    [Header("Command Strength")]
    [Tooltip("학습형 하위 정책(GNN 행동 AI)에 지시를 반영하는 세기. 지시 가산점 ln(계수)에 곱하는 배율 (1 = 기존 v1 세기, 0 = 지시 무시). Utility 등 곱셈 반영 정책에는 영향 없음")]
    [Range(0f, 2f)] public float commandStrength = 1f;

    [Header("Decision Log")]
    [Tooltip("지시를 내릴 때마다 실제 관측 23개, 지시, 탐험 여부, RL Q 값을 CSV로 기록 (7단계 RL 재학습용)")]
    public bool logDecisions = true;
    [Tooltip("기록 폴더. 비우면 Application.persistentDataPath")]
    public string decisionLogRoot = "";
    public string decisionLogFile = "coordinator_decisions.csv";

    [Header("Debug")]
    public bool logCommands = false;
    [SerializeField] private string lastDecisionSummary;
    [SerializeField] private int exploredCommandCount; // 이번 에피소드에서 탐험으로 교체된 지시 수
    [SerializeField] private int lowHpTriggerCount;    // 아군 위험 감지로 즉시 재평가한 횟수

    private bool lastLowHp;
    private float nextEvaluateTime;
    private bool lastBossAttackable = true;
    private bool lastSlimesAlive;
    private bool lastOneShotActive;

    // RL 추론 상태
    private Unity.InferenceEngine.Worker coordinatorWorker;
    private readonly float[] obsBuffer = new float[CoordinatorObservation.Dim];
    private bool warnedNoModel;

    // 판단 기록 상태
    private readonly float[] decisionObs = new float[CoordinatorObservation.Dim];
    private float[] pendingQ;          // 직전 RL 추론의 Q 값 (규칙 판단이면 null)
    private RaidMetricsLogger metricsLogger;

    private readonly List<NPCSimpleFSMController> npcs = new List<NPCSimpleFSMController>(4);

    // 탐험 지시 유지 상태 (NPC별 유지 중인 무작위 지시와 남은 판단 수)
    private struct ExploreHold
    {
        public NpcCommandType type;
        public int remaining;
    }
    private readonly Dictionary<NPCSimpleFSMController, ExploreHold> exploreHold = new Dictionary<NPCSimpleFSMController, ExploreHold>();
    private readonly List<NPCSimpleFSMController> staleKeys = new List<NPCSimpleFSMController>();
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
        CoordinatorDecisionLog.Close();
        coordinatorWorker?.Dispose();
        coordinatorWorker = null;
    }

    private void Update()
    {
        NpcCommandBias.LogitScale = commandStrength;
        NpcCommandBias.CommandSet = commandSet;

        if (mode == CoordinatorMode.None)
            return;

        CollectNpcs();
        if (npcs.Count == 0)
            return;

        bool oneShotActive = IsOneShotActive();
        bool slimesAlive = SlimeTargetSelector.AnyAlive();
        bool bossAttackable = NpcActionMask.IsBossTacticallyAttackable(npcs[0]);

        // 아군 위험 진입: 최저 체력이 임계선을 처음 밑돈 순간만 트리거 (회복은 다음 주기에 반영).
        // 상태 갱신이 평가 시점에만 일어나므로 위험이 지속되는 동안 반복 발동하지 않음
        bool lowHp = lowHpTriggerThreshold > 0f && GetPartyMinHp() < lowHpTriggerThreshold;

        bool phaseChanged = oneShotActive != lastOneShotActive
            || slimesAlive != lastSlimesAlive
            || bossAttackable != lastBossAttackable
            || (lowHp && !lastLowHp);

        if (Time.time < nextEvaluateTime && !(reevaluateOnPhaseChange && phaseChanged))
            return;

        // 주기 도래 전에 아군 위험으로 앞당겨진 평가인지 기록 (디버그용)
        if (Time.time < nextEvaluateTime && lowHp && !lastLowHp)
        {
            lowHpTriggerCount++;
            if (logCommands)
                Debug.Log("[RaidCoordinator] 아군 위험 감지로 즉시 재평가", this);
        }

        nextEvaluateTime = Time.time + evaluateInterval;
        lastOneShotActive = oneShotActive;
        lastSlimesAlive = slimesAlive;
        lastBossAttackable = bossAttackable;
        lastLowHp = lowHp;

        if (mode == CoordinatorMode.RL && TryEvaluateRL())
            return;

        if (ruleVersion >= 2)
            EvaluateV2(oneShotActive, slimesAlive, bossAttackable);
        else
            Evaluate(oneShotActive, slimesAlive, bossAttackable);
    }

    // ---------- RL 조율 (2단계) ----------

    // 학습된 정책으로 NPC별 지시 결정. 모델 미준비 시 false 반환 후 규칙으로 폴백 (1회 경고)
    private bool TryEvaluateRL()
    {
        if (coordinatorWorker == null)
        {
            if (coordinatorModel == null)
            {
                if (!warnedNoModel)
                {
                    warnedNoModel = true;
                    Debug.LogWarning("[RaidCoordinator] RL 모드이나 coordinatorModel이 비어 있음. 규칙 조율로 폴백", this);
                }
                return false;
            }

            try
            {
                Unity.InferenceEngine.Model model = Unity.InferenceEngine.ModelLoader.Load(coordinatorModel);
                coordinatorWorker = new Unity.InferenceEngine.Worker(model, Unity.InferenceEngine.BackendType.CPU);
            }
            catch (System.Exception exception)
            {
                if (!warnedNoModel)
                {
                    warnedNoModel = true;
                    Debug.LogError($"[RaidCoordinator] 조율자 모델 로드 실패: {exception.Message}. 규칙 조율로 폴백", this);
                }
                return false;
            }
        }

        issuedThisTick.Clear();

        for (int i = 0; i < npcs.Count; i++)
        {
            NPCSimpleFSMController npc = npcs[i];
            CoordinatorObservation.Fill(obsBuffer, npc, npcs, episodeTimeLimit);

            int best = 0;
            try
            {
                using (var input = new Unity.InferenceEngine.Tensor<float>(
                    new Unity.InferenceEngine.TensorShape(1, CoordinatorObservation.Dim), obsBuffer))
                {
                    coordinatorWorker.SetInput("coordinator_obs", input);
                    coordinatorWorker.Schedule();

                    using (var output = (coordinatorWorker.PeekOutput("command_q")
                        as Unity.InferenceEngine.Tensor<float>).ReadbackAndClone())
                    {
                        float[] qValues = output.DownloadToArray();
                        pendingQ = qValues;
                        float bestQ = float.NegativeInfinity;
                        for (int a = 0; a < qValues.Length && a < 5; a++)
                        {
                            if (qValues[a] > bestQ)
                            {
                                bestQ = qValues[a];
                                best = a;
                            }
                        }
                    }
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[RaidCoordinator] RL 추론 실패: {exception.Message}. 규칙 조율로 폴백", this);
                coordinatorWorker.Dispose();
                coordinatorWorker = null;
                return false;
            }

            NpcCommandType type = (NpcCommandType)best;
            PlayerStatus target = type == NpcCommandType.ProtectAlly
                ? NpcHealerPolicy.FindLowestHpAlly(out _) : null;
            Issue(npc, type, target);
        }

        lastDecisionSummary = "RL policy";
        return true;
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

    // ---------- 규칙 조율 v2 ----------

    private void EvaluateV2(bool oneShotActive, bool slimesAlive, bool bossAttackable)
    {
        issuedThisTick.Clear();

        // 1. 즉사기 충전 중: 전원 무개입 (v1과 같음)
        if (oneShotActive)
        {
            IssueRemaining(NpcCommandType.Free, "v2 OneShot: all free");
            return;
        }

        NPCSimpleFSMController healer = FindHealer();
        bool noHealer = healer == null;

        // 2. 슬라임 생존 + 보스 무적: 슬라임 전담 배분 (폭발 1회당 지속 장판이 남아 이후 피격 증가)
        if (slimesAlive && !bossAttackable)
        {
            AssignSlimeHandlers(slimeHandlerCountV2);
            IssueCaution(noHealer, false);
            IssueRemaining(NpcCommandType.Free, "v2 AddPhase: slime handlers + caution");
            return;
        }

        // 3. 힐러 보호: 위험 아군이 있을 때만 (임계 0.4)
        PlayerStatus lowest = NpcHealerPolicy.FindLowestHpAlly(out float lowestRatio);
        if (useProtectV2 && healer != null && lowest != null && lowestRatio <= protectHpThresholdV2)
            Issue(healer, NpcCommandType.ProtectAlly, lowest);

        // 4. NPC별 경계 태세
        bool flying = npcs.Count > 0 && npcs[0].bossSkillPattern != null && npcs[0].bossSkillPattern.IsFlying;
        IssueCaution(noHealer, flying);

        // 5. 화력 페이스: 타임아웃 궤적이고 NPC 힐러가 있을 때만 화력 집중
        float progress = 1f - GetBossHpRatio();
        float pace = episodeTimeLimit > 0f ? Time.timeSinceLevelLoad / episodeTimeLimit : 0f;
        if (useFocusV2 && !noHealer && bossAttackable && progress < pace * paceMargin)
        {
            for (int i = 0; i < npcs.Count; i++)
            {
                if (!issuedThisTick.Contains(npcs[i]) && !npcs[i].IsHealerRole)
                    Issue(npcs[i], NpcCommandType.FocusBoss);
            }
            IssueRemaining(NpcCommandType.Free, $"v2 BehindPace: focus boss (progress {progress:0.00} < pace {pace:0.00})");
            return;
        }

        IssueRemaining(NpcCommandType.Free, noHealer ? "v2 NoHealer: caution by hp" : "v2 Normal");
    }

    // 아직 지시를 받지 않은 NPC 중 경계 조건을 만족하는 NPC에게 경계 태세 발행
    private void IssueCaution(bool noHealer, bool flying)
    {
        for (int i = 0; i < npcs.Count; i++)
        {
            NPCSimpleFSMController npc = npcs[i];
            if (issuedThisTick.Contains(npc))
                continue;

            float hp = GetHpRatio(npc);
            bool caution = noHealer
                ? hp < cautionHpNoHealer || (cautionDuringFlyNoHealer && flying)
                : hp < cautionHpWithHealer;

            if (caution)
                Issue(npc, NpcCommandType.HoldDefensive);
        }
    }

    private static float GetHpRatio(NPCSimpleFSMController npc)
    {
        Health health = npc.health != null ? npc.health : npc.GetComponent<Health>();
        if (health == null || health.MaxHealth <= 0f)
            return 1f;
        return Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);
    }

    // 슬라임 전담: 힐러가 아닌 NPC 중 가장 시급한 슬라임과 가까운 순으로 지정
    private void AssignSlimeHandlers()
    {
        AssignSlimeHandlers(slimeHandlerCount);
    }

    private void AssignSlimeHandlers(int count)
    {
        int assigned = 0;
        while (assigned < Mathf.Max(1, count))
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
        // 입실론 탐험: 조율자의 지시를 확률적으로 무작위 지시로 교체 (RL 학습 데이터의 행동 다양성 확보).
        // Rule과 RL 모드 모두 적용 (2회차 이후의 정책 반복은 현재 RL 정책 + 탐험으로 수집).
        // 지시는 편향일 뿐이라 Mask와 안전 행동은 여전히 불가침 (탐험이 생존 로직을 깨지 않음).
        // 지시와 결과는 스냅샷의 command_id 계열 필드로 기록되므로 별도 로그 불필요
        bool explored = false;
        if (mode != CoordinatorMode.None && explorationEpsilon > 0f
            && exploreHold.TryGetValue(npc, out ExploreHold hold) && hold.remaining > 0)
        {
            // 유지 중인 탐험 지시: 남은 횟수만큼 같은 무작위 지시 반복
            hold.remaining--;
            exploreHold[npc] = hold;
            type = hold.type;
            target = type == NpcCommandType.ProtectAlly ? NpcHealerPolicy.FindLowestHpAlly(out _) : null;
            explored = true;
            exploredCommandCount++;
        }
        else if (mode != CoordinatorMode.None && explorationEpsilon > 0f && Random.value < explorationEpsilon)
        {
            type = (NpcCommandType)Random.Range(0, 5);
            exploreHold[npc] = new ExploreHold { type = type, remaining = Mathf.Max(1, explorationHoldDecisions) - 1 };
            target = type == NpcCommandType.ProtectAlly ? NpcHealerPolicy.FindLowestHpAlly(out _) : null;
            explored = true;
            exploredCommandCount++;
        }

        if (logDecisions)
            LogDecision(npc, type, explored);
        pendingQ = null;

        CommandBoard.Issue(npc, type, target);
        issuedThisTick.Add(npc);

        if (logCommands)
            Debug.Log($"[RaidCoordinator] {npc.name} <- {type}{(target != null ? " (" + target.name + ")" : "")}{(explored ? " [explore]" : "")}", this);
    }

    // 지시 반영 전 시점의 관측(직전 지시 포함)과 이번 지시를 기록
    private void LogDecision(NPCSimpleFSMController npc, NpcCommandType type, bool explored)
    {
        if (metricsLogger == null)
            metricsLogger = FindFirstObjectByType<RaidMetricsLogger>();

        CoordinatorObservation.Fill(decisionObs, npc, npcs, episodeTimeLimit);

        int alive = 0, total = 0;
        IReadOnlyList<PlayerStatus> statuses = CombatRegistry.PlayerStatuses;
        for (int i = 0; i < statuses.Count; i++)
        {
            PlayerStatus status = statuses[i];
            if (status == null || status.Health == null)
                continue;
            total++;
            if (!status.Health.IsDead)
                alive++;
        }

        string episodeId = metricsLogger != null ? metricsLogger.episodeId : "";
        string algorithm = metricsLogger != null ? metricsLogger.algorithmName : "";
        CoordinatorDecisionLog.Write(decisionLogRoot, decisionLogFile, episodeId, algorithm, mode, npc,
            decisionObs, type, explored, pendingQ, alive, total);
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

        // 파괴된 NPC(판 재시작으로 교체된 객체)의 탐험 유지 상태 제거
        if (exploreHold.Count > 0)
        {
            staleKeys.Clear();
            foreach (var key in exploreHold.Keys)
                if (key == null)
                    staleKeys.Add(key);
            for (int i = 0; i < staleKeys.Count; i++)
                exploreHold.Remove(staleKeys[i]);
        }

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

    // 생존 캐릭터 중 최저 체력 비율 (즉시 재평가 트리거의 입력)
    private static float GetPartyMinHp()
    {
        IReadOnlyList<PlayerStatus> statuses = CombatRegistry.PlayerStatuses;
        float min = 1f;
        bool any = false;

        for (int i = 0; i < statuses.Count; i++)
        {
            PlayerStatus status = statuses[i];
            if (status == null || status.Health == null || status.Health.IsDead || status.Health.MaxHealth <= 0f)
                continue;

            min = Mathf.Min(min, status.Health.CurrentHealth / status.Health.MaxHealth);
            any = true;
        }

        return any ? min : 1f;
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
