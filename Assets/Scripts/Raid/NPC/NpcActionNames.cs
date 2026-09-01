/// <summary>
/// NPC 정책 행동의 단일 정의.
/// 정수 값은 그래프 스냅샷의 policy_action_id로 그대로 기록되므로
/// 데이터 수집 시작 이후에는 기존 번호를 변경하지 않고 뒤에만 추가.
/// (GNN_SCHEMA_V1, Node 문서 4.7 기준. MoveToSafePosition = 3은 문서 예시와 일치)
///
/// 13~16은 보스 공격의 스킬 슬롯별 분리 (v2).
/// 기존 AttackBoss(9)는 "스킬을 정책 내부 우선순위로 자동 선택"하는 의미였으나,
/// 스킬 선택 자체를 학습 대상으로 만들기 위해 슬롯별 행동으로 대체.
/// 9번은 과거 수집 데이터와의 호환을 위해 번호만 유지하고 신규 정책에서는 사용하지 않음.
/// </summary>
public enum NpcAction
{
    None = 0,
    FollowPlayer = 1,
    MoveToBoss = 2,
    MoveToSafePosition = 3,
    KeepDistance = 4,
    RegroupDuringBossFly = 5,
    MoveToAllyAndHeal = 6,
    ShieldDangerAlly = 7,
    ShieldParty = 8,
    AttackBoss = 9,              // 사용 중지 (슬롯별 행동 13~16으로 대체)
    AttackSlime = 10,
    MoveToSlime = 11,
    SpreadFromParty = 12,
    AttackBossBasic = 13,
    AttackBossSkill1 = 14,
    AttackBossSkill2 = 15,
    AttackBossUltimate = 16
}

/// <summary>
/// NPC 행동 지표에 기록되는 액션 이름의 단일 정의.
/// CSV 및 RaidMetricsLogger 집계와의 호환을 위해 문자열 상수 유지.
/// </summary>
public static class NpcActionNames
{
    public const string MoveToSafePosition = "MoveToSafePosition";
    public const string FollowPlayer = "FollowPlayer";
    public const string MoveToBoss = "MoveToBoss";
    public const string KeepDistance = "KeepDistance";
    public const string RegroupDuringBossFly = "RegroupDuringBossFly";
    public const string MoveToAllyAndHeal = "MoveToAllyAndHeal";
    public const string ShieldDangerAlly = "ShieldDangerAlly";
    public const string ShieldParty = "ShieldParty";
    public const string AttackBoss = "AttackBoss";
    public const string AttackSlime = "AttackSlime";
    public const string MoveToSlime = "MoveToSlime";
    public const string SpreadFromParty = "SpreadFromParty";
    public const string AttackBossBasic = "AttackBossBasic";
    public const string AttackBossSkill1 = "AttackBossSkill1";
    public const string AttackBossSkill2 = "AttackBossSkill2";
    public const string AttackBossUltimate = "AttackBossUltimate";
}

/// <summary>
/// NpcAction과 지표용 이름, 공통 행동 상태(activity_id) 사이의 변환.
/// </summary>
public static class NpcActions
{
    // 행동 번호의 개수 (Mask 배열과 모델 출력 차원의 기준)
    public const int ActionCount = 17;

    // CSV 지표에 기록되는 이름 반환 (None은 빈 문자열)
    public static string GetName(NpcAction action)
    {
        switch (action)
        {
            case NpcAction.FollowPlayer: return NpcActionNames.FollowPlayer;
            case NpcAction.MoveToBoss: return NpcActionNames.MoveToBoss;
            case NpcAction.MoveToSafePosition: return NpcActionNames.MoveToSafePosition;
            case NpcAction.KeepDistance: return NpcActionNames.KeepDistance;
            case NpcAction.RegroupDuringBossFly: return NpcActionNames.RegroupDuringBossFly;
            case NpcAction.MoveToAllyAndHeal: return NpcActionNames.MoveToAllyAndHeal;
            case NpcAction.ShieldDangerAlly: return NpcActionNames.ShieldDangerAlly;
            case NpcAction.ShieldParty: return NpcActionNames.ShieldParty;
            case NpcAction.AttackBoss: return NpcActionNames.AttackBoss;
            case NpcAction.AttackSlime: return NpcActionNames.AttackSlime;
            case NpcAction.MoveToSlime: return NpcActionNames.MoveToSlime;
            case NpcAction.SpreadFromParty: return NpcActionNames.SpreadFromParty;
            case NpcAction.AttackBossBasic: return NpcActionNames.AttackBossBasic;
            case NpcAction.AttackBossSkill1: return NpcActionNames.AttackBossSkill1;
            case NpcAction.AttackBossSkill2: return NpcActionNames.AttackBossSkill2;
            case NpcAction.AttackBossUltimate: return NpcActionNames.AttackBossUltimate;
            default: return string.Empty;
        }
    }

    // Node 문서 4.7의 공통 행동 상태 코드
    // 0=NONE, 1=IDLE, 2=MOVE, 3=ATTACK, 4=CAST, 5=EVADE, 6=SUPPORT, 7=DISABLED, 8=DEAD
    public static int GetActivityId(NpcAction action)
    {
        switch (action)
        {
            case NpcAction.FollowPlayer:
            case NpcAction.MoveToBoss:
            case NpcAction.MoveToSlime:
            case NpcAction.MoveToAllyAndHeal:
                return 2; // MOVE

            case NpcAction.AttackBoss:
            case NpcAction.AttackSlime:
            case NpcAction.AttackBossBasic:
                return 3; // ATTACK

            case NpcAction.AttackBossSkill1:
            case NpcAction.AttackBossSkill2:
            case NpcAction.AttackBossUltimate:
                return 4; // CAST

            case NpcAction.MoveToSafePosition:
            case NpcAction.KeepDistance:
            case NpcAction.RegroupDuringBossFly:
            case NpcAction.SpreadFromParty:
                return 5; // EVADE

            case NpcAction.ShieldDangerAlly:
            case NpcAction.ShieldParty:
                return 6; // SUPPORT

            default:
                return 0; // NONE
        }
    }

    // 보스 공격 계열 행동인지 (거리 조건과 실행 경로가 동일한 묶음)
    public static bool IsBossAttack(NpcAction action)
    {
        return action == NpcAction.AttackBossBasic
            || action == NpcAction.AttackBossSkill1
            || action == NpcAction.AttackBossSkill2
            || action == NpcAction.AttackBossUltimate
            || action == NpcAction.AttackBoss;
    }

    // 보스 공격 행동이 사용하는 스킬 슬롯 (기본 공격은 null)
    public static PlayerSkillSlot? GetSkillSlot(NpcAction action)
    {
        switch (action)
        {
            case NpcAction.AttackBossSkill1: return PlayerSkillSlot.Skill1;
            case NpcAction.AttackBossSkill2: return PlayerSkillSlot.Skill2;
            case NpcAction.AttackBossUltimate: return PlayerSkillSlot.Ultimate;
            default: return null;
        }
    }
}
