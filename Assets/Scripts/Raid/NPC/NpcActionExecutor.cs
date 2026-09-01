using UnityEngine;

/// <summary>
/// 정책 행동의 실행 단일 구현.
/// Utility 정책과 GNN 정책이 같은 실행기를 사용하므로 정책 간 차이는
/// 순수하게 판단에만 존재 (비교 공정성 조건).
/// 실행이 성립하지 않으면(대상 소멸, 후퇴 불가, 스킬 쿨타임) false를 돌려
/// 호출 측이 차순위 행동으로 폴백.
/// </summary>
public static class NpcActionExecutor
{
    public static bool TryExecute(NPCSimpleFSMController npc, NpcAction action, bool tank)
    {
        switch (action)
        {
            case NpcAction.MoveToSafePosition:
                return npc.TryExecuteSafeEscape("Policy");

            case NpcAction.SpreadFromParty:
                return npc.TryExecuteSpreadFromParty();

            case NpcAction.FollowPlayer:
            case NpcAction.RegroupDuringBossFly:
                npc.FollowPlayerFormation();
                return true;

            case NpcAction.MoveToBoss:
            {
                if (npc.boss == null)
                    return false;

                Vector3 aimPoint = npc.GetBossAimPoint();
                Vector3 facePoint = ResolveFacePoint(npc, aimPoint);
                npc.MoveToCombatRangeAvoidingDanger(aimPoint, DesiredDistance(npc, tank));
                npc.FaceWhileMoving(facePoint);
                return true;
            }

            case NpcAction.KeepDistance:
            {
                if (npc.boss == null)
                    return false;

                Vector3 aimPoint = npc.GetBossAimPoint();
                float distance = NPCSimpleFSMController.FlatDistance(npc.transform.position, aimPoint);
                Vector3 retreatFrom = distance > 0.1f ? aimPoint : npc.boss.transform.position;
                float separation = npc.keepDistanceFromBoss + Mathf.Max(0f, npc.keepDistanceMargin);

                // 맵 끝에 몰려 물러날 수 없으면 실행 불성립 (호출 측이 다른 행동으로 폴백)
                if (!npc.CanRetreatFrom(retreatFrom, separation))
                    return false;

                npc.MoveAwayFrom(retreatFrom, separation);
                npc.FaceWhileMoving(ResolveFacePoint(npc, aimPoint));
                return true;
            }

            case NpcAction.AttackBoss:
            {
                if (npc.boss == null)
                    return false;

                npc.FacePosition(ResolveFacePoint(npc, npc.GetBossAimPoint()));
                npc.ExecuteAttackTick(allowUltimate: true);
                return true;
            }

            // 슬롯별 보스 공격: 선택한 스킬을 그대로 시전 (기본 공격은 슬롯 없음)
            case NpcAction.AttackBossBasic:
            case NpcAction.AttackBossSkill1:
            case NpcAction.AttackBossSkill2:
            case NpcAction.AttackBossUltimate:
            {
                if (npc.boss == null)
                    return false;

                npc.FacePosition(ResolveFacePoint(npc, npc.GetBossAimPoint()));
                npc.ExecuteAttackWithSlot(NpcActions.GetSkillSlot(action));
                return true;
            }

            case NpcAction.MoveToSlime:
            {
                SlimeAddEnemy slime = SlimeTargetSelector.FindMostUrgent(npc.transform.position);
                if (slime == null)
                    return false;

                npc.MoveToward(slime.transform.position, Mathf.Max(0.2f, DesiredDistance(npc, tank) * 0.8f));
                return true;
            }

            case NpcAction.AttackSlime:
            {
                SlimeAddEnemy slime = SlimeTargetSelector.FindMostUrgent(npc.transform.position);
                if (slime == null)
                    return false;

                npc.FacePosition(slime.transform.position);
                npc.ExecuteAttackTick(allowUltimate: false);
                return true;
            }

            case NpcAction.MoveToAllyAndHeal:
            {
                PlayerStatus ally = NpcHealerPolicy.FindLowestHpAlly(out _);
                if (ally == null)
                    return false;

                if (NpcHealerPolicy.MoveNearAllyIfNeeded(npc, ally))
                    return true;

                return NpcHealerPolicy.TryUseHealerSkill(npc, useShieldSkill: false);
            }

            case NpcAction.ShieldDangerAlly:
            {
                PlayerStatus dangerAlly = NpcHealerPolicy.FindMostThreatenedAlly(npc);
                if (dangerAlly == null)
                    return false;

                if (NpcHealerPolicy.MoveNearAllyIfNeeded(npc, dangerAlly))
                    return true;

                return NpcHealerPolicy.TryUseHealerSkill(npc, useShieldSkill: true);
            }

            case NpcAction.ShieldParty:
            {
                PlayerStatus lowest = NpcHealerPolicy.FindLowestHpAlly(out _);
                if (lowest == null)
                    return false;

                if (NpcHealerPolicy.MoveNearAllyIfNeeded(npc, lowest))
                    return true;

                return NpcHealerPolicy.TryUseHealerSkill(npc, useShieldSkill: true);
            }

            default:
                return false;
        }
    }

    private static float DesiredDistance(NPCSimpleFSMController npc, bool tank)
    {
        return tank ? npc.meleeDistance : npc.rangedDistance;
    }

    // 조준점이 자기 위치와 겹치면(몸체 내부) 보스 중심을 바라봄
    private static Vector3 ResolveFacePoint(NPCSimpleFSMController npc, Vector3 aimPoint)
    {
        float distance = NPCSimpleFSMController.FlatDistance(npc.transform.position, aimPoint);
        return distance > 0.1f ? aimPoint : npc.boss.transform.position;
    }
}
