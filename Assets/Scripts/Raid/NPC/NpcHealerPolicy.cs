using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 힐러의 의사결정 정책.
/// 기존 NPCSimpleFSMController.ThinkHealer와 힐러 전용 아군 탐색 로직의 분리 구현.
/// </summary>
public class NpcHealerPolicy : INpcRolePolicy
{
    public NpcAction Think(NPCSimpleFSMController npc)
    {
        // 패턴 인지 모드: 위험 지역에 있는 아군 보호 우선
        if (npc.IsPatternAware() && npc.healerProtectDangerAlly)
        {
            PlayerStatus dangerAlly = FindMostThreatenedAlly(npc);
            if (dangerAlly != null)
            {
                bool moved = MoveNearAllyIfNeeded(npc, dangerAlly);
                if (moved)
                {
                    npc.SetStateLabel($"Healer MoveToDangerAlly {dangerAlly.name}");
                    return NpcAction.MoveToAllyAndHeal;
                }

                bool usedSupport = false;
                float ratio = GetHealthRatio(dangerAlly);
                if (ratio <= npc.healerDangerShieldHpThreshold)
                    usedSupport = TryUseHealerSkill(npc, useShieldSkill: true);

                if (!usedSupport && ratio <= npc.healerLowHpThreshold)
                    usedSupport = TryUseHealerSkill(npc, useShieldSkill: false);

                if (usedSupport)
                {
                    npc.SetStateLabel($"Healer ProtectDangerAlly {dangerAlly.name}");
                    return NpcAction.ShieldDangerAlly;
                }
            }
        }

        PlayerStatus lowAlly = FindLowestHpAlly(out float lowRatio);

        if (lowAlly != null && lowRatio <= npc.healerLowHpThreshold)
        {
            // 단일 힐은 시전 거리까지 접근 후 그 아군을 대상으로 시전
            bool movedToAlly = MoveNearAllyIfNeeded(npc, lowAlly, npc.healerHealCastDistance);
            bool usedHeal = !movedToAlly && TryUseHealerSkill(npc, useShieldSkill: false, lowAlly);

            if (movedToAlly || usedHeal)
            {
                npc.SetStateLabel($"Healer Heal {lowAlly.name}");
                return NpcAction.MoveToAllyAndHeal;
            }
        }

        if (lowAlly != null && lowRatio <= npc.healerShieldThreshold)
        {
            bool movedToAlly = MoveNearAllyIfNeeded(npc, lowAlly);
            bool usedShield = !movedToAlly && TryUseHealerSkill(npc, useShieldSkill: true);

            if (movedToAlly || usedShield)
            {
                npc.SetStateLabel($"Healer Shield {lowAlly.name}");
                return NpcAction.ShieldParty;
            }
        }

        if (npc.IsBossFlyingAndUntargetable())
        {
            npc.FollowPlayerFormation();
            npc.SetStateLabel("Healer Support Only: Boss Flying");
            return NpcAction.RegroupDuringBossFly;
        }

        if (npc.boss != null)
        {
            // 치유할 대상이 없으면 원거리 딜러와 동일하게 전투 수행
            return NpcFighterPolicy.ThinkFighter(npc, npc.rangedDistance, false);
        }

        npc.FollowPlayerFormation();
        npc.SetStateLabel("Healer Follow Player");
        return NpcAction.FollowPlayer;
    }

    // 위험 지역 안에 있는 아군 중 (낮은 HP, 임박한 지역, 가까운 거리) 점수가 가장 높은 대상 탐색
    // Utility 정책도 재사용하는 공용 탐색
    public static PlayerStatus FindMostThreatenedAlly(NPCSimpleFSMController npc)
    {
        IReadOnlyList<PlayerStatus> statuses = CombatRegistry.PlayerStatuses;
        PlayerStatus best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < statuses.Count; i++)
        {
            PlayerStatus ally = statuses[i];
            if (!PartyTargetUtility.IsValidPlayerTarget(ally))
                continue;

            DangerZoneHandle containingZone = DangerZoneRegistry.FindFirstZoneContaining(ally.transform.position, npc.dangerCheckPadding);
            if (containingZone == null)
                continue;

            float hpRatio = GetHealthRatio(ally);
            float distance = NPCSimpleFSMController.FlatDistance(npc.transform.position, ally.transform.position);
            float zoneUrgency = containingZone.TimeRemaining >= 0f ? Mathf.Clamp01(1f - containingZone.TimeRemaining / 2f) : 0.4f;
            float score = (1f - hpRatio) * 2f + zoneUrgency - distance * 0.03f;

            if (score > bestScore)
            {
                bestScore = score;
                best = ally;
            }
        }

        return best;
    }

    public static PlayerStatus FindLowestHpAlly(out float lowestRatio)
    {
        lowestRatio = 1f;
        PlayerStatus lowest = null;

        IReadOnlyList<PlayerStatus> statuses = CombatRegistry.PlayerStatuses;
        for (int i = 0; i < statuses.Count; i++)
        {
            PlayerStatus ally = statuses[i];
            if (!PartyTargetUtility.IsValidPlayerTarget(ally))
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

    // 아군이 힐 사거리 밖이면 접근 이동 수행 후 true 반환
    public static bool MoveNearAllyIfNeeded(NPCSimpleFSMController npc, PlayerStatus ally)
    {
        return MoveNearAllyIfNeeded(npc, ally, npc.healerFollowDistance);
    }

    // 지정 거리까지 아군에게 접근. 단일 힐은 시전 거리(healerHealCastDistance)를 사용하여
    // "다가가서 힐하는" 설계를 실행 규칙으로 보장
    public static bool MoveNearAllyIfNeeded(NPCSimpleFSMController npc, PlayerStatus ally, float approachDistance)
    {
        if (ally == null)
            return false;

        float distance = NPCSimpleFSMController.FlatDistance(npc.transform.position, ally.transform.position);
        if (distance > approachDistance)
        {
            npc.MoveToward(ally.transform.position, Mathf.Min(1.0f, approachDistance * 0.25f));
            return true;
        }

        npc.ClearMovement();
        return false;
    }

    public static float GetHealthRatio(PlayerStatus target)
    {
        if (target == null || target.Health == null || target.Health.MaxHealth <= 0f)
            return 1f;

        return Mathf.Clamp01(target.Health.CurrentHealth / target.Health.MaxHealth);
    }

    // 스킬1(힐) 또는 스킬2(실드) 사용 시도 (행동 쿨다운 공유)
    public static bool TryUseHealerSkill(NPCSimpleFSMController npc, bool useShieldSkill, PlayerStatus intendedTarget = null)
    {
        if (!npc.IsActionReady)
            return false;

        npc.ConsumeActionCooldown();

        if (npc.skillController == null)
            return false;

        // 다가간 그 아군에게 효과가 들어가도록 의도 대상 지정
        // (미지정 시 스킬 기본 규칙인 "가장 가까운 다친 아군" 사용)
        npc.skillController.SetSupportTargetOverride(intendedTarget);

        bool used = useShieldSkill ? npc.skillController.TryUseSkill2() : npc.skillController.TryUseSkill1();
        if (!used)
            npc.skillController.SetSupportTargetOverride(null);

        return used;
    }
}
