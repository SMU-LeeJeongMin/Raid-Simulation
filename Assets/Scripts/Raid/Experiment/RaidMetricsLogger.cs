using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// 레이드 1회 실행 결과를 CSV로 저장합니다.
/// BasicFSM / PatternAwareFSM / Utility / GAT 비교를 위해 전투 이벤트를 누적 수집합니다.
/// </summary>
public class RaidMetricsLogger : MonoBehaviour
{
    [Header("Experiment")]
    public string algorithmName = "PatternAwareFSM";
    public string episodeId = "";
    public bool autoStartOnAwake = true;
    public bool writeOnBossDeath = true;
    public bool writeOnPartyWipe = true;
    [Tooltip("켜두면 보스 사망 또는 파티 전멸처럼 전투가 실제로 끝난 경우에만 CSV를 기록합니다. ApplicationQuit/Timeout/Player 단독 사망은 기록하지 않습니다.")]
    public bool recordOnlyTerminalOutcomes = true;
    [Min(1)] public int requiredPartyMemberCountForWipe = 4;
    public bool writeOnPlayerDeath = false;
    public bool writeOnApplicationQuit = false;
    public bool writeOnTimeout = false;
    public float maxEpisodeDuration = 600f;

    [Header("Low HP / Heal Quality")]
    [Range(0.05f, 0.95f)] public float lowHpThreshold = 0.5f;
    public float healResponseWindow = 5f;

    [Header("Auto Bind")]
    public bool autoBind = true;
    public float rebindInterval = 1f;

    [Header("Output")]
    public string fileName = "raid_episode_metrics.csv";
    public bool logPathOnStart = true;

    [Header("Runtime Debug")]
    [SerializeField] private bool episodeRunning;
    [SerializeField] private bool resultWritten;
    [SerializeField] private float episodeStartTime;
    [SerializeField] private string outputPath;

    [Header("Runtime Totals")]
    [SerializeField] private float playerDamageTaken;
    [SerializeField] private float npcDamageTaken;
    [SerializeField] private float playerRawDamageTaken;
    [SerializeField] private float npcRawDamageTaken;
    [SerializeField] private float shieldAbsorbedAmount;
    [SerializeField] private int shieldAbsorbEvents;
    [SerializeField] private int shieldPreventedDeathCount;
    [SerializeField] private int playerLowHpEvents;
    [SerializeField] private int npcLowHpEvents;
    [SerializeField] private int playerPatternHitCount;
    [SerializeField] private int npcPatternHitCount;
    [SerializeField] private int playerDeathCount;
    [SerializeField] private int npcDeathCount;
    [SerializeField] private int bossDeathCount;
    [SerializeField] private int damageEvents;
    [SerializeField] private int activeDangerZoneCountAtEnd;

    private readonly HashSet<Health> subscribedHealths = new HashSet<Health>();
    private readonly HashSet<Health> partyHealths = new HashSet<Health>();
    private readonly Dictionary<Health, float> lastLowHpEnterTime = new Dictionary<Health, float>();
    private readonly Dictionary<string, int> skillFailReasons = new Dictionary<string, int>();
    private readonly Dictionary<string, int> npcActionCounts = new Dictionary<string, int>();

    private Health bossHealth;
    private float nextBindTime;
    private bool autoGenerateEpisodeId;

    // Heal / shield quality
    private int healEvents;
    private float healRequestedAmount;
    private float healActualAmount;
    private float overhealAmount;
    private int playerHealEvents;
    private int npcHealEvents;
    private float playerHealAmount;
    private float npcHealAmount;
    private int lowHpHealEvents;
    private int playerLowHpHealEvents;
    private int npcLowHpHealEvents;
    private int lowHpRecoveryEvents;
    private int healResponseEvents;
    private float healResponseTimeTotal;
    private int shieldAddedEvents;
    private float shieldAddedAmount;

    // Attack / skill usage
    private int basicAttackHitCount;
    private int basicAttackMissCount;
    private int playerBasicAttackHitCount;
    private int npcBasicAttackHitCount;
    private int skillAttemptCount;
    private int skillCastCount;
    private int skillFailCount;
    private int playerSkillCastCount;
    private int npcSkillCastCount;
    private int offensiveSkillCastCount;
    private int supportSkillCastCount;
    private int ultimateAttemptCount;
    private int ultimateCastCount;
    private int ultimateFailCount;
    private int ultimateOnBossCount;
    private int ultimateOnSlimeCount;
    private int ultimateOnImmuneTargetCount;
    private int ultimateWhileBossImmuneCount;
    private int ultimateWastedCount;

    // Slime/Add phase
    private int slimeSpawnCount;
    private int slimeKilledCount;
    private int slimeExplodedCount;
    private int slimeDotZoneCreatedCount;

    // NPC action decisions
    private int npcMoveToSafePositionCount;
    private int npcFollowPlayerCount;
    private int npcMoveToBossCount;
    private int npcKeepDistanceCount;
    private int npcRegroupDuringBossFlyCount;
    private int npcMoveToAllyAndHealCount;
    private int npcShieldDangerAllyCount;

    private void Awake()
    {
        outputPath = Path.Combine(Application.persistentDataPath, fileName);

        // 인스펙터에서 id를 비워둔 경우 에피소드 시작마다 자동 생성
        autoGenerateEpisodeId = string.IsNullOrWhiteSpace(episodeId);

        if (logPathOnStart)
            Debug.Log($"[RaidMetricsLogger] Output: {outputPath}", this);

        SubscribeMetricEvents();

        if (autoStartOnAwake)
            StartEpisode();
    }

    private void OnEnable()
    {
        SubscribeMetricEvents();
    }

    private void OnDisable()
    {
        UnsubscribeMetricEvents();
        UnsubscribeHealthListeners();
    }

    private void Update()
    {
        if (!episodeRunning)
            return;

        if (autoBind && Time.time >= nextBindTime)
        {
            nextBindTime = Time.time + Mathf.Max(0.1f, rebindInterval);
            BindCurrentCombatObjects();
        }

        if (!resultWritten && !recordOnlyTerminalOutcomes && writeOnTimeout && maxEpisodeDuration > 0f && Time.time - episodeStartTime >= maxEpisodeDuration)
            FinishEpisode(false, "timeout");
    }

    private void OnApplicationQuit()
    {
        if (!recordOnlyTerminalOutcomes && writeOnApplicationQuit && episodeRunning && !resultWritten)
            FinishEpisode(false, "application_quit");
    }

    private void SubscribeMetricEvents()
    {
        UnsubscribeMetricEvents();
        RaidMetricsEvents.PlayerDamageResolved += OnPlayerDamageResolved;
        RaidMetricsEvents.PlayerHealed += OnPlayerHealed;
        RaidMetricsEvents.ShieldAdded += OnShieldAdded;
        RaidMetricsEvents.SkillAttempted += OnSkillAttempted;
        RaidMetricsEvents.SkillFailed += OnSkillFailed;
        RaidMetricsEvents.SkillCast += OnSkillCast;
        RaidMetricsEvents.BasicAttackHit += OnBasicAttackHit;
        RaidMetricsEvents.BasicAttackMissed += OnBasicAttackMissed;
        RaidMetricsEvents.NPCActionSelected += OnNPCActionSelected;
        RaidMetricsEvents.SlimeSpawned += OnSlimeSpawned;
        RaidMetricsEvents.SlimeKilled += OnSlimeKilled;
        RaidMetricsEvents.SlimeExploded += OnSlimeExploded;
        RaidMetricsEvents.SlimeDotZoneCreated += OnSlimeDotZoneCreated;
    }

    private void UnsubscribeMetricEvents()
    {
        RaidMetricsEvents.PlayerDamageResolved -= OnPlayerDamageResolved;
        RaidMetricsEvents.PlayerHealed -= OnPlayerHealed;
        RaidMetricsEvents.ShieldAdded -= OnShieldAdded;
        RaidMetricsEvents.SkillAttempted -= OnSkillAttempted;
        RaidMetricsEvents.SkillFailed -= OnSkillFailed;
        RaidMetricsEvents.SkillCast -= OnSkillCast;
        RaidMetricsEvents.BasicAttackHit -= OnBasicAttackHit;
        RaidMetricsEvents.BasicAttackMissed -= OnBasicAttackMissed;
        RaidMetricsEvents.NPCActionSelected -= OnNPCActionSelected;
        RaidMetricsEvents.SlimeSpawned -= OnSlimeSpawned;
        RaidMetricsEvents.SlimeKilled -= OnSlimeKilled;
        RaidMetricsEvents.SlimeExploded -= OnSlimeExploded;
        RaidMetricsEvents.SlimeDotZoneCreated -= OnSlimeDotZoneCreated;
    }

    [ContextMenu("Start Episode")]
    public void StartEpisode()
    {
        episodeRunning = true;
        resultWritten = false;
        episodeStartTime = Time.time;

        // 같은 세션에서 여러 에피소드를 돌려도 행이 고유하도록 id 재생성
        if (autoGenerateEpisodeId || string.IsNullOrWhiteSpace(episodeId))
            episodeId = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

        playerDamageTaken = 0f;
        npcDamageTaken = 0f;
        playerRawDamageTaken = 0f;
        npcRawDamageTaken = 0f;
        shieldAbsorbedAmount = 0f;
        shieldAbsorbEvents = 0;
        shieldPreventedDeathCount = 0;
        playerLowHpEvents = 0;
        npcLowHpEvents = 0;
        playerPatternHitCount = 0;
        npcPatternHitCount = 0;
        playerDeathCount = 0;
        npcDeathCount = 0;
        bossDeathCount = 0;
        damageEvents = 0;
        activeDangerZoneCountAtEnd = 0;

        healEvents = 0;
        healRequestedAmount = 0f;
        healActualAmount = 0f;
        overhealAmount = 0f;
        playerHealEvents = 0;
        npcHealEvents = 0;
        playerHealAmount = 0f;
        npcHealAmount = 0f;
        lowHpHealEvents = 0;
        playerLowHpHealEvents = 0;
        npcLowHpHealEvents = 0;
        lowHpRecoveryEvents = 0;
        healResponseEvents = 0;
        healResponseTimeTotal = 0f;
        shieldAddedEvents = 0;
        shieldAddedAmount = 0f;

        basicAttackHitCount = 0;
        basicAttackMissCount = 0;
        playerBasicAttackHitCount = 0;
        npcBasicAttackHitCount = 0;
        skillAttemptCount = 0;
        skillCastCount = 0;
        skillFailCount = 0;
        playerSkillCastCount = 0;
        npcSkillCastCount = 0;
        offensiveSkillCastCount = 0;
        supportSkillCastCount = 0;
        ultimateAttemptCount = 0;
        ultimateCastCount = 0;
        ultimateFailCount = 0;
        ultimateOnBossCount = 0;
        ultimateOnSlimeCount = 0;
        ultimateOnImmuneTargetCount = 0;
        ultimateWhileBossImmuneCount = 0;
        ultimateWastedCount = 0;

        slimeSpawnCount = 0;
        slimeKilledCount = 0;
        slimeExplodedCount = 0;
        slimeDotZoneCreatedCount = 0;

        npcMoveToSafePositionCount = 0;
        npcFollowPlayerCount = 0;
        npcMoveToBossCount = 0;
        npcKeepDistanceCount = 0;
        npcRegroupDuringBossFlyCount = 0;
        npcMoveToAllyAndHealCount = 0;
        npcShieldDangerAllyCount = 0;

        lastLowHpEnterTime.Clear();
        partyHealths.Clear();
        skillFailReasons.Clear();
        npcActionCounts.Clear();

        BindCurrentCombatObjects();
    }

    [ContextMenu("Finish Episode Manually")]
    public void FinishEpisodeManually()
    {
        FinishEpisode(bossDeathCount > 0, "manual");
    }

    private void BindCurrentCombatObjects()
    {
        PlayerStatus[] statuses = FindObjectsByType<PlayerStatus>(FindObjectsSortMode.None);
        for (int i = 0; i < statuses.Length; i++)
        {
            PlayerStatus status = statuses[i];
            if (status == null)
                continue;

            status.ResolveReferences();
            RegisterPartyHealth(status.Health);
            Subscribe(status.Health);
        }

        BossDummyController boss = FindFirstObjectByType<BossDummyController>();
        if (boss != null)
        {
            bossHealth = boss.health != null ? boss.health : boss.GetComponent<Health>();
            Subscribe(bossHealth);
        }
    }

    private void Subscribe(Health health)
    {
        if (health == null || subscribedHealths.Contains(health))
            return;

        health.EnsureEvents();
        subscribedHealths.Add(health);
        health.onDeath.AddListener(OnHealthDeath);
    }

    // 구독한 사망 이벤트 리스너 해제 (씬 재시작 시 리스너 누적 방지)
    private void UnsubscribeHealthListeners()
    {
        foreach (Health health in subscribedHealths)
        {
            if (health != null)
                health.onDeath.RemoveListener(OnHealthDeath);
        }

        subscribedHealths.Clear();
    }

    private void RegisterPartyHealth(Health health)
    {
        if (health == null)
            return;

        if (health.GetComponent<PlayerStatus>() == null)
            return;

        if (health.GetComponent<BossDummyController>() != null)
            return;

        partyHealths.Add(health);
    }

    private void OnPlayerDamageResolved(PlayerStatus status, float rawDamage, float shieldAbsorbed, float hpDamage, float hpBefore, float hpAfter, GameObject source)
    {
        if (!episodeRunning || resultWritten || status == null)
            return;

        damageEvents++;

        bool isNPC = IsNPC(status);
        if (isNPC)
        {
            npcRawDamageTaken += rawDamage;
            npcDamageTaken += hpDamage;
        }
        else
        {
            playerRawDamageTaken += rawDamage;
            playerDamageTaken += hpDamage;
        }

        if (shieldAbsorbed > 0f)
        {
            shieldAbsorbEvents++;
            shieldAbsorbedAmount += shieldAbsorbed;

            if (hpBefore > 0f && rawDamage >= hpBefore && hpAfter > 0f)
                shieldPreventedDeathCount++;
        }

        if (DangerZoneRegistry.IsPointInAnyZone(status.transform.position, 0.1f))
        {
            if (isNPC)
                npcPatternHitCount++;
            else
                playerPatternHitCount++;
        }

        float maxHp = status.Health != null ? Mathf.Max(1f, status.Health.MaxHealth) : Mathf.Max(1f, hpBefore);
        float thresholdValue = maxHp * Mathf.Clamp01(lowHpThreshold);
        bool wasLow = hpBefore <= thresholdValue;
        bool isLow = hpAfter <= thresholdValue;

        if (!wasLow && isLow)
        {
            if (isNPC)
                npcLowHpEvents++;
            else
                playerLowHpEvents++;

            if (status.Health != null)
            {
                lastLowHpEnterTime[status.Health] = Time.time;
            }
        }
    }

    private void OnPlayerHealed(PlayerStatus status, float requestedHeal, float actualHeal, float hpBefore, float hpAfter, GameObject source)
    {
        if (!episodeRunning || resultWritten || status == null)
            return;

        healEvents++;
        healRequestedAmount += Mathf.Max(0f, requestedHeal);
        healActualAmount += Mathf.Max(0f, actualHeal);
        overhealAmount += Mathf.Max(0f, requestedHeal - actualHeal);

        bool isNPC = IsNPC(status);
        if (isNPC)
        {
            npcHealEvents++;
            npcHealAmount += actualHeal;
        }
        else
        {
            playerHealEvents++;
            playerHealAmount += actualHeal;
        }

        float maxHp = status.Health != null ? Mathf.Max(1f, status.Health.MaxHealth) : Mathf.Max(1f, hpAfter);
        float thresholdValue = maxHp * Mathf.Clamp01(lowHpThreshold);
        bool healedWhileLow = hpBefore <= thresholdValue;
        if (healedWhileLow)
        {
            lowHpHealEvents++;
            if (isNPC)
                npcLowHpHealEvents++;
            else
                playerLowHpHealEvents++;
        }

        if (healedWhileLow && hpAfter > thresholdValue)
            lowHpRecoveryEvents++;

        if (status.Health != null && lastLowHpEnterTime.TryGetValue(status.Health, out float lowStart))
        {
            float response = Time.time - lowStart;
            if (response <= Mathf.Max(0.1f, healResponseWindow))
            {
                healResponseEvents++;
                healResponseTimeTotal += response;
            }

            if (hpAfter > thresholdValue)
            {
                lastLowHpEnterTime.Remove(status.Health);
            }
        }
    }

    private void OnShieldAdded(PlayerStatus status, float amount, float duration, GameObject source)
    {
        if (!episodeRunning || resultWritten || status == null)
            return;

        shieldAddedEvents++;
        shieldAddedAmount += Mathf.Max(0f, amount);
    }

    private void OnSkillAttempted(PlayerSkillController controller, PlayerSkillDefinition skill, PlayerSkillSlot slot)
    {
        if (!episodeRunning || resultWritten)
            return;

        skillAttemptCount++;
        if (slot == PlayerSkillSlot.Ultimate)
            ultimateAttemptCount++;
    }

    private void OnSkillFailed(PlayerSkillController controller, PlayerSkillDefinition skill, PlayerSkillSlot slot, string reason)
    {
        if (!episodeRunning || resultWritten)
            return;

        skillFailCount++;
        if (slot == PlayerSkillSlot.Ultimate)
            ultimateFailCount++;

        reason = string.IsNullOrWhiteSpace(reason) ? "unknown" : reason;
        if (!skillFailReasons.ContainsKey(reason))
            skillFailReasons[reason] = 0;
        skillFailReasons[reason]++;
    }

    private void OnSkillCast(PlayerSkillController controller, PlayerSkillDefinition skill, PlayerSkillSlot slot, bool offensive, DamageReceiver target)
    {
        if (!episodeRunning || resultWritten || skill == null)
            return;

        skillCastCount++;
        if (controller != null && controller.GetComponent<NPCPartyMember>() != null)
            npcSkillCastCount++;
        else
            playerSkillCastCount++;

        if (offensive)
            offensiveSkillCastCount++;
        else
            supportSkillCastCount++;

        if (slot == PlayerSkillSlot.Ultimate || skill.ultimateCost > 0f)
        {
            ultimateCastCount++;
            Health targetHealth = ResolveTargetHealth(target);
            bool targetSlime = targetHealth != null && targetHealth.GetComponent<SlimeAddEnemy>() != null;
            bool targetBoss = targetHealth != null && targetHealth.GetComponent<BossDummyController>() != null;
            bool targetImmune = targetHealth != null && targetHealth.DamageImmune;
            bool bossImmune = bossHealth != null && bossHealth.DamageImmune;

            if (targetBoss)
                ultimateOnBossCount++;
            if (targetSlime)
                ultimateOnSlimeCount++;
            if (targetImmune)
                ultimateOnImmuneTargetCount++;
            if (bossImmune)
                ultimateWhileBossImmuneCount++;

            // 보스 무적 중 궁극기를 쓰거나, 슬라임 처리용으로 궁극기를 쓰면 현재 실험에서는 효율이 낮은 사용으로 기록합니다.
            if (targetSlime || targetImmune || bossImmune || (offensive && target == null))
                ultimateWastedCount++;
        }
    }

    private void OnBasicAttackHit(PlayerBasicAttack attacker, BasicAttackStats stats, DamageReceiver target)
    {
        if (!episodeRunning || resultWritten)
            return;

        basicAttackHitCount++;
        if (attacker != null && attacker.GetComponent<NPCPartyMember>() != null)
            npcBasicAttackHitCount++;
        else
            playerBasicAttackHitCount++;
    }

    private void OnBasicAttackMissed(PlayerBasicAttack attacker, BasicAttackStats stats, string reason)
    {
        if (!episodeRunning || resultWritten)
            return;

        basicAttackMissCount++;
    }

    private void OnNPCActionSelected(NPCSimpleFSMController controller, string actionName)
    {
        if (!episodeRunning || resultWritten || string.IsNullOrWhiteSpace(actionName))
            return;

        if (!npcActionCounts.ContainsKey(actionName))
            npcActionCounts[actionName] = 0;
        npcActionCounts[actionName]++;

        // 액션 이름 상수와 정확히 일치하는 경우만 집계 (Contains 부분 일치로 인한 오집계 방지)
        switch (actionName)
        {
            case NpcActionNames.MoveToSafePosition:
                npcMoveToSafePositionCount++;
                break;
            case NpcActionNames.FollowPlayer:
                npcFollowPlayerCount++;
                break;
            case NpcActionNames.MoveToBoss:
                npcMoveToBossCount++;
                break;
            case NpcActionNames.KeepDistance:
                npcKeepDistanceCount++;
                break;
            case NpcActionNames.RegroupDuringBossFly:
                npcRegroupDuringBossFlyCount++;
                break;
            case NpcActionNames.MoveToAllyAndHeal:
                npcMoveToAllyAndHealCount++;
                break;
            case NpcActionNames.ShieldDangerAlly:
                npcShieldDangerAllyCount++;
                break;
        }
    }

    private void OnSlimeSpawned(SlimeAddEnemy slime)
    {
        if (!episodeRunning || resultWritten)
            return;
        slimeSpawnCount++;
    }

    private void OnSlimeKilled(SlimeAddEnemy slime)
    {
        if (!episodeRunning || resultWritten)
            return;
        slimeKilledCount++;
    }

    private void OnSlimeExploded(SlimeAddEnemy slime)
    {
        if (!episodeRunning || resultWritten)
            return;
        slimeExplodedCount++;
    }

    private void OnSlimeDotZoneCreated(SlimeDotZone zone)
    {
        if (!episodeRunning || resultWritten)
            return;
        slimeDotZoneCreatedCount++;
    }

    private void OnHealthDeath(Health deadHealth)
    {
        if (!episodeRunning || resultWritten || deadHealth == null)
            return;

        if (deadHealth == bossHealth || deadHealth.GetComponent<BossDummyController>() != null)
        {
            bossDeathCount++;
            if (writeOnBossDeath)
                FinishEpisode(true, "boss_dead");
            return;
        }

        PlayerStatus status = deadHealth.GetComponent<PlayerStatus>();
        if (status != null)
        {
            RegisterPartyHealth(deadHealth);

            if (IsNPC(status))
                npcDeathCount++;
            else
                playerDeathCount++;

            if (writeOnPartyWipe && IsPartyWipedOut())
            {
                FinishEpisode(false, "party_wipe");
                return;
            }

            if (!recordOnlyTerminalOutcomes && !IsNPC(status) && writeOnPlayerDeath)
                FinishEpisode(false, "player_dead");
        }
    }

    private bool IsPartyWipedOut()
    {
        RefreshPartyHealthSet();

        int trackedCount = 0;
        int aliveCount = 0;

        List<Health> invalid = null;
        foreach (Health health in partyHealths)
        {
            if (health == null)
            {
                invalid ??= new List<Health>();
                invalid.Add(health);
                continue;
            }

            if (health.GetComponent<PlayerStatus>() == null || health.GetComponent<BossDummyController>() != null)
                continue;

            trackedCount++;
            if (!health.IsDead)
                aliveCount++;
        }

        if (invalid != null)
        {
            for (int i = 0; i < invalid.Count; i++)
                partyHealths.Remove(invalid[i]);
        }

        return trackedCount >= Mathf.Max(1, requiredPartyMemberCountForWipe) && aliveCount == 0;
    }

    private void RefreshPartyHealthSet()
    {
        PlayerStatus[] statuses = FindObjectsByType<PlayerStatus>(FindObjectsSortMode.None);
        for (int i = 0; i < statuses.Length; i++)
        {
            PlayerStatus status = statuses[i];
            if (status == null)
                continue;

            status.ResolveReferences();
            RegisterPartyHealth(status.Health);
        }
    }

    public void FinishEpisode(bool clearSuccess, string endReason)
    {
        if (resultWritten)
            return;

        // 이벤트 핸들러 재진입 방지를 위한 선차단
        resultWritten = true;
        episodeRunning = false;
        activeDangerZoneCountAtEnd = DangerZoneRegistry.Count;

        string playerClass = ResolvePlayerClass();
        float elapsed = Time.time - episodeStartTime;
        float bossHpRatio = bossHealth != null && bossHealth.MaxHealth > 0f ? bossHealth.CurrentHealth / bossHealth.MaxHealth : -1f;
        float avgHealResponseTime = healResponseEvents > 0 ? healResponseTimeTotal / healResponseEvents : -1f;

        // 헤더와 값을 같은 목록에서 생성하여 컬럼 밀림 원천 차단
        List<KeyValuePair<string, string>> columns = BuildCsvColumns(clearSuccess, endReason, playerClass, elapsed, bossHpRatio, avgHealResponseTime);
        string valueLine = JoinColumns(columns, false);

        try
        {
            WriteEpisodeRow(columns, valueLine);
            Debug.Log($"[RaidMetricsLogger] Episode saved: {outputPath}", this);
        }
        catch (Exception e)
        {
            // CSV가 다른 프로그램(예: Excel)에 열려 있으면 쓰기가 실패하므로 데이터를 로그에 남겨 유실 방지
            Debug.LogError($"[RaidMetricsLogger] CSV 기록 실패: {e.Message}\n복구용 데이터 행: {valueLine}", this);
        }
    }

    // CSV 한 행의 (헤더, 값) 쌍을 단일 지점에서 정의
    private List<KeyValuePair<string, string>> BuildCsvColumns(bool clearSuccess, string endReason, string playerClass, float elapsed, float bossHpRatio, float avgHealResponseTime)
    {
        List<KeyValuePair<string, string>> columns = new List<KeyValuePair<string, string>>(48);
        void Add(string header, string value) => columns.Add(new KeyValuePair<string, string>(header, value));

        Add("episode_id", Escape(episodeId));
        Add("algorithm", Escape(algorithmName));
        Add("player_class", Escape(playerClass));
        Add("clear_success", clearSuccess ? "1" : "0");
        Add("clear_time", FormatFloat(elapsed));
        Add("end_reason", Escape(endReason));
        Add("boss_hp_ratio", FormatFloat(bossHpRatio));
        Add("player_damage_taken", FormatFloat(playerDamageTaken));
        Add("npc_damage_taken", FormatFloat(npcDamageTaken));
        Add("shield_absorbed_amount", FormatFloat(shieldAbsorbedAmount));
        Add("shield_absorb_events", FormatInt(shieldAbsorbEvents));
        Add("shield_added_events", FormatInt(shieldAddedEvents));
        Add("player_low_hp_events", FormatInt(playerLowHpEvents));
        Add("npc_low_hp_events", FormatInt(npcLowHpEvents));
        Add("player_pattern_hit_count", FormatInt(playerPatternHitCount));
        Add("npc_pattern_hit_count", FormatInt(npcPatternHitCount));
        Add("player_death_count", FormatInt(playerDeathCount));
        Add("npc_death_count", FormatInt(npcDeathCount));
        Add("boss_death_count", FormatInt(bossDeathCount));
        Add("heal_events", FormatInt(healEvents));
        Add("heal_actual_amount", FormatFloat(healActualAmount));
        Add("overheal_amount", FormatFloat(overhealAmount));
        Add("player_heal_events", FormatInt(playerHealEvents));
        Add("player_heal_amount", FormatFloat(playerHealAmount));
        Add("npc_heal_events", FormatInt(npcHealEvents));
        Add("npc_heal_amount", FormatFloat(npcHealAmount));
        Add("player_low_hp_heal_events", FormatInt(playerLowHpHealEvents));
        Add("npc_low_hp_heal_events", FormatInt(npcLowHpHealEvents));
        Add("avg_heal_response_time", FormatFloat(avgHealResponseTime));
        Add("skill_fail_count", FormatInt(skillFailCount));
        Add("ultimate_on_boss_count", FormatInt(ultimateOnBossCount));
        Add("ultimate_on_slime_count", FormatInt(ultimateOnSlimeCount));
        Add("ultimate_while_boss_immune_count", FormatInt(ultimateWhileBossImmuneCount));
        Add("slime_spawn_count", FormatInt(slimeSpawnCount));
        Add("slime_killed_count", FormatInt(slimeKilledCount));
        Add("slime_dot_zone_created_count", FormatInt(slimeDotZoneCreatedCount));
        Add("npc_move_to_safe_position_count", FormatInt(npcMoveToSafePositionCount));
        Add("npc_keep_distance_count", FormatInt(npcKeepDistanceCount));
        Add("npc_move_to_ally_and_heal_count", FormatInt(npcMoveToAllyAndHealCount));
        Add("npc_shield_danger_ally_count", FormatInt(npcShieldDangerAllyCount));
        Add("skill_failure_summary", Escape(GetSkillFailureSummary()));
        Add("npc_action_summary", Escape(GetNPCActionSummary()));

        return columns;
    }

    private static string JoinColumns(List<KeyValuePair<string, string>> columns, bool useHeader)
    {
        List<string> parts = new List<string>(columns.Count);
        for (int i = 0; i < columns.Count; i++)
            parts.Add(useHeader ? columns[i].Key : columns[i].Value);

        return string.Join(",", parts);
    }

    private void WriteEpisodeRow(List<KeyValuePair<string, string>> columns, string valueLine)
    {
        string headerLine = JoinColumns(columns, true);

        FileInfo fileInfo = new FileInfo(outputPath);
        bool needHeader = !fileInfo.Exists || fileInfo.Length == 0;

        if (!needHeader)
        {
            // 기존 파일이 구버전 컬럼 구성일 때 데이터가 어긋난 채 쌓이는 것을 감지
            string existingHeader = ReadFirstLine(outputPath);
            if (!string.Equals(existingHeader, headerLine, StringComparison.Ordinal))
                Debug.LogWarning("[RaidMetricsLogger] 기존 CSV의 헤더가 현재 컬럼 정의와 불일치. 새 파일명 사용 권장", this);
        }

        string content = needHeader
            ? headerLine + Environment.NewLine + valueLine + Environment.NewLine
            : valueLine + Environment.NewLine;

        File.AppendAllText(outputPath, content);
    }

    private static string ReadFirstLine(string path)
    {
        using (StreamReader reader = new StreamReader(path))
            return reader.ReadLine();
    }

    private string ResolvePlayerClass()
    {
        RaidSelectedCharacterSpawner spawner = FindFirstObjectByType<RaidSelectedCharacterSpawner>();
        if (spawner != null && spawner.SpawnedPlayer != null)
        {
            PlayerClassInfo info = spawner.SpawnedPlayer.GetComponent<PlayerClassInfo>();
            if (info != null && !string.IsNullOrWhiteSpace(info.characterId))
                return info.characterId;
        }

        return SelectedCharacterMemory.LoadSelectedId("unknown");
    }

    private bool IsNPC(PlayerStatus status)
    {
        return status != null && status.GetComponent<NPCPartyMember>() != null;
    }

    private Health ResolveTargetHealth(DamageReceiver target)
    {
        if (target == null)
            return null;

        if (target.targetHealth != null)
            return target.targetHealth;

        return target.GetComponentInParent<Health>();
    }

    private string GetSkillFailureSummary()
    {
        if (skillFailReasons.Count == 0)
            return string.Empty;

        List<string> parts = new List<string>();
        foreach (KeyValuePair<string, int> pair in skillFailReasons)
            parts.Add(pair.Key + ":" + pair.Value.ToString(CultureInfo.InvariantCulture));

        return string.Join("|", parts);
    }

    private string GetNPCActionSummary()
    {
        if (npcActionCounts.Count == 0)
            return string.Empty;

        List<string> parts = new List<string>();
        foreach (KeyValuePair<string, int> pair in npcActionCounts)
            parts.Add(pair.Key + ":" + pair.Value.ToString(CultureInfo.InvariantCulture));

        return string.Join("|", parts);
    }

    private string FormatFloat(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return "";

        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private string FormatInt(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private string Escape(string value)
    {
        if (value == null)
            value = string.Empty;

        value = value.Replace("\"", "\"\"");
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("|"))
            return "\"" + value + "\"";

        return value;
    }
}
