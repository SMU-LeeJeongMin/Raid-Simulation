using System;
using UnityEngine;

/// <summary>
/// 실험 지표 수집용 전역 이벤트 허브입니다.
/// 실제 게임 로직은 그대로 두고, 각 시스템에서 발생한 전투 이벤트만 RaidMetricsLogger로 전달합니다.
/// </summary>
public static class RaidMetricsEvents
{
    public static event Action<PlayerStatus, float, float, float, float, float, GameObject> PlayerDamageResolved;
    public static event Action<PlayerStatus, float, float, float, float, GameObject> PlayerHealed;
    public static event Action<PlayerStatus, float, float, GameObject> ShieldAdded;

    public static event Action<PlayerSkillController, PlayerSkillDefinition, PlayerSkillSlot> SkillAttempted;
    public static event Action<PlayerSkillController, PlayerSkillDefinition, PlayerSkillSlot, string> SkillFailed;
    public static event Action<PlayerSkillController, PlayerSkillDefinition, PlayerSkillSlot, bool, DamageReceiver> SkillCast;

    public static event Action<PlayerBasicAttack, BasicAttackStats, DamageReceiver> BasicAttackHit;
    public static event Action<PlayerBasicAttack, BasicAttackStats, string> BasicAttackMissed;

    public static event Action<NPCSimpleFSMController, string> NPCActionSelected;

    public static event Action<SlimeAddEnemy> SlimeSpawned;
    public static event Action<SlimeAddEnemy> SlimeKilled;
    public static event Action<SlimeAddEnemy> SlimeExploded;
    public static event Action<SlimeDotZone> SlimeDotZoneCreated;

    public static void ReportPlayerDamageResolved(PlayerStatus target, float rawDamage, float shieldAbsorbed, float hpDamage, float hpBefore, float hpAfter, GameObject source)
    {
        PlayerDamageResolved?.Invoke(target, rawDamage, shieldAbsorbed, hpDamage, hpBefore, hpAfter, source);
    }

    public static void ReportPlayerHealed(PlayerStatus target, float requestedHeal, float actualHeal, float hpBefore, float hpAfter, GameObject source)
    {
        PlayerHealed?.Invoke(target, requestedHeal, actualHeal, hpBefore, hpAfter, source);
    }

    public static void ReportShieldAdded(PlayerStatus target, float amount, float duration, GameObject source)
    {
        ShieldAdded?.Invoke(target, amount, duration, source);
    }

    public static void ReportSkillAttempted(PlayerSkillController controller, PlayerSkillDefinition skill, PlayerSkillSlot slot)
    {
        SkillAttempted?.Invoke(controller, skill, slot);
    }

    public static void ReportSkillFailed(PlayerSkillController controller, PlayerSkillDefinition skill, PlayerSkillSlot slot, string reason)
    {
        SkillFailed?.Invoke(controller, skill, slot, reason ?? string.Empty);
    }

    public static void ReportSkillCast(PlayerSkillController controller, PlayerSkillDefinition skill, PlayerSkillSlot slot, bool offensive, DamageReceiver target)
    {
        SkillCast?.Invoke(controller, skill, slot, offensive, target);
    }

    public static void ReportBasicAttackHit(PlayerBasicAttack attacker, BasicAttackStats stats, DamageReceiver target)
    {
        BasicAttackHit?.Invoke(attacker, stats, target);
    }

    public static void ReportBasicAttackMiss(PlayerBasicAttack attacker, BasicAttackStats stats, string reason)
    {
        BasicAttackMissed?.Invoke(attacker, stats, reason ?? string.Empty);
    }

    public static void ReportNPCAction(NPCSimpleFSMController controller, string actionName)
    {
        NPCActionSelected?.Invoke(controller, actionName ?? string.Empty);
    }

    public static void ReportSlimeSpawned(SlimeAddEnemy slime)
    {
        SlimeSpawned?.Invoke(slime);
    }

    public static void ReportSlimeKilled(SlimeAddEnemy slime)
    {
        SlimeKilled?.Invoke(slime);
    }

    public static void ReportSlimeExploded(SlimeAddEnemy slime)
    {
        SlimeExploded?.Invoke(slime);
    }

    public static void ReportSlimeDotZoneCreated(SlimeDotZone zone)
    {
        SlimeDotZoneCreated?.Invoke(zone);
    }
}
