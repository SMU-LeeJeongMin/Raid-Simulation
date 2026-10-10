using UnityEngine;

/// <summary>
/// 지시의 반영 규칙 (Arbiter). 지시별 행동군 편향 계수의 단일 정의.
///
/// - Utility 정책은 점수에 계수를 곱하고, GNN 정책은 로짓에 ln(계수)를 더함.
///   두 방식은 행동 순위에 같은 방향의 이동을 만들므로 정책 간 반영이 등가
/// - 안전 행동(회피, 산개)의 계수는 v1의 모든 지시에서 1.0, v2 경계 태세에서만 가산 (지시가 생존을 약화시키지 못함)
/// - Mask가 제외한 행동은 편향과 무관하게 후보가 아님 (Mask 불가침)
/// - Free는 모든 계수 1.0 (조율자 없는 조건과 수학적으로 동일한 거동의 보장)
///
/// 지시 체계 v2 (7단계 진단 반영, CommandSet = 2).
/// - RGCN 하위 정책의 약점: 장판 피격 과다(판당 약 90회 대 규칙 60회), 장판 안 회피 선택 60% (규칙 78%),
///   긴 시전 스킬(Skill2, 궁극기) 사용 중 회피 불가로 피격 누적. NPC 힐러가 없는 블록에서 비행 국면 진입 시 체력 부족으로 전멸
/// - HoldDefensive를 경계 태세로 재정의: 안전 행동(회피, 산개) 가산(계수 2.0), 비행 중 재집결 가산(2.0), 그 외 1.0
/// - HandleSlimes v2: 슬라임 행동 3.0, 안전 행동 2.0 (슬라임 폭발이 남기는 지속 장판 억제)
/// - 안전 행동은 가산만 허용 (지시가 생존을 약화시키지 못한다는 원칙 유지)
/// - 그 외 지시는 v1과 같은 계수 (긴 시전 공격은 보스 공격과 같은 계수)
/// </summary>
public static class NpcCommandBias
{
    // 학습형 하위 정책의 로짓 가산 세기 (RaidCoordinator.commandStrength가 매 프레임 설정, 1 = 기존 세기)
    public static float LogitScale = 1f;

    // 지시 체계 버전 (RaidCoordinator.commandSet이 매 프레임 설정). 1 = v1 계수표, 2 = 경계 태세 포함 v2 계수표
    public static int CommandSet = 2;

    private enum ActionGroup
    {
        BossAttack,     // 9, 13, 14 (기본 공격, Skill1)
        LongCast,       // 15, 16 (Skill2, 궁극기: 시전 중 회피 불가)
        BossApproach,   // 2
        Slime,          // 10, 11
        Support,        // 6, 7, 8
        Formation,      // 1, 4
        Regroup,        // 5 (보스 비행 중에만 후보)
        Safety          // 3, 12
    }

    public static float Factor(NPCSimpleFSMController npc, NpcAction action)
    {
        return Factor(CommandBoard.Get(npc).type, action);
    }

    // GNN 로짓 가산용 (argmax 순위 이동이 Utility의 곱과 같은 방향)
    public static float LogitBias(NPCSimpleFSMController npc, NpcAction action)
    {
        return Mathf.Log(Factor(npc, action)) * LogitScale;
    }

    public static float Factor(NpcCommandType command, NpcAction action)
    {
        if (command == NpcCommandType.Free)
            return 1f;

        ActionGroup group = GroupOf(action);

        // v2 경계 태세: 안전 행동 가산, 비행 중 재집결 가산 (나머지 행동군은 1.0)
        // 1차안(대형 1.3, 접근 0.6, 기본 공격 0.8)은 거리 유지와 플레이어 추종만 반복하고 공격하지 않는 문제 확인으로 폐기
        // 긴 시전 억제(0.5, v2b와 v2c)는 힐러 블록 시간 초과 증가(40판 중 3 → 10)로 폐기 (안전 행동은 장판 안에서만 후보라 장판 밖 화력 손실 없음)
        // 재집결은 보스 비행 중에만 후보이므로 지상 전투에는 영향 없음 (비행 국면에서 힐러 플레이어 곁 유지)
        if (CommandSet >= 2 && command == NpcCommandType.HoldDefensive)
        {
            switch (group)
            {
                case ActionGroup.Safety: return 2.0f;
                case ActionGroup.Regroup: return 2.0f;
            }
            return 1f;
        }

        // v2 슬라임 처리: 슬라임 행동 강한 가산과 회피 가산 (보스 무적 중이라 화력 손실 없음)
        // 슬라임 폭발 1회당 지속 장판이 남아 판당 피격 약 5회 증가 (RGCN 판당 폭발 약 6회 대 규칙 약 2회)
        if (CommandSet >= 2 && command == NpcCommandType.HandleSlimes)
        {
            switch (group)
            {
                case ActionGroup.Slime: return 3.0f;
                case ActionGroup.Safety: return 2.0f;
                case ActionGroup.BossAttack:
                case ActionGroup.LongCast:
                case ActionGroup.BossApproach: return 0.6f;
                case ActionGroup.Support:
                case ActionGroup.Formation:
                case ActionGroup.Regroup: return 0.8f;
            }
            return 1f;
        }

        if (group == ActionGroup.Safety)
            return 1f;

        // v1 계수표에서 긴 시전 공격은 보스 공격과, 재집결은 대형과 같은 계수
        if (group == ActionGroup.LongCast)
            group = ActionGroup.BossAttack;
        if (group == ActionGroup.Regroup)
            group = ActionGroup.Formation;

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
            case NpcAction.AttackBoss:
                return ActionGroup.BossAttack;

            case NpcAction.AttackBossSkill2:
            case NpcAction.AttackBossUltimate:
                return ActionGroup.LongCast;

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
                return ActionGroup.Formation;

            case NpcAction.RegroupDuringBossFly:
                return ActionGroup.Regroup;

            default:
                return ActionGroup.Safety;
        }
    }
}
