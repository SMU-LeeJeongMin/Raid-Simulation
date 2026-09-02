using UnityEngine;

/// <summary>
/// 탱커/원거리 딜러의 전투 의사결정 정책.
/// BasicFSM 모드: 보스만 상대하는 순진한 규칙 (비교 기준선, 기존 동작 불변).
/// PatternAwareFSM 모드: 보스가 공격 불가 상태(Add Phase 무적)면 슬라임 우선 처리.
/// </summary>
public class NpcFighterPolicy : INpcRolePolicy
{
    private readonly bool tank;

    public NpcFighterPolicy(bool tank)
    {
        this.tank = tank;
    }

    public NpcAction Think(NPCSimpleFSMController npc)
    {
        float desiredDistance = tank ? npc.meleeDistance : npc.rangedDistance;
        return ThinkFighter(npc, desiredDistance, tank);
    }

    // 힐러 정책도 보스 공격 시 재사용하는 공용 전투 판단
    public static NpcAction ThinkFighter(NPCSimpleFSMController npc, float desiredDistance, bool tank)
    {
        if (npc.boss == null)
        {
            npc.FollowPlayerFormation();
            npc.SetStateLabel("No Boss: Follow Player");
            return NpcAction.FollowPlayer;
        }

        // 패턴 인지 모드: 보스가 공격할 가치가 없는 상태(무적)이고 슬라임이 있으면 슬라임 우선
        // BasicFSM은 전술 검사를 쓰지 않으므로 기존처럼 무적 보스를 계속 공격 (기준선 유지)
        // 슬라임 생존 여부는 거리와 무관하게 판단 (Mask의 슬라임 행동은 거리로 배타 분리되므로 직접 조회)
        if (npc.IsPatternAware() && npc.prioritizeSlimesWhenBossImmune
            && SlimeTargetSelector.AnyAlive()
            && !NpcActionMask.IsBossTacticallyAttackable(npc))
        {
            return ThinkAttackSlime(npc, desiredDistance, tank);
        }

        if (npc.IsBossFlyingAndUntargetable())
        {
            npc.FollowPlayerFormation();
            npc.SetStateLabel(tank ? "Tank Wait: Boss Flying" : "DPS Wait: Boss Flying");
            return NpcAction.RegroupDuringBossFly;
        }

        // 거리 판단과 이동 목적지는 보스 콜라이더 표면 기준 조준점 사용
        // (거대 보스 모델에서 중심점 거리로는 공격 창에 진입할 수 없는 문제의 보정)
        Vector3 bossPosition = npc.boss.transform.position;
        Vector3 aimPoint = npc.GetBossAimPoint();
        float distance = NPCSimpleFSMController.FlatDistance(npc.transform.position, aimPoint);

        // NPC가 콜라이더 안쪽에 있으면 조준점이 자기 위치와 겹치므로 바라보기는 보스 중심 기준
        Vector3 facePoint = distance > 0.1f ? aimPoint : bossPosition;

        if (distance > desiredDistance)
        {
            // 패턴 인지 모드는 위험 지역 밖의 사거리 지점으로 접근 (BasicFSM은 순진한 접근 유지)
            npc.MoveToCombatRangeAvoidingDanger(aimPoint, desiredDistance);
            npc.FaceWhileMoving(facePoint);
            npc.SetStateLabel(tank ? "Tank MoveToBoss" : "DPS MoveToBoss");
            return NpcAction.MoveToBoss;
        }

        if (!tank && distance < npc.keepDistanceFromBoss)
        {
            // 조준점이 자기 위치와 겹치면(몸체 내부) 후퇴 방향이 현재 바라보는 방향으로
            // 퇴화하여 보스 쪽으로 도망치는 오동작이 생기므로 보스 중심 기준으로 후퇴
            Vector3 retreatFrom = distance > 0.1f ? aimPoint : bossPosition;
            float retreatSeparation = npc.keepDistanceFromBoss + Mathf.Max(0f, npc.keepDistanceMargin);

            // 맵 끝에 몰려 실제로 물러날 수 없으면 후퇴 대신 근접 교전 유지
            if (npc.CanRetreatFrom(retreatFrom, retreatSeparation))
            {
                // 퇴각 목적지에 여유 거리를 더해 퇴각과 재퇴각의 반복 방지
                npc.MoveAwayFrom(retreatFrom, retreatSeparation);
                npc.FaceWhileMoving(facePoint);
                npc.SetStateLabel("DPS KeepDistance");
                return NpcAction.KeepDistance;
            }
        }

        npc.FacePosition(facePoint);

        // 기존과 동일한 우선순위(궁극기 > 스킬1 > 스킬2 > 기본)로 슬롯을 정하고
        // 어떤 슬롯을 썼는지까지 행동으로 보고 (동작 불변, 라벨만 구체화)
        NpcAction attackAction = ChooseBossAttackAction(npc);
        npc.SetStateLabel(tank ? "Tank Attack" : "DPS Attack");
        npc.ExecuteAttackWithSlot(NpcActions.GetSkillSlot(attackAction));
        return attackAction;
    }

    // FSM의 보스 공격 슬롯 선택 (Mask가 허용하는 범위에서 고정 우선순위)
    public static NpcAction ChooseBossAttackAction(NPCSimpleFSMController npc)
    {
        if (npc.useUltimateWhenReady
            && NpcActionMask.IsAvailable(npc, NpcAction.AttackBossUltimate, includeTacticalChecks: false))
            return NpcAction.AttackBossUltimate;

        if (NpcActionMask.IsAvailable(npc, NpcAction.AttackBossSkill1, includeTacticalChecks: false))
            return NpcAction.AttackBossSkill1;

        if (NpcActionMask.IsAvailable(npc, NpcAction.AttackBossSkill2, includeTacticalChecks: false))
            return NpcAction.AttackBossSkill2;

        return NpcAction.AttackBossBasic;
    }

    // 슬라임 우선 처리: 가장 시급한 슬라임에 접근 후 공격
    // 궁극기는 사용하지 않음 (슬라임 대상 궁극기는 낭비 지표로 집계되는 비효율 행동)
    private static NpcAction ThinkAttackSlime(NPCSimpleFSMController npc, float desiredDistance, bool tank)
    {
        SlimeAddEnemy slime = SlimeTargetSelector.FindMostUrgent(npc.transform.position);
        if (slime == null)
        {
            npc.FollowPlayerFormation();
            npc.SetStateLabel("No Slime: Follow Player");
            return NpcAction.FollowPlayer;
        }

        Vector3 slimePosition = slime.transform.position;
        float distance = NPCSimpleFSMController.FlatDistance(npc.transform.position, slimePosition);

        if (distance > desiredDistance)
        {
            npc.MoveToward(slimePosition, Mathf.Max(0.2f, desiredDistance * 0.8f));
            npc.SetStateLabel(tank ? "Tank MoveToSlime" : "DPS MoveToSlime");
            return NpcAction.MoveToSlime;
        }

        npc.FacePosition(slimePosition);
        npc.SetStateLabel(tank ? "Tank AttackSlime" : "DPS AttackSlime");

        // 슬라임 대상 궁극기는 낭비 행동이므로 사용하지 않음
        npc.ExecuteAttackTick(allowUltimate: false);
        return NpcAction.AttackSlime;
    }
}
