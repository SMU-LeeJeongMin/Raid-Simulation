/// <summary>
/// GNN_SCHEMA_V1의 정수 코드 단일 정의 (Node 문서, Message passing 문서 기준).
/// 데이터 수집 시작 이후에는 기존 번호를 변경하지 않고 뒤에만 추가.
/// </summary>
public static class GnnSchema
{
    public const string Version = "GNN_SCHEMA_V1";

    // ---------- 노드 종류 (Node 문서 3장) ----------
    public const int NodeTypeCharacter = 1;
    public const int NodeTypeBoss = 2;
    public const int NodeTypeSlime = 3;
    public const int NodeTypeSkill = 4;        // v1 수집에서는 미사용 (자리 예약)
    public const int NodeTypeDangerZone = 5;
    public const int NodeTypeObjective = 6;

    // ---------- 노드 계열 ----------
    public const int FamilyActor = 1;
    public const int FamilyEffect = 2;
    public const int FamilyObjective = 3;

    // ---------- 팀 ----------
    public const int TeamNeutral = 0;
    public const int TeamParty = 1;
    public const int TeamEnemy = 2;

    // ---------- 조종 주체 ----------
    public const int ControllerNone = 0;
    public const int ControllerHumanPlayer = 1;
    public const int ControllerPartyAI = 2;
    public const int ControllerEnemyAI = 3;
    public const int ControllerEnvironment = 4;

    // ---------- Relation (Message passing 문서 3장) ----------
    public const int RelationSelfLoop = 0;
    public const int RelationOwnsSkill = 1;       // v1 수집에서는 미사용 (SKILL 노드와 함께 예약)
    public const int RelationTargets = 2;
    public const int RelationThreatens = 3;
    public const int RelationCanAttack = 4;
    public const int RelationCanApplyEffect = 5;  // v1 수집에서는 미사용 (SKILL 노드와 함께 예약)
    public const int RelationCanSupport = 6;
    public const int RelationAffects = 7;
    public const int RelationNeedsSupport = 8;
    public const int RelationRequires = 9;
    public const int RelationPartyRelation = 10;
    public const int RelationCount = 11;

    // ---------- 직업과 역할 ----------
    public const int ClassNone = 0;
    public const int ClassWarrior = 1;
    public const int ClassArcher = 2;
    public const int ClassMage = 3;
    public const int ClassHealer = 4;

    public const int RoleNone = 0;
    public const int RoleFrontline = 1;
    public const int RoleDamage = 2;
    public const int RoleSupport = 3;

    // characterId 문자열("warrior", "archer" 등)에서 직업 코드 변환
    public static int ClassIdFromCharacterId(string characterId)
    {
        string id = (characterId ?? string.Empty).ToLowerInvariant();

        if (id.Contains("warrior") || id.Contains("tank"))
            return ClassWarrior;
        if (id.Contains("archer"))
            return ClassArcher;
        if (id.Contains("mage"))
            return ClassMage;
        if (id.Contains("healer"))
            return ClassHealer;

        return ClassNone;
    }

    public static int RoleIdFromClassId(int classId)
    {
        switch (classId)
        {
            case ClassWarrior: return RoleFrontline;
            case ClassArcher:
            case ClassMage: return RoleDamage;
            case ClassHealer: return RoleSupport;
            default: return RoleNone;
        }
    }

    // ---------- 효과와 범위 (Node 문서 4.8) ----------
    public const int EffectNone = 0;
    public const int EffectDamage = 1;
    public const int EffectDot = 2;
    public const int EffectHeal = 3;
    public const int EffectHot = 4;
    public const int EffectShield = 5;

    public const int ScopeNone = 0;
    public const int ScopeSingle = 1;
    public const int ScopeArea = 2;
    public const int ScopeParty = 3;

    public const int ShapeNone = 0;
    public const int ShapeCircle = 1;
    public const int ShapeLine = 2;
    public const int ShapeCone = 3;
    public const int ShapeProjectile = 4;
    public const int ShapeTracking = 5;

    public static int ShapeIdFromZoneShape(DangerZoneShape shape)
    {
        switch (shape)
        {
            case DangerZoneShape.Circle: return ShapeCircle;
            case DangerZoneShape.Rectangle: return ShapeLine;
            case DangerZoneShape.Cone: return ShapeCone;
            default: return ShapeNone;
        }
    }

    // ---------- 스킬 슬롯 (Node 문서 4.9) ----------
    // 0은 스킬이 아닌 노드를 뜻하는 NONE으로 사용
    public const int SkillSlotNone = 0;
    public const int SkillSlotSkill1 = 1;
    public const int SkillSlotSkill2 = 2;
    public const int SkillSlotUltimate = 3;

    public static int SkillSlotId(PlayerSkillSlot slot)
    {
        switch (slot)
        {
            case PlayerSkillSlot.Skill1: return SkillSlotSkill1;
            case PlayerSkillSlot.Skill2: return SkillSlotSkill2;
            case PlayerSkillSlot.Ultimate: return SkillSlotUltimate;
            default: return SkillSlotNone;
        }
    }

    // 게임의 스킬 효과 종류를 스키마 코드로 변환
    public static int EffectTypeFromSkill(PlayerSkillEffectType effectType)
    {
        switch (effectType)
        {
            case PlayerSkillEffectType.DamageOnce:
            case PlayerSkillEffectType.MultiHitDamage:
                return EffectDamage;
            case PlayerSkillEffectType.DamageOverTime:
                return EffectDot;
            case PlayerSkillEffectType.HealNearestAlly:
                return EffectHeal;
            case PlayerSkillEffectType.HealOverTimeAllAllies:
                return EffectHot;
            case PlayerSkillEffectType.ShieldAllAllies:
                return EffectShield;
            default:
                return EffectNone;
        }
    }

    // 스킬이 아군을 대상으로 하는지 (지원 스킬 판정)
    public static bool IsSupportEffect(PlayerSkillEffectType effectType)
    {
        return effectType == PlayerSkillEffectType.HealNearestAlly
            || effectType == PlayerSkillEffectType.HealOverTimeAllAllies
            || effectType == PlayerSkillEffectType.ShieldAllAllies;
    }

    // ---------- 보조 상태 코드 (Node 문서 4.9) ----------
    public const int ZoneStageNone = 0;
    public const int ZoneStageWarning = 1;
    public const int ZoneStageActive = 2;

    public const int ObjectiveNone = 0;
    public const int ObjectiveKillSlimes = 1;

    // DangerZone 생성 원인
    public const int ZoneSourceNone = 0;
    public const int ZoneSourceBoss = 1;
    public const int ZoneSourceSlime = 2;

    public static int ZoneSourceFromCategory(DangerZoneCategory category)
    {
        switch (category)
        {
            case DangerZoneCategory.SlimeDOT: return ZoneSourceSlime;
            case DangerZoneCategory.Unknown: return ZoneSourceNone;
            default: return ZoneSourceBoss;
        }
    }

    public static int ZoneStageFromCategory(DangerZoneCategory category)
    {
        // Telegraph는 발동 전 경고, 나머지는 발동 중으로 분류
        return category == DangerZoneCategory.Telegraph ? ZoneStageWarning : ZoneStageActive;
    }

    // ---------- 공통 행동 상태 (Node 문서 4.7) ----------
    public const int ActivityNone = 0;
    public const int ActivityIdle = 1;
    public const int ActivityMove = 2;
    public const int ActivityAttack = 3;
    public const int ActivityCast = 4;
    public const int ActivityEvade = 5;
    public const int ActivitySupport = 6;
    public const int ActivityDisabled = 7;
    public const int ActivityDead = 8;
}
