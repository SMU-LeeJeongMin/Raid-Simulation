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
        classStats = new[]
        {
            new BasicAttackStats
            {
                characterId = "warrior",
                displayName = "Warrior",
                damage = 85f,
                ultimateGainOnHit = 4f,
                attackDuration = 0.95f,
                extraRecoveryTime = 0f,
                hitNormalizedTime = 0.45f,
                range = 1.0f,
                hitRadius = 0.65f,
                requireTargetInFront = true,
                maxTargetAngle = 180f,
                useHorizontalAutoTarget = true,
                useColliderBoundsForAutoTarget = true,
                autoTargetExtraRange = 0.35f,
                animatorStateName = "ATK0",
                returnStateName = "IdleA",
                useDirectClipPlayback = true,
                disableRootMotionDuringAttack = true,
                lockMovementDuringAttack = true,
                suppressMovementAnimationDuringAttack = true,
                faceTargetOnAttack = true
            },
            new BasicAttackStats
            {
                characterId = "archer",
                displayName = "Archer",
                damage = 55f,
                ultimateGainOnHit = 3f,
                attackDuration = 0.60f,
                extraRecoveryTime = 0f,
                hitNormalizedTime = 0.45f,
                range = 6.0f,
                hitRadius = 0.45f,
                requireTargetInFront = true,
                maxTargetAngle = 180f,
                useHorizontalAutoTarget = true,
                useColliderBoundsForAutoTarget = true,
                autoTargetExtraRange = 0.35f,
                animatorStateName = "Shoot",
                returnStateName = "IdleA",
                useDirectClipPlayback = true,
                disableRootMotionDuringAttack = true,
                lockMovementDuringAttack = true,
                suppressMovementAnimationDuringAttack = true,
                faceTargetOnAttack = true
            },
            new BasicAttackStats
            {
                characterId = "mage",
                displayName = "Mage",
                damage = 70f,
                ultimateGainOnHit = 4f,
                attackDuration = 0.80f,
                extraRecoveryTime = 0f,
                hitNormalizedTime = 0.45f,
                range = 5.0f,
                hitRadius = 0.5f,
                requireTargetInFront = true,
                maxTargetAngle = 180f,
                useHorizontalAutoTarget = true,
                useColliderBoundsForAutoTarget = true,
                autoTargetExtraRange = 0.35f,
                animatorStateName = "ATK0",
                returnStateName = "IdleA",
                useDirectClipPlayback = true,
                disableRootMotionDuringAttack = true,
                lockMovementDuringAttack = true,
                suppressMovementAnimationDuringAttack = true,
                faceTargetOnAttack = true
            },
            new BasicAttackStats
            {
                characterId = "healer",
                displayName = "Healer",
                damage = 40f,
                ultimateGainOnHit = 4f,
                attackDuration = 1.05f,
                extraRecoveryTime = 0f,
                hitNormalizedTime = 0.45f,
                range = 4.5f,
                hitRadius = 0.45f,
                requireTargetInFront = true,
                maxTargetAngle = 180f,
                useHorizontalAutoTarget = true,
                useColliderBoundsForAutoTarget = true,
                autoTargetExtraRange = 0.35f,
                animatorStateName = "ATK0",
                returnStateName = "IdleA",
                useDirectClipPlayback = true,
                disableRootMotionDuringAttack = true,
                lockMovementDuringAttack = true,
                suppressMovementAnimationDuringAttack = true,
                faceTargetOnAttack = true
            }
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
