using System.Collections.Generic;

/// <summary>
/// 행동 가능성 Mask의 단일 구현 (Agency 문서 3.5).
/// FSM, Utility, GCN, GAT 등 모든 정책이 같은 Mask를 사용해야
/// "행동 후보는 동일하고 판단 방식만 다르다"는 비교 공정성 조건이 성립.
///
/// 검사는 두 계층으로 구분.
/// - 기본 검사: 대상 존재, 생존, 자원과 쿨타임 등 규칙상 실행이 성립하는지
/// - 전술 검사(includeTacticalChecks): 보스 무적, 비행 등 실행은 되지만
///   효과가 없는 상황의 제외 여부
/// BasicFSM은 기본 검사만 사용하여 "무적 보스 공격" 같은 순진한 행동을
/// 그대로 유지하고(비교 기준선), PatternAwareFSM과 학습 모델은 전술 검사까지 사용.
/// </summary>
public static class NpcActionMask
{
    // 전체 행동 목록 (Mask 순회 및 학습 모델의 행동 후보 나열용)
    // AttackBoss(9)는 슬롯별 행동으로 대체되어 후보에서 제외 (번호만 호환용으로 유지)
    public static readonly NpcAction[] AllActions =
    {
        NpcAction.FollowPlayer,
        NpcAction.MoveToBoss,
        NpcAction.MoveToSafePosition,
        NpcAction.KeepDistance,
        NpcAction.RegroupDuringBossFly,
        NpcAction.MoveToAllyAndHeal,
        NpcAction.ShieldDangerAlly,
        NpcAction.ShieldParty,
        NpcAction.AttackSlime,
        NpcAction.MoveToSlime,
        NpcAction.SpreadFromParty,
        NpcAction.AttackBossBasic,
        NpcAction.AttackBossSkill1,
        NpcAction.AttackBossSkill2,
        NpcAction.AttackBossUltimate
    };

    // 단일 행동의 실행 가능성 판정
    public static bool IsAvailable(NPCSimpleFSMController npc, NpcAction action, bool includeTacticalChecks)
    {
        if (npc == null)
            return false;

        switch (action)
        {
            case NpcAction.FollowPlayer:
                return npc.playerTarget != null;

            // 거리 조건이 있는 보스 관련 행동 3종은 기하학적으로 서로 배타.
            // 사거리 밖에서의 공격은 실제로 빗나가고, 이미 충분히 떨어진 상태의 거리두기는
            // 의미가 없으므로 규칙상 실행 불가로 판정 (모든 정책에 동일 적용)
            case NpcAction.MoveToBoss:
                if (!HasLivingBoss(npc))
                    return false;
                return npc.DistanceToBoss() > npc.DesiredCombatDistance;

            case NpcAction.KeepDistance:
                if (!HasLivingBoss(npc))
                    return false;
                return npc.DistanceToBoss() < npc.keepDistanceFromBoss;

            // 사용 중지된 자동 선택 공격 (슬롯별 행동으로 대체)
            case NpcAction.AttackBoss:
                return false;

            case NpcAction.AttackBossBasic:
                return IsBossAttackPossible(npc, includeTacticalChecks);

            // 스킬 공격은 자원과 쿨타임, 정책의 스킬 사용 간격까지 충족해야 후보가 됨
            case NpcAction.AttackBossSkill1:
                return IsBossAttackPossible(npc, includeTacticalChecks) && CanCastSkillNow(npc, PlayerSkillSlot.Skill1);

            case NpcAction.AttackBossSkill2:
                return IsBossAttackPossible(npc, includeTacticalChecks) && CanCastSkillNow(npc, PlayerSkillSlot.Skill2);

            case NpcAction.AttackBossUltimate:
                return IsBossAttackPossible(npc, includeTacticalChecks) && CanCastSkillNow(npc, PlayerSkillSlot.Ultimate);

            // 위험 지역 안이거나, 벽 그림자 덕분에만 안전한 위치(대피 유지 필요)면 후보
            case NpcAction.MoveToSafePosition:
                return DangerZoneRegistry.IsPointInAnyZone(npc.transform.position, npc.dangerCheckPadding)
                    || DangerZoneRegistry.IsPointShadowProtected(npc.transform.position, npc.dangerCheckPadding);

            case NpcAction.RegroupDuringBossFly:
                return npc.bossSkillPattern != null && npc.bossSkillPattern.IsFlying;

            // 지원 행동은 해당 슬롯의 스킬이 실제로 지원 효과(힐, 실드)일 때만 후보가 됨
            // (공격 스킬을 가진 직업이 지원 행동 후보를 받아 실행 실패로 폴백하는 낭비 방지)
            case NpcAction.MoveToAllyAndHeal:
                return IsSupportSlot(npc, PlayerSkillSlot.Skill1)
                    && CanUseSupportSkill(npc, PlayerSkillSlot.Skill1);

            // 위험 아군 실드는 위험 지역 안의 아군이 실제로 존재해야 실행이 성립
            // (대상 없이 후보로 허용하면 실행 단계에서 반드시 실패하므로 규칙상 불가로 판정)
            case NpcAction.ShieldDangerAlly:
                return IsSupportSlot(npc, PlayerSkillSlot.Skill2)
                    && CanUseSupportSkill(npc, PlayerSkillSlot.Skill2)
                    && NpcHealerPolicy.FindMostThreatenedAlly(npc) != null;

            case NpcAction.ShieldParty:
                return IsSupportSlot(npc, PlayerSkillSlot.Skill2)
                    && CanUseSupportSkill(npc, PlayerSkillSlot.Skill2);

            // 슬라임 행동도 보스 공격과 같이 거리로 배타 분리
            // (근접 상태에서 MoveToSlime만 반복하며 공격하지 않는 문제 방지)
            case NpcAction.AttackSlime:
            {
                SlimeAddEnemy slime = SlimeTargetSelector.FindMostUrgent(npc.transform.position);
                if (slime == null)
                    return false;
                return NPCSimpleFSMController.FlatDistance(npc.transform.position, slime.transform.position)
                    <= npc.DesiredCombatDistance;
            }

            case NpcAction.MoveToSlime:
            {
                SlimeAddEnemy slime = SlimeTargetSelector.FindMostUrgent(npc.transform.position);
                if (slime == null)
                    return false;
                return NPCSimpleFSMController.FlatDistance(npc.transform.position, slime.transform.position)
                    > npc.DesiredCombatDistance;
            }

            case NpcAction.SpreadFromParty:
                return npc.bossSkillPattern != null && npc.bossSkillPattern.IsTrackingLightningActive;

            default:
                return false;
        }
    }

    // 현재 실행 가능한 행동을 buffer에 수집 (buffer는 호출 측에서 재사용)
    public static int CollectAvailable(NPCSimpleFSMController npc, bool includeTacticalChecks, List<NpcAction> buffer)
    {
        buffer.Clear();

        for (int i = 0; i < AllActions.Length; i++)
        {
            if (IsAvailable(npc, AllActions[i], includeTacticalChecks))
                buffer.Add(AllActions[i]);
        }

        return buffer.Count;
    }

    // 보스 공격 계열의 공통 조건 (생존, 교전 거리 안, 전술 검사)
    private static bool IsBossAttackPossible(NPCSimpleFSMController npc, bool includeTacticalChecks)
    {
        if (!HasLivingBoss(npc))
            return false;

        if (npc.DistanceToBoss() > npc.DesiredCombatDistance)
            return false;

        if (!includeTacticalChecks)
            return true;

        // 전술 검사: 무적이거나 비행으로 타격 불가한 보스는 공격 후보에서 제외
        return !IsBossDamageImmune(npc) && !npc.IsBossFlyingAndUntargetable();
    }

    // 스킬을 지금 실제로 시전할 수 있는지 (자원과 쿨타임 + 정책의 스킬 사용 간격)
    private static bool CanCastSkillNow(NPCSimpleFSMController npc, PlayerSkillSlot slot)
    {
        if (npc.skillController == null || !npc.IsSkillTickReady)
            return false;

        return npc.skillController.CanUseSkillForUI(slot, checkTargetRange: false, out _);
    }

    // 거리와 무관하게 보스가 전술적으로 때릴 가치가 있는지 (생존, 무적 아님, 비행 아님).
    // "사거리 안인가"와 "때릴 가치가 있는가"는 다른 판단이므로 분리.
    // 슬라임 우선 전환 판단, CAN_ATTACK 엣지 생성 등 거리 조건이 개입하면 안 되는 곳에서 사용
    public static bool IsBossTacticallyAttackable(NPCSimpleFSMController npc)
    {
        if (npc == null || !HasLivingBoss(npc))
            return false;

        return !IsBossDamageImmune(npc) && !npc.IsBossFlyingAndUntargetable();
    }

    private static bool HasLivingBoss(NPCSimpleFSMController npc)
    {
        if (npc.boss == null)
            return false;

        Health bossHealth = npc.boss.health != null ? npc.boss.health : npc.boss.GetComponent<Health>();
        return bossHealth != null && !bossHealth.IsDead;
    }

    private static bool IsBossDamageImmune(NPCSimpleFSMController npc)
    {
        if (npc.boss == null)
            return false;

        Health bossHealth = npc.boss.health != null ? npc.boss.health : npc.boss.GetComponent<Health>();
        return bossHealth != null && bossHealth.DamageImmune;
    }

    // 지원 스킬의 자원과 쿨타임 충족 여부 (대상 탐색 없이 경량 검사)
    private static bool CanUseSupportSkill(NPCSimpleFSMController npc, PlayerSkillSlot slot)
    {
        if (npc.skillController == null)
            return false;

        return npc.skillController.CanUseSkillForUI(slot, checkTargetRange: false, out _);
    }

    // 해당 슬롯의 스킬이 지원 효과(힐, 실드 계열)인지 (직업별 스킬 구성 기준)
    private static bool IsSupportSlot(NPCSimpleFSMController npc, PlayerSkillSlot slot)
    {
        if (npc.skillController == null)
            return false;

        PlayerSkillDefinition definition = npc.skillController.GetSkillDefinitionForUI(slot);
        return definition != null && GnnSchema.IsSupportEffect(definition.effectType);
    }
}
