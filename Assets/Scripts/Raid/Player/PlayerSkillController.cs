// 플레이어 스킬 실행

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerSkillController : MonoBehaviour
{
    [Header("Database")]
    public PlayerSkillDatabase skillDatabase;
    public PlayerClassInfo classInfo;
    public string characterIdOverride = "";

    [Header("References")]
    public Animator animator;
    public PlayerMovement playerMovement;
    public PlayerBasicAttack basicAttack;
    public PlayerStatus playerStatus;
    public Mana mana;
    public PlayerUltimateGauge ultimateGauge;
    public PlayerSkillVFXPlayer VFXPlayer;

    [Header("Boss Phase Rules")]
    public bool blockOffensiveSkillsWhileBossFlying = true;

    [Header("Input")]
    public bool useKeyboardInput = true;
    public bool ignoreInputWhenPointerIsOverUI = true;
#if ENABLE_INPUT_SYSTEM
    public Key skill1Key = Key.Digit2;
    public Key skill2Key = Key.Digit3;
    public Key ultimateKey = Key.Digit4;
#else
    public KeyCode skill1Key = KeyCode.Alpha2;
    public KeyCode skill2Key = KeyCode.Alpha3;
    public KeyCode ultimateKey = KeyCode.Alpha4;
#endif

    [Header("Animation Playback")]
    public bool forceOneShotSkillAnimation = true;
    public bool findAnimationClipByStateName = true;
    public bool warnWhenSkillClipMissing = true;

    [Header("Runtime Debug")]
    [SerializeField] private string resolvedCharacterId;
    [SerializeField] private bool skillInProgress;
    [SerializeField] private string currentSkillInfo;
    [SerializeField] private string currentSkillAnimationInfo;
    [SerializeField] private float skill1ReadyTime;
    [SerializeField] private float skill2ReadyTime;
    [SerializeField] private float ultimateReadyTime;
    [SerializeField] private PlayerSkillSlot activeSkillSlot = PlayerSkillSlot.Skill1;

    // 스킬 클립 직접 재생을 담당하는 공용 플레이어
    private SingleClipPlayer skillClipPlayer;
    private SingleClipPlayer SkillClipPlayer => skillClipPlayer ??= new SingleClipPlayer(name + "_SkillGraph", "Skill");

    private float originalAnimatorSpeed = 1f;
    private bool originalRootMotion;
    private bool hasOriginalAnimatorValues;
    private float originalMoveSpeed;
    private bool hasOriginalMoveSpeed;
    private Coroutine skillAnimationStopRoutine;

    public bool IsSkillInProgress => skillInProgress;
    public PlayerSkillSlot ActiveSkillSlot => activeSkillSlot;
    public PlayerUltimateGauge UltimateGauge => ultimateGauge;
    public Mana Mana => mana;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (!useKeyboardInput)
            return;

        if (ignoreInputWhenPointerIsOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (WasKeyPressed(skill1Key))
            TryUseSkill(PlayerSkillSlot.Skill1);
        else if (WasKeyPressed(skill2Key))
            TryUseSkill(PlayerSkillSlot.Skill2);
        else if (WasKeyPressed(ultimateKey))
            TryUseSkill(PlayerSkillSlot.Ultimate);
    }

#if ENABLE_INPUT_SYSTEM
    private bool WasKeyPressed(Key key)
    {
        return Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
    }
#else
    private bool WasKeyPressed(KeyCode key)
    {
        return Input.GetKeyDown(key);
    }
#endif

    public void ResolveReferences()
    {
        if (classInfo == null)
            classInfo = GetComponent<PlayerClassInfo>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (basicAttack == null)
            basicAttack = GetComponent<PlayerBasicAttack>();

        if (playerStatus == null)
            playerStatus = GetComponent<PlayerStatus>();

        if (playerStatus != null)
        {
            playerStatus.ResolveReferences();
            if (mana == null)
                mana = playerStatus.Mana;
            if (ultimateGauge == null)
                ultimateGauge = playerStatus.UltimateGauge;
        }

        if (mana == null)
            mana = GetComponent<Mana>();

        if (ultimateGauge == null)
            ultimateGauge = GetComponent<PlayerUltimateGauge>();

        if (VFXPlayer == null)
            VFXPlayer = GetComponent<PlayerSkillVFXPlayer>();
    }

    public bool TryUseSkill1()
    {
        return TryUseSkill(PlayerSkillSlot.Skill1);
    }

    public bool TryUseSkill2()
    {
        return TryUseSkill(PlayerSkillSlot.Skill2);
    }

    public bool TryUseUltimate()
    {
        return TryUseSkill(PlayerSkillSlot.Ultimate);
    }

    public bool TryUseSkill(PlayerSkillSlot slot)
    {
        // 실제 사용과 UI 표시가 동일한 검증 규칙 공유 (규칙 불일치로 인한 표류 방지)
        if (!ValidateSkillUse(slot, requireTarget: true, out _, out PlayerSkillDefinition skill, out DamageReceiver target))
            return false;

        if (skill.manaCost > 0f)
            mana.Consume(skill.manaCost);

        if (skill.ultimateCost > 0f)
            ultimateGauge.Consume(skill.ultimateCost);

        SetReadyTime(slot, Time.time + skill.cooldown);
        StartCoroutine(SkillRoutine(skill, target));
        return true;
    }

    // 스킬 사용 가능 여부의 단일 검증
    private bool ValidateSkillUse(PlayerSkillSlot slot, bool requireTarget, out string reason, out PlayerSkillDefinition skill, out DamageReceiver target)
    {
        target = null;
        skill = null;
        ResolveReferences();

        if (skillInProgress)
        {
            reason = activeSkillSlot == slot ? "CastingCurrent" : "CastingOther";
            return false;
        }

        skill = ResolveSkill(slot);
        if (skill == null)
        {
            reason = "NoSkill";
            return false;
        }

        if (Time.time < GetReadyTime(slot))
        {
            reason = "Cooldown";
            return false;
        }

        bool offensive = IsOffensiveSkill(skill);

        if (offensive && blockOffensiveSkillsWhileBossFlying && IsAnyBossFlying())
        {
            reason = "BossFlying";
            return false;
        }

        if (skill.manaCost > 0f && (mana == null || !mana.CanConsume(skill.manaCost)))
        {
            reason = "NotEnoughMana";
            return false;
        }

        if (skill.ultimateCost > 0f && (ultimateGauge == null || !ultimateGauge.CanConsume(skill.ultimateCost)))
        {
            reason = "NotEnoughUltimate";
            return false;
        }

        if (offensive && requireTarget)
        {
            if (basicAttack == null)
            {
                reason = "NoTarget";
                return false;
            }

            target = basicAttack.FindTargetForSkill();
            if (target == null)
            {
                reason = "NoTarget";
                return false;
            }
        }

        reason = "Ready";
        return true;
    }

    private PlayerSkillDefinition ResolveSkill(PlayerSkillSlot slot)
    {
        resolvedCharacterId = ResolveCharacterId();
        return skillDatabase != null ? skillDatabase.GetSkill(resolvedCharacterId, slot) : null;
    }

    private string ResolveCharacterId()
    {
        if (!string.IsNullOrWhiteSpace(characterIdOverride))
            return characterIdOverride.Trim();

        if (classInfo != null && !string.IsNullOrWhiteSpace(classInfo.characterId))
            return classInfo.characterId.Trim();

        return SelectedCharacterMemory.LoadSelectedId("warrior");
    }

    private IEnumerator SkillRoutine(PlayerSkillDefinition skill, DamageReceiver targetAtStart)
    {
        skillInProgress = true;
        activeSkillSlot = skill.slot;

        // 잠금 시간 계산은 루틴 시작 시각 기준 (쿨다운 역산 의존 제거)
        float startTime = Time.time;

        try
        {
            if (targetAtStart != null && skill.faceTargetOnSkill && basicAttack != null)
                FaceTarget(basicAttack.GetTargetAimPositionForSkill(targetAtStart));

            float actionDuration = Mathf.Max(0.05f, skill.actionDuration);
            float effectDelay = Mathf.Clamp01(skill.effectDelayNormalized) * actionDuration;
            float totalLockTime = Mathf.Max(actionDuration, effectDelay) + Mathf.Max(0f, skill.extraRecoveryTime);

            currentSkillInfo = $"{skill.displayName} mana={skill.manaCost:0.##}, cooldown={skill.cooldown:0.##}, duration={actionDuration:0.##}";

            PrepareAnimatorForSkill(skill);
            SetActionLocks(skill, true);
            ApplyMovementSpeedMultiplier(skill, true);
            PlaySkillAnimation(skill, actionDuration);
            StartSkillAnimationStopTimer(actionDuration);

            if (VFXPlayer != null)
            {
                VFXPlayer.PlayCastVFX(skill);
                VFXPlayer.PlayAuraVFX(skill);
            }

            if (effectDelay > 0f)
                yield return new WaitForSeconds(effectDelay);

            yield return ExecuteSkillEffect(skill, targetAtStart);

            float elapsedAfterEffect = Time.time - startTime;
            float remaining = Mathf.Max(0f, totalLockTime - elapsedAfterEffect);
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);
        }
        finally
        {
            // 스킬 효과 중 예외가 발생해도 잠금과 애니메이션 상태를 반드시 복원 (영구 잠금 방지)
            StopSkillAnimationStopTimer();
            StopDirectSkillClipIfNeeded();
            RestoreAnimatorAfterSkill();
            ApplyMovementSpeedMultiplier(skill, false);
            SetActionLocks(skill, false);

            skillInProgress = false;
        }
    }

    private IEnumerator ExecuteSkillEffect(PlayerSkillDefinition skill, DamageReceiver targetAtStart)
    {
        switch (skill.effectType)
        {
            case PlayerSkillEffectType.DamageOnce:
                ApplyDamageSkill(skill, targetAtStart, skill.damage, true);
                break;

            case PlayerSkillEffectType.MultiHitDamage:
                yield return ExecuteMultiHitDamage(skill, targetAtStart);
                break;

            case PlayerSkillEffectType.DamageOverTime:
                yield return ExecuteDamageOverTime(skill, targetAtStart);
                break;

            case PlayerSkillEffectType.HealNearestAlly:
                ApplyHealNearestAlly(skill);
                break;

            case PlayerSkillEffectType.ShieldAllAllies:
                ApplyShieldAllAllies(skill);
                break;

            case PlayerSkillEffectType.HealOverTimeAllAllies:
                yield return ExecuteHealOverTimeAllAllies(skill);
                break;
        }

        if (skill.ultimateCost <= 0f && skill.ultimateGainOnUse > 0f && ultimateGauge != null)
            ultimateGauge.Gain(skill.ultimateGainOnUse);
    }

    private void ApplyDamageSkill(PlayerSkillDefinition skill, DamageReceiver targetAtStart, float damage, bool playTargetVFX)
    {
        DamageReceiver primaryTarget = targetAtStart != null ? targetAtStart : basicAttack != null ? basicAttack.FindTargetForSkill() : null;
        if (primaryTarget == null)
            return;

        List<DamageReceiver> targets = ResolveDamageTargets(skill, primaryTarget);
        if (targets.Count == 0)
            return;

        Action applyDamage = () =>
        {
            for (int i = 0; i < targets.Count; i++)
            {
                DamageReceiver target = targets[i];
                if (target == null || target.targetHealth == null || target.targetHealth.IsDead)
                    continue;

                target.ReceiveDamage(damage, gameObject);
            }
        };

        bool projectileLaunched = false;
        if (VFXPlayer != null && playTargetVFX)
            projectileLaunched = VFXPlayer.PlayTargetVFX(skill, primaryTarget, skill.applyDamageOnProjectileImpact ? applyDamage : null);

        if (!skill.applyDamageOnProjectileImpact || !projectileLaunched)
            applyDamage();
    }

    private IEnumerator ExecuteMultiHitDamage(PlayerSkillDefinition skill, DamageReceiver targetAtStart)
    {
        int count = Mathf.Max(1, skill.multiHitCount);
        float interval = Mathf.Max(0.01f, skill.multiHitInterval);

        for (int i = 0; i < count; i++)
        {
            ApplyDamageSkill(skill, targetAtStart, skill.damage, true);
            if (i < count - 1)
                yield return new WaitForSeconds(interval);
        }
    }

    private IEnumerator ExecuteDamageOverTime(PlayerSkillDefinition skill, DamageReceiver targetAtStart)
    {
        DamageReceiver primaryTarget = targetAtStart;
        if (primaryTarget == null && basicAttack != null)
            primaryTarget = basicAttack.FindTargetForSkill();

        if (primaryTarget == null)
            yield break;

        if (VFXPlayer != null)
        {
            VFXPlayer.PlayTargetVFX(skill, primaryTarget);
            VFXPlayer.PlayPersistentVFX(skill, primaryTarget);
        }

        float tickInterval = Mathf.Max(0.01f, skill.tickInterval);
        float duration = Mathf.Max(tickInterval, skill.duration);
        int tickCount = Mathf.Max(1, Mathf.CeilToInt(duration / tickInterval));
        List<DamageReceiver> cachedTargets = ResolveDamageTargets(skill, primaryTarget);

        for (int i = 0; i < tickCount; i++)
        {
            List<DamageReceiver> tickTargets = skill.useAreaDamage && skill.refreshAreaTargetsEachTick
                ? ResolveDamageTargets(skill, primaryTarget)
                : cachedTargets;

            for (int j = 0; j < tickTargets.Count; j++)
            {
                DamageReceiver target = tickTargets[j];
                if (target == null || target.targetHealth == null || target.targetHealth.IsDead)
                    continue;

                target.ReceiveDamage(skill.tickDamage, gameObject);
            }

            if (i < tickCount - 1)
                yield return new WaitForSeconds(tickInterval);
        }
    }

    // NPC 지원 스킬의 의도 대상. 힐러가 다가간 그 아군에게 효과가 들어가도록 지정하며,
    // 미지정(사람 조작 포함) 시 기본 규칙인 "가장 가까운 다친 아군"을 사용
    private PlayerStatus supportTargetOverride;

    public void SetSupportTargetOverride(PlayerStatus ally)
    {
        supportTargetOverride = ally;
    }

    // 의도 대상의 1회 소비 (효과 적용 시점에 유효하지 않으면 기본 규칙으로 폴백)
    private PlayerStatus ConsumeSupportTargetOverride()
    {
        PlayerStatus target = supportTargetOverride;
        supportTargetOverride = null;

        if (target == null || target.Health == null || target.Health.IsDead)
            return null;

        return target;
    }

    private void ApplyHealNearestAlly(PlayerSkillDefinition skill)
    {
        PlayerStatus ally = ConsumeSupportTargetOverride();
        if (ally == null)
            ally = FindNearestAlly(true);
        if (ally == null)
            return;

        ally.Heal(skill.healAmount, gameObject);

        if (VFXPlayer != null)
            VFXPlayer.PlayAllyVFX(skill, ally);
    }

    private void ApplyShieldAllAllies(PlayerSkillDefinition skill)
    {
        PlayerStatus[] allies = FindAllies();
        for (int i = 0; i < allies.Length; i++)
        {
            PlayerStatus ally = allies[i];
            // 사망한 아군은 실드 대상에서 제외
            if (!PartyTargetUtility.IsValidPlayerTarget(ally))
                continue;

            ally.AddShield(skill.shieldAmount, skill.shieldDuration);

            if (VFXPlayer != null)
                VFXPlayer.PlayAllyVFX(skill, ally);
        }
    }

    private IEnumerator ExecuteHealOverTimeAllAllies(PlayerSkillDefinition skill)
    {
        float interval = Mathf.Max(0.01f, skill.tickInterval);
        float duration = Mathf.Max(interval, skill.duration);
        int tickCount = Mathf.Max(1, Mathf.CeilToInt(duration / interval));

        for (int i = 0; i < tickCount; i++)
        {
            PlayerStatus[] allies = FindAllies();
            for (int j = 0; j < allies.Length; j++)
            {
                PlayerStatus ally = allies[j];
                // 사망한 아군은 지속 힐 대상에서 제외 (틱마다 재판정)
                if (!PartyTargetUtility.IsValidPlayerTarget(ally))
                    continue;

                ally.Heal(skill.tickHealAmount, gameObject);

                if (VFXPlayer != null)
                    VFXPlayer.PlayAllyVFX(skill, ally);
            }
            if (i < tickCount - 1)
                yield return new WaitForSeconds(interval);
        }
    }

    private List<DamageReceiver> ResolveDamageTargets(PlayerSkillDefinition skill, DamageReceiver primaryTarget)
    {
        List<DamageReceiver> result = new List<DamageReceiver>();
        if (primaryTarget == null)
            return result;

        if (!skill.useAreaDamage)
        {
            AddReceiverIfValid(result, primaryTarget, skill.maxAreaTargets);
            return result;
        }

        Vector3 center = GetTargetCenter(primaryTarget);
        float radius = Mathf.Max(0.1f, skill.areaDamageRadius);
        int maxTargets = Mathf.Max(1, skill.maxAreaTargets);
        HashSet<Health> visited = new HashSet<Health>();

        int skillTargetMask = basicAttack != null ? basicAttack.targetMask.value : ~0;
        Collider[] hits = Physics.OverlapSphere(center, radius, skillTargetMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            DamageReceiver receiver = ResolveDamageReceiverFromCollider(hits[i]);
            AddReceiverIfValid(result, receiver, maxTargets, visited);
            if (result.Count >= maxTargets)
                return result;
        }

        // 씬 전체 탐색 대신 레지스트리 순회 (지속 스킬은 틱마다 호출되는 핫패스)
        IReadOnlyList<DamageReceiver> receivers = CombatRegistry.DamageReceivers;
        for (int i = 0; i < receivers.Count; i++)
        {
            DamageReceiver receiver = receivers[i];
            if (receiver == null)
                continue;

            Health health = receiver.targetHealth != null ? receiver.targetHealth : receiver.GetComponentInParent<Health>();
            if (health == null || health.IsDead || health.GetComponent<PlayerStatus>() != null)
                continue;

            float distance = Vector3.Distance(center, GetHealthCenter(health));
            if (distance <= radius)
                AddReceiverIfValid(result, receiver, maxTargets, visited);

            if (result.Count >= maxTargets)
                break;
        }

        return result;
    }

    private void AddReceiverIfValid(List<DamageReceiver> list, DamageReceiver receiver, int maxTargets)
    {
        AddReceiverIfValid(list, receiver, maxTargets, null);
    }

    private void AddReceiverIfValid(List<DamageReceiver> list, DamageReceiver receiver, int maxTargets, HashSet<Health> visited)
    {
        if (list == null || receiver == null || list.Count >= maxTargets)
            return;

        if (receiver.transform.root == transform.root)
            return;

        if (receiver.targetHealth == null)
            receiver.targetHealth = receiver.GetComponentInParent<Health>();

        Health health = receiver.targetHealth;
        if (health == null || health.IsDead)
            return;

        // 플레이어와 NPC는 공격 스킬 대상에서 제외합니다.
        if (health.GetComponent<PlayerStatus>() != null)
            return;

        if (visited != null)
        {
            if (visited.Contains(health))
                return;
            visited.Add(health);
        }

        if (!list.Contains(receiver))
            list.Add(receiver);
    }

    private DamageReceiver ResolveDamageReceiverFromCollider(Collider col)
    {
        if (col == null)
            return null;

        DamageReceiver receiver = col.GetComponentInParent<DamageReceiver>();
        if (receiver != null)
            return receiver;

        Health health = col.GetComponentInParent<Health>();
        if (health == null || health.GetComponent<PlayerStatus>() != null)
            return null;

        receiver = health.GetComponent<DamageReceiver>();
        if (receiver == null)
            receiver = health.gameObject.AddComponent<DamageReceiver>();

        receiver.targetHealth = health;
        return receiver;
    }

    private Vector3 GetTargetCenter(DamageReceiver target)
    {
        if (target == null)
            return transform.position;

        Health health = target.targetHealth != null ? target.targetHealth : target.GetComponentInParent<Health>();
        if (health != null)
            return GetHealthCenter(health);

        return target.transform.position;
    }

    private Vector3 GetHealthCenter(Health health)
    {
        if (health == null)
            return transform.position;

        Collider[] colliders = health.GetComponentsInChildren<Collider>(true);
        Bounds bounds = new Bounds(health.transform.position, Vector3.zero);
        bool hasBounds = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || !col.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = col.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(col.bounds);
            }
        }

        return hasBounds ? bounds.center : health.transform.position;
    }

    private PlayerStatus[] FindAllies()
    {
        // 레지스트리 순회 + 캐시된 보스 더미 판정 (사망한 아군 포함 여부는 기존 동작 유지)
        IReadOnlyList<PlayerStatus> statuses = CombatRegistry.PlayerStatuses;
        List<PlayerStatus> allies = new List<PlayerStatus>();
        for (int i = 0; i < statuses.Count; i++)
        {
            PlayerStatus status = statuses[i];
            if (status == null || status.IsBossDummy)
                continue;

            allies.Add(status);
        }

        if (allies.Count == 0 && playerStatus != null)
            allies.Add(playerStatus);

        return allies.ToArray();
    }

    private PlayerStatus FindNearestAlly(bool preferDamaged)
    {
        PlayerStatus[] allies = FindAllies();
        PlayerStatus best = null;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < allies.Length; i++)
        {
            PlayerStatus ally = allies[i];
            if (ally == null || ally.Health == null || ally.Health.IsDead)
                continue;

            if (preferDamaged && ally.Health.CurrentHealth >= ally.Health.MaxHealth)
                continue;

            float distance = Vector3.Distance(transform.position, ally.transform.position);
            if (distance < bestScore)
            {
                bestScore = distance;
                best = ally;
            }
        }

        if (best != null)
            return best;

        for (int i = 0; i < allies.Length; i++)
        {
            PlayerStatus ally = allies[i];
            if (ally == null || ally.Health == null || ally.Health.IsDead)
                continue;

            float distance = Vector3.Distance(transform.position, ally.transform.position);
            if (distance < bestScore)
            {
                bestScore = distance;
                best = ally;
            }
        }

        return best;
    }

    private bool IsOffensiveSkill(PlayerSkillDefinition skill)
    {
        return skill.effectType == PlayerSkillEffectType.DamageOnce
            || skill.effectType == PlayerSkillEffectType.DamageOverTime
            || skill.effectType == PlayerSkillEffectType.MultiHitDamage;
    }

    private float GetReadyTime(PlayerSkillSlot slot)
    {
        switch (slot)
        {
            case PlayerSkillSlot.Skill1:
                return skill1ReadyTime;
            case PlayerSkillSlot.Skill2:
                return skill2ReadyTime;
            case PlayerSkillSlot.Ultimate:
                return ultimateReadyTime;
            default:
                return 0f;
        }
    }

    private void SetReadyTime(PlayerSkillSlot slot, float value)
    {
        switch (slot)
        {
            case PlayerSkillSlot.Skill1:
                skill1ReadyTime = value;
                break;
            case PlayerSkillSlot.Skill2:
                skill2ReadyTime = value;
                break;
            case PlayerSkillSlot.Ultimate:
                ultimateReadyTime = value;
                break;
        }
    }

    public float GetCooldownRemaining(PlayerSkillSlot slot)
    {
        return Mathf.Max(0f, GetReadyTime(slot) - Time.time);
    }

    public float GetCooldownDuration(PlayerSkillSlot slot)
    {
        PlayerSkillDefinition skill = ResolveSkill(slot);
        return skill != null ? Mathf.Max(0f, skill.cooldown) : 0f;
    }

    public float GetCooldownNormalized(PlayerSkillSlot slot)
    {
        float duration = GetCooldownDuration(slot);
        if (duration <= 0.0001f)
            return 0f;

        return Mathf.Clamp01(GetCooldownRemaining(slot) / duration);
    }

    public bool IsCastingSlot(PlayerSkillSlot slot)
    {
        return skillInProgress && activeSkillSlot == slot;
    }

    public PlayerSkillDefinition GetSkillDefinitionForUI(PlayerSkillSlot slot)
    {
        return ResolveSkill(slot);
    }

    public string GetSkillDisplayName(PlayerSkillSlot slot)
    {
        PlayerSkillDefinition skill = ResolveSkill(slot);
        return skill != null ? skill.displayName : slot.ToString();
    }

    public int GetUltimateGaugePercentInt()
    {
        ResolveReferences();
        if (ultimateGauge == null)
            return 0;

        // RoundToInt를 쓰면 실제 게이지가 99.5%여도 UI에는 100%로 표시됩니다.
        // 그러면 플레이어는 100%라고 보는데 CanConsume(100)은 실패해서 버튼이 비활성화되는 것처럼 보입니다.
        // 그래서 실제로 소비 가능한 상태일 때만 100%를 표시하고, 그 전에는 내림값을 표시합니다.
        if (ultimateGauge.IsFull)
            return 100;

        return Mathf.Clamp(Mathf.FloorToInt(ultimateGauge.Normalized * 100f), 0, 99);
    }

    public float GetUltimateGaugeNormalized()
    {
        ResolveReferences();
        return ultimateGauge != null ? ultimateGauge.Normalized : 0f;
    }

    public bool TryUseSkillByIndex(int index)
    {
        switch (index)
        {
            case 0:
                return TryUseSkill(PlayerSkillSlot.Skill1);
            case 1:
                return TryUseSkill(PlayerSkillSlot.Skill2);
            case 2:
                return TryUseSkill(PlayerSkillSlot.Ultimate);
            default:
                return false;
        }
    }

    public bool CanUseSkillForUI(PlayerSkillSlot slot, bool checkTargetRange, out string reason)
    {
        // 실제 사용(TryUseSkill)과 동일한 검증 사용
        // 기존에는 보스 비행 중 차단 검사가 UI에 빠져 있어 "Ready"로 표시되지만 실제로는 실패했음
        return ValidateSkillUse(slot, checkTargetRange, out reason, out _, out _);
    }


    private void StartSkillAnimationStopTimer(float actionDuration)
    {
        StopSkillAnimationStopTimer();
        skillAnimationStopRoutine = StartCoroutine(StopSkillAnimationAfterDuration(actionDuration));
    }

    private void StopSkillAnimationStopTimer()
    {
        if (skillAnimationStopRoutine == null)
            return;

        StopCoroutine(skillAnimationStopRoutine);
        skillAnimationStopRoutine = null;
    }

    private IEnumerator StopSkillAnimationAfterDuration(float actionDuration)
    {
        yield return new WaitForSeconds(Mathf.Max(0.05f, actionDuration));

        StopDirectSkillClipIfNeeded();
        RestoreAnimatorAfterSkill();
        skillAnimationStopRoutine = null;
    }

    private void PrepareAnimatorForSkill(PlayerSkillDefinition skill)
    {
        if (animator == null)
            return;

        originalAnimatorSpeed = animator.speed;
        originalRootMotion = animator.applyRootMotion;
        hasOriginalAnimatorValues = true;
        animator.applyRootMotion = false;
    }

    private void RestoreAnimatorAfterSkill()
    {
        if (animator == null)
            return;

        animator.speed = originalAnimatorSpeed;
        if (hasOriginalAnimatorValues)
            animator.applyRootMotion = originalRootMotion;
    }

    private void PlaySkillAnimation(PlayerSkillDefinition skill, float actionDuration)
    {
        if (animator == null || skill == null)
            return;

        animator.enabled = true;
        StopDirectSkillClipIfNeeded();

        AnimationClip oneShotClip = ResolveSkillAnimationClip(skill);
        if (forceOneShotSkillAnimation && oneShotClip != null)
        {
            PlayDirectClip(oneShotClip, actionDuration);
            currentSkillAnimationInfo = $"Direct Clip: {oneShotClip.name}, duration={actionDuration:0.###}s";
            return;
        }

        string stateName = ResolveAnimatorStateName(skill.animatorStateName, true);
        if (string.IsNullOrWhiteSpace(stateName))
        {
            currentSkillAnimationInfo = "No animation state or clip";
            return;
        }

        animator.speed = 1f;
        animator.CrossFadeInFixedTime(stateName, 0.05f, 0, 0f);
        animator.Update(0f);
        currentSkillAnimationInfo = $"Animator State fallback: {stateName}";
    }

    private AnimationClip ResolveSkillAnimationClip(PlayerSkillDefinition skill)
    {
        if (skill == null)
            return null;

        if (skill.animationClip != null)
            return skill.animationClip;

        if (!findAnimationClipByStateName || animator == null || animator.runtimeAnimatorController == null)
            return null;

        string stateName = skill.animatorStateName;
        if (string.IsNullOrWhiteSpace(stateName))
            return null;

        string shortName = stateName;
        int dotIndex = shortName.LastIndexOf('.');
        if (dotIndex >= 0 && dotIndex < shortName.Length - 1)
            shortName = shortName.Substring(dotIndex + 1);

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null)
            return null;

        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null)
                continue;

            if (string.Equals(clip.name, shortName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(clip.name, stateName, StringComparison.OrdinalIgnoreCase))
                return clip;
        }

        if (warnWhenSkillClipMissing)
        {
            Debug.LogWarning(
                $"[PlayerSkillController] Skill animation clip not found for state '{stateName}'. " +
                "Assign Animation Clip directly in DB_PlayerSkills if you want one-shot playback.",
                this
            );
        }

        return null;
    }

    private void PlayDirectClip(AnimationClip clip, float actionDuration)
    {
        SkillClipPlayer.PlayTimed(animator, clip, actionDuration);
    }

    private void StopDirectSkillClipIfNeeded()
    {
        skillClipPlayer?.Stop();
    }

    private void SetActionLocks(PlayerSkillDefinition skill, bool locked)
    {
        if (skill == null)
            return;

        if (playerMovement != null)
        {
            if (skill.lockMovementDuringSkill)
                playerMovement.SetExternalMovementLock(locked);

            if (skill.suppressMovementAnimationDuringSkill)
                playerMovement.SetAnimationSuppressed(locked);
        }

        if (basicAttack != null && skill.lockBasicAttackDuringSkill)
            basicAttack.SetExternalActionLock(locked);
    }

    private void ApplyMovementSpeedMultiplier(PlayerSkillDefinition skill, bool apply)
    {
        if (playerMovement == null)
            return;

        if (apply)
        {
            if (skill == null)
                return;

            if (!hasOriginalMoveSpeed)
            {
                originalMoveSpeed = playerMovement.moveSpeed;
                hasOriginalMoveSpeed = true;
            }

            playerMovement.moveSpeed = originalMoveSpeed * Mathf.Max(0.1f, skill.movementSpeedMultiplier);
        }
        else if (hasOriginalMoveSpeed)
        {
            playerMovement.moveSpeed = originalMoveSpeed;
            hasOriginalMoveSpeed = false;
        }
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
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return string.Empty;

        string resolved = AnimatorStateUtility.ResolveStateName(animator, stateName);
        if (string.IsNullOrEmpty(resolved) && warnIfMissing)
            Debug.LogWarning($"[PlayerSkillController] Animator state not found: {stateName}. If you use Direct Clip Playback with Animation Clip assigned, this warning can be ignored.", this);

        return resolved;
    }

    private bool IsAnyBossFlying()
    {
        IReadOnlyList<BossSkillPatternController> bosses = CombatRegistry.BossSkillControllers;
        for (int i = 0; i < bosses.Count; i++)
        {
            BossSkillPatternController boss = bosses[i];
            if (boss != null && boss.IsFlying)
                return true;
        }

        return false;
    }

    private void OnDisable()
    {
        StopSkillAnimationStopTimer();
        StopDirectSkillClipIfNeeded();

        if (playerMovement != null)
        {
            playerMovement.SetExternalMovementLock(false);
            playerMovement.SetAnimationSuppressed(false);
        }

        if (basicAttack != null)
            basicAttack.SetExternalActionLock(false);

        ApplyMovementSpeedMultiplier(null, false);
        skillInProgress = false;
    }

    private void OnDestroy()
    {
        skillClipPlayer?.Dispose();
    }
}
