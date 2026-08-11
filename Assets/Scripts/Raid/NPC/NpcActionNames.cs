/// <summary>
/// NPC 행동 지표에 기록되는 액션 이름의 단일 정의.
/// FSM 컨트롤러의 문자열 리터럴과 RaidMetricsLogger의 Contains 매칭에
/// 분산되어 있던 이름을 상수로 통합하여 오타로 인한 집계 누락 방지.
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
}
