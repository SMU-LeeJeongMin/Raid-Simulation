// 직업별 기본 공격 정보를 저장하는 ScriptableObject
// 데미지, 공격 속도, 사거리, 공격 애니메이션(ATK0/Shoot), 자동 타겟팅 방식

using System;
using UnityEngine;

[CreateAssetMenu(fileName = "DB_BasicAttackStats", menuName = "DungeonSim/Basic Attack Stats Database")]
public class BasicAttackStatsDatabase : ScriptableObject
{
    public BasicAttackStats[] classStats = Array.Empty<BasicAttackStats>();

    public BasicAttackStats GetStats(string characterId)
    {
        if (classStats == null)
            return null;

        for (int i = 0; i < classStats.Length; i++)
        {
            BasicAttackStats stats = classStats[i];
            if (stats == null)
                continue;

            if (string.Equals(stats.characterId, characterId, StringComparison.OrdinalIgnoreCase))
                return stats;
        }

        return null;
    }

    [ContextMenu("Fill Default Class Stats")]
    public void FillDefaultClassStats()
    {
        // 직업 간에 달라지는 값만 지정, 공통값은 BasicAttackStats 필드 초기화 값 사용
        classStats = new[]
        {
            MakeStats("warrior", "Warrior", damage: 85f, ultimateGain: 4f, duration: 0.95f, range: 1.0f, hitRadius: 0.65f, attackStateName: "ATK0"),
            MakeStats("archer", "Archer", damage: 55f, ultimateGain: 3f, duration: 0.60f, range: 6.0f, hitRadius: 0.45f, attackStateName: "Shoot"),
            MakeStats("mage", "Mage", damage: 70f, ultimateGain: 4f, duration: 0.80f, range: 5.0f, hitRadius: 0.5f, attackStateName: "ATK0"),
            MakeStats("healer", "Healer", damage: 40f, ultimateGain: 4f, duration: 1.05f, range: 4.5f, hitRadius: 0.45f, attackStateName: "ATK0")
        };
    }

    private static BasicAttackStats MakeStats(string id, string displayName, float damage, float ultimateGain, float duration, float range, float hitRadius, string attackStateName)
    {
        return new BasicAttackStats
        {
            characterId = id,
            displayName = displayName,
            damage = damage,
            ultimateGainOnHit = ultimateGain,
            attackDuration = duration,
            range = range,
            hitRadius = hitRadius,
            animatorStateName = attackStateName
        };
    }

    private void Reset()
    {
        FillDefaultClassStats();
    }
}

[Serializable]
public class BasicAttackStats
{
    [Header("Class")]
    public string characterId = "warrior";
    public string displayName = "Warrior";

    [Header("Damage")]
    [Min(0f)] public float damage = 50f;

    [Header("Ultimate Gauge")]
    [Min(0f)] public float ultimateGainOnHit = 4f;

    [Header("Timing")]
    [Min(0.05f)] public float attackDuration = 0.8f;
    [Min(0f)] public float extraRecoveryTime = 0f;
    [Range(0f, 1f)] public float hitNormalizedTime = 0.45f;

    [Header("Hit Check")]
    [Min(0.1f)] public float range = 2f;
    [Min(0.05f)] public float hitRadius = 0.5f;
    public bool requireTargetInFront = true;
    [Range(1f, 360f)] public float maxTargetAngle = 180f;

    [Header("RPG-style Auto Targeting")]
    public bool useHorizontalAutoTarget = true;
    public bool useColliderBoundsForAutoTarget = true;
    [Min(0f)] public float autoTargetExtraRange = 0.35f;

    [Header("Animation")]
    public AnimationClip attackClip;
    public string animatorStateName = "ATK0";
    public string returnStateName = "IdleA";
    public bool useDirectClipPlayback = true;
    public bool disableRootMotionDuringAttack = true;
    public bool lockMovementDuringAttack = true;
    public bool suppressMovementAnimationDuringAttack = true;
    public bool faceTargetOnAttack = true;
}
