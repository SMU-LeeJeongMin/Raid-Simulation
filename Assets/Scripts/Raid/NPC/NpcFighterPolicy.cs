using UnityEngine;

/// <summary>
/// 탱커/원거리 딜러의 전투 의사결정 정책.
/// 기존 NPCSimpleFSMController.ThinkFighter의 분리 구현.
/// </summary>
public class NpcFighterPolicy : INpcRolePolicy
{
    private readonly bool tank;

    public NpcFighterPolicy(bool tank)
    {
        this.tank = tank;
    }

    public string Think(NPCSimpleFSMController npc)
    {
        float desiredDistance = tank ? npc.meleeDistance : npc.rangedDistance;
        return ThinkFighter(npc, desiredDistance, tank);
    }

    // 힐러 정책도 보스 공격 시 재사용하는 공용 전투 판단
    public static string ThinkFighter(NPCSimpleFSMController npc, float desiredDistance, bool tank)
    {
        if (npc.boss == null)
        {
            npc.FollowPlayerFormation();
            npc.SetStateLabel("No Boss: Follow Player");
            return NpcActionNames.FollowPlayer;
        }

        if (npc.IsBossFlyingAndUntargetable())
        {
            npc.FollowPlayerFormation();
            npc.SetStateLabel(tank ? "Tank Wait: Boss Flying" : "DPS Wait: Boss Flying");
            return NpcActionNames.RegroupDuringBossFly;
        }

        Vector3 bossPosition = npc.boss.transform.position;
        float distance = NPCSimpleFSMController.FlatDistance(npc.transform.position, bossPosition);

        if (distance > desiredDistance)
        {
            npc.MoveToCombatRange(bossPosition, desiredDistance);
            npc.SetStateLabel(tank ? "Tank MoveToBoss" : "DPS MoveToBoss");
            return NpcActionNames.MoveToBoss;
        }

        if (!tank && distance < npc.keepDistanceFromBoss)
        {
            npc.MoveAwayFrom(bossPosition, npc.keepDistanceFromBoss);
            npc.SetStateLabel("DPS KeepDistance");
            return NpcActionNames.KeepDistance;
        }

        npc.FacePosition(bossPosition);
        npc.SetStateLabel(tank ? "Tank Attack" : "DPS Attack");

        if (!npc.IsActionReady)
            return NpcActionNames.AttackBoss;

        npc.ConsumeActionCooldown();

        bool usedSkill = false;
        if (npc.IsSkillTickReady && npc.skillController != null)
        {
            npc.ConsumeSkillCooldown();

            if (npc.useUltimateWhenReady)
                usedSkill = npc.skillController.TryUseUltimate();

            if (!usedSkill)
                usedSkill = npc.skillController.TryUseSkill1();

            if (!usedSkill)
                usedSkill = npc.skillController.TryUseSkill2();
        }

        if (!usedSkill && npc.basicAttack != null)
            npc.basicAttack.TryBasicAttack();

        return NpcActionNames.AttackBoss;
    }
}
