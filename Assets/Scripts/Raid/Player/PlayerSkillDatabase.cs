// 플레이어 스킬 DB

using System;
using UnityEngine;

public enum PlayerSkillSlot
{
    Skill1,
    Skill2,
    Ultimate
}

public enum PlayerSkillEffectType
{
    DamageOnce,
    DamageOverTime,
    MultiHitDamage,
    HealNearestAlly,
    ShieldAllAllies,
    HealOverTimeAllAllies
}

public enum PlayerSkillVFXRotationMode
{
    WorldEuler,
    CasterForward,
    DirectionToTarget
}

public enum PlayerSkillVFXProjectileStartPoint
{
    Caster,
    AboveTarget,
    TargetOffset
}

public enum PlayerSkillVFXProjectileRenderMode
{
    MovingObject,
    StretchLine
}

public enum PlayerSkillVFXLineAxis
{
    X,
    Y,
    Z
}

[CreateAssetMenu(fileName = "DB_PlayerSkills", menuName = "DungeonSim/Player Skill Database")]
public class PlayerSkillDatabase : ScriptableObject
{
    public PlayerSkillSet[] skillSets = Array.Empty<PlayerSkillSet>();

    public PlayerSkillSet GetSkillSet(string characterId)
    {
        if (skillSets == null)
            return null;

        for (int i = 0; i < skillSets.Length; i++)
        {
            PlayerSkillSet set = skillSets[i];
            if (set == null)
                continue;

            if (string.Equals(set.characterId, characterId, StringComparison.OrdinalIgnoreCase))
                return set;
        }

        return null;
    }

    public PlayerSkillDefinition GetSkill(string characterId, PlayerSkillSlot slot)
    {
        PlayerSkillSet set = GetSkillSet(characterId);
        if (set == null)
            return null;

        return set.GetSkill(slot);
    }

    [ContextMenu("Fill Default Skill Sets")]
    public void FillDefaultSkillSets()
    {
        skillSets = new[]
        {
            CreateWarrior(),
            CreateArcher(),
            CreateMage(),
            CreateHealer()
        };
    }

    private void Reset()
    {
        FillDefaultSkillSets();
    }

    private PlayerSkillSet CreateWarrior()
    {
        return new PlayerSkillSet
        {
            characterId = "warrior",
            displayName = "Warrior",
            skill1 = new PlayerSkillDefinition
            {
                skillId = "warrior_slash",
                displayName = "검으로 베기",
                slot = PlayerSkillSlot.Skill1,
                effectType = PlayerSkillEffectType.DamageOnce,
                manaCost = 20f,
                cooldown = 4f,
                ultimateGainOnUse = 12f,
                animatorStateName = "ATK1",
                actionDuration = 0.85f,
                effectDelayNormalized = 0.45f,
                damage = 160f,
                useAreaDamage = true,
                areaDamageRadius = 1.8f,
                targetVFXAtCaster = true,
                targetVFXRotationMode = PlayerSkillVFXRotationMode.CasterForward,
                targetVFXOffset = new Vector3(0f, 0.8f, 0.55f),
                targetVFXAutoDestroyDelay = 3f
            },
            skill2 = new PlayerSkillDefinition
            {
                skillId = "warrior_whirlwind",
                displayName = "검 회오리",
                slot = PlayerSkillSlot.Skill2,
                effectType = PlayerSkillEffectType.DamageOnce,
                manaCost = 30f,
                cooldown = 8f,
                ultimateGainOnUse = 18f,
                animatorStateName = "ATK2",
                actionDuration = 1.05f,
                effectDelayNormalized = 0.5f,
                damage = 240f,
                useAreaDamage = true,
                areaDamageRadius = 3.0f,
                targetVFXAutoDestroyDelay = 3f
            },
            ultimate = new PlayerSkillDefinition
            {
                skillId = "warrior_ultimate_blade_burst",
                displayName = "검기 폭발",
                slot = PlayerSkillSlot.Ultimate,
                effectType = PlayerSkillEffectType.MultiHitDamage,
                manaCost = 50f,
                ultimateCost = 100f,
                cooldown = 40f,
                ultimateGainOnUse = 0f,
                animatorStateName = "ATK3",
                actionDuration = 1.35f,
                effectDelayNormalized = 0.25f,
                damage = 120f,
                useAreaDamage = true,
                areaDamageRadius = 3.2f,
                multiHitCount = 5,
                multiHitInterval = 0.13f,
                targetVFXAutoDestroyDelay = 3f,
                lockMovementDuringSkill = true,
                suppressMovementAnimationDuringSkill = true
            }.WithStretchLineProjectile(projectileSpeed: 18f, projectileMaxLifetime: 2f, lineVisibleDuration: 0.35f)
        };
    }

    private PlayerSkillSet CreateArcher()
    {
        return new PlayerSkillSet
        {
            characterId = "archer",
            displayName = "Archer",
            skill1 = new PlayerSkillDefinition
            {
                skillId = "archer_arrow_rain",
                displayName = "화살비",
                slot = PlayerSkillSlot.Skill1,
                effectType = PlayerSkillEffectType.DamageOverTime,
                manaCost = 25f,
                cooldown = 8f,
                ultimateGainOnUse = 15f,
                animatorStateName = "ATK1",
                actionDuration = 0.9f,
                effectDelayNormalized = 0.35f,
                damage = 0f,
                tickDamage = 35f,
                useAreaDamage = true,
                areaDamageRadius = 4.0f,
                refreshAreaTargetsEachTick = true,
                duration = 3f,
                tickInterval = 0.5f,
                targetVFXAutoDestroyDelay = 2f,
                persistentVFXDuration = 3f
            },
            skill2 = new PlayerSkillDefinition
            {
                skillId = "archer_charged_explosion",
                displayName = "차징 후 폭파",
                slot = PlayerSkillSlot.Skill2,
                effectType = PlayerSkillEffectType.DamageOnce,
                manaCost = 35f,
                cooldown = 12f,
                ultimateGainOnUse = 22f,
                animatorStateName = "ATK2",
                actionDuration = 2f,
                effectDelayNormalized = 0.95f,
                damage = 300f,
                useAreaDamage = true,
                areaDamageRadius = 2.8f,
                castVFXAutoDestroyDelay = 2.2f,
                targetVFXAutoDestroyDelay = 3f
            },
            ultimate = new PlayerSkillDefinition
            {
                skillId = "archer_tornado",
                displayName = "토네이도",
                slot = PlayerSkillSlot.Ultimate,
                effectType = PlayerSkillEffectType.DamageOverTime,
                manaCost = 50f,
                ultimateCost = 100f,
                cooldown = 40f,
                animatorStateName = "ATK3",
                actionDuration = 1.2f,
                effectDelayNormalized = 0.35f,
                tickDamage = 90f,
                useAreaDamage = true,
                areaDamageRadius = 4.0f,
                refreshAreaTargetsEachTick = true,
                duration = 3f,
                tickInterval = 0.5f,
                persistentVFXDuration = 3f,
                targetVFXAutoDestroyDelay = 4f
            }
        };
    }

    private PlayerSkillSet CreateMage()
    {
        return new PlayerSkillSet
        {
            characterId = "mage",
            displayName = "Mage",
            skill1 = new PlayerSkillDefinition
            {
                skillId = "mage_ground_fire",
                displayName = "아래에서 뿜어져 나오는 불",
                slot = PlayerSkillSlot.Skill1,
                effectType = PlayerSkillEffectType.DamageOverTime,
                manaCost = 30f,
                cooldown = 8f,
                ultimateGainOnUse = 16f,
                animatorStateName = "ATK1",
                actionDuration = 0.9f,
                effectDelayNormalized = 0.4f,
                tickDamage = 45f,
                useAreaDamage = true,
                areaDamageRadius = 4.0f,
                refreshAreaTargetsEachTick = true,
                duration = 3f,
                tickInterval = 0.5f,
                persistentVFXDuration = 3f
            },
            skill2 = new PlayerSkillDefinition
            {
                skillId = "mage_line_fire",
                displayName = "일직선 불 뿜기",
                slot = PlayerSkillSlot.Skill2,
                effectType = PlayerSkillEffectType.DamageOverTime,
                manaCost = 40f,
                cooldown = 12f,
                ultimateGainOnUse = 24f,
                animatorStateName = "ATK2",
                actionDuration = 1.2f,
                effectDelayNormalized = 0.25f,
                tickDamage = 55f,
                useAreaDamage = true,
                areaDamageRadius = 3.2f,
                refreshAreaTargetsEachTick = true,
                duration = 2.5f,
                tickInterval = 0.5f,
                targetVFXAtCaster = true,
                targetVFXRotationMode = PlayerSkillVFXRotationMode.CasterForward,
                persistentVFXAtCaster = true,
                persistentVFXRotationMode = PlayerSkillVFXRotationMode.CasterForward,
                persistentVFXOffset = new Vector3(0f, 0.9f, 0.7f),
                persistentVFXDuration = 2.5f
            },
            ultimate = new PlayerSkillDefinition
            {
                skillId = "mage_meteor",
                displayName = "메테오",
                slot = PlayerSkillSlot.Ultimate,
                effectType = PlayerSkillEffectType.DamageOnce,
                manaCost = 60f,
                ultimateCost = 100f,
                cooldown = 45f,
                animatorStateName = "ATK3",
                actionDuration = 1.8f,
                effectDelayNormalized = 0.85f,
                damage = 550f,
                useAreaDamage = true,
                areaDamageRadius = 4.0f,
                castVFXAutoDestroyDelay = 2f,
                targetVFXAutoDestroyDelay = 4f
            }.WithStretchLineProjectile(projectileSpeed: 12f, projectileMaxLifetime: 3f, lineVisibleDuration: 0.45f)
        };
    }

    private PlayerSkillSet CreateHealer()
    {
        return new PlayerSkillSet
        {
            characterId = "healer",
            displayName = "Healer",
            skill1 = new PlayerSkillDefinition
            {
                skillId = "healer_heal",
                displayName = "힐",
                slot = PlayerSkillSlot.Skill1,
                effectType = PlayerSkillEffectType.HealNearestAlly,
                manaCost = 25f,
                cooldown = 5f,
                ultimateGainOnUse = 14f,
                animatorStateName = "ATK1",
                actionDuration = 0.85f,
                effectDelayNormalized = 0.45f,
                healAmount = 80f,
                castVFXAutoDestroyDelay = 2f,
                allyVFXAutoDestroyDelay = 3f
            },
            skill2 = new PlayerSkillDefinition
            {
                skillId = "healer_shield",
                displayName = "쉴드",
                slot = PlayerSkillSlot.Skill2,
                effectType = PlayerSkillEffectType.ShieldAllAllies,
                manaCost = 35f,
                cooldown = 12f,
                ultimateGainOnUse = 22f,
                animatorStateName = "ATK2",
                actionDuration = 1f,
                effectDelayNormalized = 0.45f,
                shieldAmount = 100f,
                shieldDuration = 10f,
                castVFXAutoDestroyDelay = 2f,
                allyVFXAutoDestroyDelay = 3f
            },
            ultimate = new PlayerSkillDefinition
            {
                skillId = "healer_ultimate_angelic_heal",
                displayName = "광역 힐",
                slot = PlayerSkillSlot.Ultimate,
                effectType = PlayerSkillEffectType.HealOverTimeAllAllies,
                manaCost = 60f,
                ultimateCost = 100f,
                cooldown = 45f,
                animatorStateName = "ATK3",
                actionDuration = 5f,
                effectDelayNormalized = 0.05f,
                tickHealAmount = 80f,
                duration = 5f,
                tickInterval = 1f,
                lockBasicAttackDuringSkill = true,
                lockMovementDuringSkill = false,
                suppressMovementAnimationDuringSkill = false,
                movementSpeedMultiplier = 1.35f,
                castVFXAutoDestroyDelay = 5f,
                auraVFXAutoDestroyDelay = 5.5f,
                allyVFXAutoDestroyDelay = 2f
            }
        };
    }
}

[Serializable]
public class PlayerSkillSet
{
    [Header("Class")]
    public string characterId = "warrior";
    public string displayName = "Warrior";

    [Header("Skills")]
    public PlayerSkillDefinition skill1 = new PlayerSkillDefinition { slot = PlayerSkillSlot.Skill1, animatorStateName = "ATK1" };
    public PlayerSkillDefinition skill2 = new PlayerSkillDefinition { slot = PlayerSkillSlot.Skill2, animatorStateName = "ATK2" };
    public PlayerSkillDefinition ultimate = new PlayerSkillDefinition { slot = PlayerSkillSlot.Ultimate, animatorStateName = "ATK3", ultimateCost = 100f };

    public PlayerSkillDefinition GetSkill(PlayerSkillSlot slot)
    {
        switch (slot)
        {
            case PlayerSkillSlot.Skill1:
                return skill1;
            case PlayerSkillSlot.Skill2:
                return skill2;
            case PlayerSkillSlot.Ultimate:
                return ultimate;
            default:
                return null;
        }
    }
}

[Serializable]
public class PlayerSkillDefinition
{
    [Header("Identity")]
    public string skillId = "skill";
    public string displayName = "Skill";
    public PlayerSkillSlot slot = PlayerSkillSlot.Skill1;
    public PlayerSkillEffectType effectType = PlayerSkillEffectType.DamageOnce;

    [Header("Cost / Cooldown")]
    [Min(0f)] public float manaCost = 0f;
    [Min(0f)] public float ultimateCost = 0f;
    [Min(0f)] public float cooldown = 3f;
    [Min(0f)] public float ultimateGainOnUse = 10f;

    [Header("Animation")]
    public AnimationClip animationClip;
    public string animatorStateName = "ATK1";
    [Min(0.05f)] public float actionDuration = 0.8f;
    [Range(0f, 1f)] public float effectDelayNormalized = 0.45f;
    [Min(0f)] public float extraRecoveryTime = 0f;

    [Header("Action Lock")]
    public bool lockMovementDuringSkill = true;
    public bool suppressMovementAnimationDuringSkill = true;
    public bool lockBasicAttackDuringSkill = true;
    public bool faceTargetOnSkill = true;
    [Min(0.1f)] public float movementSpeedMultiplier = 1f;

    [Header("Damage")]
    [Min(0f)] public float damage = 100f;
    [Min(0f)] public float tickDamage = 30f;
    [Min(1)] public int multiHitCount = 1;
    [Min(0.01f)] public float multiHitInterval = 0.1f;

    [Header("Area Damage - Offensive Skills")]
    [Tooltip("켜면 공격형 스킬이 주 타겟 주변의 모든 적에게 피해를 줍니다. 기본공격에는 적용되지 않습니다.")]
    public bool useAreaDamage = false;
    [Min(0.1f)] public float areaDamageRadius = 2.5f;
    [Tooltip("DOT 스킬일 때 매 tick마다 범위 안의 적을 다시 찾습니다. 꺼두면 시작 시점의 대상들만 피해를 받습니다.")]
    public bool refreshAreaTargetsEachTick = true;
    [Min(1)] public int maxAreaTargets = 16;

    [Header("Duration / Tick")]
    [Min(0f)] public float duration = 0f;
    [Min(0.01f)] public float tickInterval = 1f;

    [Header("Heal / Shield")]
    [Min(0f)] public float healAmount = 50f;
    [Min(0f)] public float tickHealAmount = 50f;
    [Min(0f)] public float shieldAmount = 50f;
    [Min(0.1f)] public float shieldDuration = 10f;

    [Header("VFX - Cast")]
    public GameObject castVFXPrefab;
    public bool attachCastVFXToCaster = true;
    public Vector3 castVFXOffset = new Vector3(0f, 0.9f, 0.3f);
    public PlayerSkillVFXRotationMode castVFXRotationMode = PlayerSkillVFXRotationMode.CasterForward;
    public Vector3 castVFXEuler = Vector3.zero;
    public Vector3 castVFXScale = Vector3.one;
    [Min(0.05f)] public float castVFXAutoDestroyDelay = 3f;

    [Header("VFX - Target / Impact")]
    public GameObject targetVFXPrefab;
    public bool targetVFXAtCaster = false;
    public string targetAnchorName = "VFXHitAnchor";
    public Vector3 targetVFXOffset = Vector3.zero;
    public PlayerSkillVFXRotationMode targetVFXRotationMode = PlayerSkillVFXRotationMode.WorldEuler;
    public Vector3 targetVFXEuler = Vector3.zero;
    public Vector3 targetVFXScale = Vector3.one;
    [Min(0.05f)] public float targetVFXAutoDestroyDelay = 3f;

    [Header("VFX - Projectile")]
    public bool useProjectileVFX = false;
    public bool applyDamageOnProjectileImpact = false;
    public GameObject projectileVFXPrefab;
    public PlayerSkillVFXProjectileRenderMode projectileRenderMode = PlayerSkillVFXProjectileRenderMode.MovingObject;
    public PlayerSkillVFXProjectileStartPoint projectileStartPoint = PlayerSkillVFXProjectileStartPoint.Caster;
    public Vector3 projectileVFXStartOffset = new Vector3(0f, 0.9f, 0.4f);
    public PlayerSkillVFXRotationMode projectileVFXRotationMode = PlayerSkillVFXRotationMode.DirectionToTarget;
    public Vector3 projectileVFXEuler = Vector3.zero;
    public Vector3 projectileVFXScale = Vector3.one;
    [Min(0.1f)] public float projectileSpeed = 12f;
    [Min(0.05f)] public float projectileMaxLifetime = 2f;
    public bool projectileLookAtTarget = true;
    public bool spawnTargetVFXOnProjectileImpact = true;

    [Header("VFX - Projectile Line Mode")]
    public PlayerSkillVFXLineAxis projectileLineAxis = PlayerSkillVFXLineAxis.Z;
    [Min(0.001f)] public float projectileLineLengthScale = 1f;
    [Min(0.001f)] public float projectileLineThicknessScale = 1f;
    [Min(0.01f)] public float projectileLineVisibleDuration = 0.35f;
    public float projectileLineImpactDelay = -1f;

    [Header("VFX - Persistent Area")]
    public GameObject persistentVFXPrefab;
    public bool persistentVFXAtCaster = false;
    public Vector3 persistentVFXOffset = Vector3.zero;
    public PlayerSkillVFXRotationMode persistentVFXRotationMode = PlayerSkillVFXRotationMode.WorldEuler;
    public Vector3 persistentVFXEuler = Vector3.zero;
    public Vector3 persistentVFXScale = Vector3.one;
    [Min(0.05f)] public float persistentVFXDuration = 3f;

    [Header("VFX - Ally")]
    public GameObject allyVFXPrefab;
    public string allyVFXAnchorName = "VFXHitAnchor";
    public Vector3 allyVFXOffset = new Vector3(0f, 1.0f, 0f);
    public Vector3 allyVFXEuler = Vector3.zero;
    public Vector3 allyVFXScale = Vector3.one;
    [Min(0.05f)] public float allyVFXAutoDestroyDelay = 3f;

    [Header("VFX - Ultimate Aura")]
    public GameObject auraVFXPrefab;
    public bool attachAuraVFXToCaster = true;
    public Vector3 auraVFXOffset = Vector3.zero;
    public Vector3 auraVFXEuler = Vector3.zero;
    public Vector3 auraVFXScale = Vector3.one;
    [Min(0.05f)] public float auraVFXAutoDestroyDelay = 5f;

    // 시전자에서 대상으로 뻗는 직선(StretchLine) 투사체 공통 설정 적용
    // 워리어와 메이지 궁극기에 중복 기술되어 있던 12줄의 단일 정의
    public PlayerSkillDefinition WithStretchLineProjectile(float projectileSpeed, float projectileMaxLifetime, float lineVisibleDuration)
    {
        useProjectileVFX = true;
        applyDamageOnProjectileImpact = true;
        projectileRenderMode = PlayerSkillVFXProjectileRenderMode.StretchLine;
        projectileStartPoint = PlayerSkillVFXProjectileStartPoint.Caster;
        projectileVFXStartOffset = new Vector3(0f, 0.9f, 0.5f);
        projectileVFXRotationMode = PlayerSkillVFXRotationMode.DirectionToTarget;
        projectileLineAxis = PlayerSkillVFXLineAxis.Z;
        projectileLineLengthScale = 1f;
        projectileLineThicknessScale = 1f;
        projectileLineVisibleDuration = lineVisibleDuration;
        projectileLineImpactDelay = -1f;
        this.projectileSpeed = projectileSpeed;
        this.projectileMaxLifetime = projectileMaxLifetime;
        spawnTargetVFXOnProjectileImpact = true;
        return this;
    }
}
