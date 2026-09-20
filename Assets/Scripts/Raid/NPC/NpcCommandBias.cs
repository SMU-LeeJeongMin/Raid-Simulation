using UnityEngine;

/// <summary>
/// 지시의 반영 규칙 (Arbiter). 지시별 행동군 편향 계수의 단일 정의.
///
/// - Utility 정책은 점수에 계수를 곱하고, GNN 정책은 로짓에 ln(계수)를 더함.
///   두 방식은 행동 순위에 같은 방향의 이동을 만들므로 정책 간 반영이 등가
/// - 안전 행동(회피, 산개)의 계수는 모든 지시에서 1.0 (지시가 생존을 약화시키지 못함)
/// - Mask가 제외한 행동은 편향과 무관하게 후보가 아님 (Mask 불가침)
/// - Free는 모든 계수 1.0 (조율자 없는 조건과 수학적으로 동일한 거동의 보장)
/// </summary>
public static class NpcCommandBias
{
    private enum ActionGroup
    {
        BossAttack,     // 13~16
        BossApproach,   // 2
        Slime,          // 10, 11
        Support,        // 6, 7, 8
        Formation,      // 1, 4, 5
        Safety          // 3, 12
    }

    public static float Factor(NPCSimpleFSMController npc, NpcAction action)
    {
        return Factor(CommandBoard.Get(npc).type, action);
    }

    // GNN 로짓 가산용 (argmax 순위 이동이 Utility의 곱과 같은 방향)
    public static float LogitBias(NPCSimpleFSMController npc, NpcAction action)
    {
        return Mathf.Log(Factor(npc, action));
    }

    public static float Factor(NpcCommandType command, NpcAction action)
    {
        if (command == NpcCommandType.Free)
            return 1f;

        ActionGroup group = GroupOf(action);
        if (group == ActionGroup.Safety)
            return 1f;

        switch (command)
        {
            case NpcCommandType.FocusBoss:
                switch (group)
                {
                    // v1 값으로 고정 (규칙 조율의 기준선).
                    // v1.1의 강화 시도(1.8)는 healer 블록에서 전멸 증가로 악화 확인,
                    // 10판 블록의 판별력 한계도 확인되어 수동 튜닝 중단 (이후는 RL 조율자)
                    case ActionGroup.BossAttack: return 1.35f;
                    case ActionGroup.BossApproach: return 1.25f;
                    case ActionGroup.Slime: return 0.6f;
                    case ActionGroup.Support: return 0.8f;
                    case ActionGroup.Formation: return 0.7f;
                }
                break;

            case NpcCommandType.HandleSlimes:
                switch (group)
                {
                    case ActionGroup.BossAttack: return 0.6f;
                    case ActionGroup.BossApproach: return 0.6f;
                    case ActionGroup.Slime: return 1.5f;
                    case ActionGroup.Support: return 0.8f;
                    case ActionGroup.Formation: return 0.8f;
                }
                break;

            case NpcCommandType.ProtectAlly:
                switch (group)
                {
                    case ActionGroup.BossAttack: return 0.7f;
                    case ActionGroup.BossApproach: return 0.7f;
                    case ActionGroup.Slime: return 0.7f;
                    case ActionGroup.Support: return 1.5f;
                    case ActionGroup.Formation: return 0.9f;
                }
                break;

            case NpcCommandType.HoldDefensive:
                switch (group)
                {
                    case ActionGroup.BossAttack: return 0.6f;
                    case ActionGroup.BossApproach: return 0.6f;
                    case ActionGroup.Slime: return 0.8f;
                    case ActionGroup.Support: return 1.2f;
                    case ActionGroup.Formation: return 1.3f;
                }
                break;
        }

        return 1f;
    }

    private static ActionGroup GroupOf(NpcAction action)
    {
        switch (action)
        {
            case NpcAction.AttackBossBasic:
            case NpcAction.AttackBossSkill1:
            case NpcAction.AttackBossSkill2:
            case NpcAction.AttackBossUltimate:
            case NpcAction.AttackBoss:
                return ActionGroup.BossAttack;

            case NpcAction.MoveToBoss:
                return ActionGroup.BossApproach;

            case NpcAction.AttackSlime:
            case NpcAction.MoveToSlime:
                return ActionGroup.Slime;

            case NpcAction.MoveToAllyAndHeal:
            case NpcAction.ShieldDangerAlly:
            case NpcAction.ShieldParty:
                return ActionGroup.Support;

            case NpcAction.FollowPlayer:
            case NpcAction.KeepDistance:
            case NpcAction.RegroupDuringBossFly:
                return ActionGroup.Formation;

            default:
                return ActionGroup.Safety;
        }
    }
}
