using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RL 조율자(COORD_RL_V1)의 관측 벡터 빌더.
/// 필드 순서는 coordinator_manifest.json의 obs_fields와 완전히 일치해야 함
/// (학습 노트북 step10의 build_obs가 스냅샷에서 계산하는 것과 같은 의미의 값을
/// 실행 시점의 게임 상태에서 직접 계산).
/// </summary>
public static class CoordinatorObservation
{
    public const int Dim = 23;

    // 학습 노트북과 동일한 정규화 상한
    private const float SlimeNormMax = 4f;
    private const float ZoneNormMax = 5f;

    public static void Fill(float[] buffer, NPCSimpleFSMController npc,
        IReadOnlyList<NPCSimpleFSMController> partyNpcs, float episodeTimeLimit)
    {
        for (int i = 0; i < Dim; i++)
            buffer[i] = 0f;

        // ---------- 전역 상태 ----------
        // 파티 HP 통계: 스크립트 플레이어를 포함한 전체 캐릭터 기준 (스냅샷의 캐릭터 노드와 동일)
        IReadOnlyList<PlayerStatus> statuses = CombatRegistry.PlayerStatuses;
        int total = 0, alive = 0;
        float hpSum = 0f, hpMin = 1f;

        for (int i = 0; i < statuses.Count; i++)
        {
            PlayerStatus status = statuses[i];
            if (status == null || status.Health == null)
                continue;

            total++;
            if (status.Health.IsDead || status.Health.MaxHealth <= 0f)
                continue;

            alive++;
            float ratio = Mathf.Clamp01(status.Health.CurrentHealth / status.Health.MaxHealth);
            hpSum += ratio;
            hpMin = Mathf.Min(hpMin, ratio);
        }

        Health bossHealth = null;
        BossDummyController boss = npc != null ? npc.boss : null;
        if (boss != null)
            bossHealth = boss.health != null ? boss.health : boss.GetComponent<Health>();

        int slimesAlive = 0;
        IReadOnlyList<SlimeAddEnemy> slimes = CombatRegistry.Slimes;
        for (int i = 0; i < slimes.Count; i++)
            if (slimes[i] != null && slimes[i].IsAlive)
                slimesAlive++;

        bool healerNpc = false;
        for (int i = 0; i < partyNpcs.Count; i++)
            if (partyNpcs[i] != null && partyNpcs[i].IsHealerRole)
                healerNpc = true;

        BossSkillPatternController pattern = npc != null ? npc.bossSkillPattern : null;
        if (pattern == null && boss != null)
            pattern = boss.GetComponent<BossSkillPatternController>();

        buffer[0] = bossHealth != null && bossHealth.MaxHealth > 0f
            ? Mathf.Clamp01(bossHealth.CurrentHealth / bossHealth.MaxHealth) : 1f;
        buffer[1] = episodeTimeLimit > 0f ? Mathf.Clamp01(Time.timeSinceLevelLoad / episodeTimeLimit) : 0f;
        buffer[2] = alive > 0 ? hpSum / alive : 0f;
        buffer[3] = alive > 0 ? hpMin : 0f;
        buffer[4] = total > 0 ? (float)alive / total : 0f;
        buffer[5] = Mathf.Clamp01(slimesAlive / SlimeNormMax);
        buffer[6] = bossHealth != null && bossHealth.DamageImmune ? 1f : 0f;
        buffer[7] = pattern != null && pattern.IsFlying ? 1f : 0f;
        buffer[8] = healerNpc ? 1f : 0f;
        buffer[9] = Mathf.Clamp01(DangerZoneRegistry.GetActiveZones().Count / ZoneNormMax);

        // ---------- 해당 NPC 상태 ----------
        PlayerStatus self = npc != null ? npc.status : null;
        if (self != null && self.Health != null && self.Health.MaxHealth > 0f)
        {
            buffer[10] = Mathf.Clamp01(self.Health.CurrentHealth / self.Health.MaxHealth);
            if (self.Shield != null)
                buffer[13] = Mathf.Max(0f, self.Shield.CurrentShield / self.Health.MaxHealth);
        }
        if (self != null && self.Mana != null)
            buffer[11] = self.Mana.Normalized;

        PlayerSkillController skills = self != null ? self.GetComponent<PlayerSkillController>() : null;
        if (skills != null)
        {
            buffer[12] = Mathf.Clamp01(skills.GetUltimateGaugeNormalized());
            buffer[17] = skills.IsSkillInProgress ? 1f : 0f;
        }

        buffer[14] = npc != null && npc.IsHealerRole ? 1f : 0f;
        buffer[15] = npc != null && npc.IsTankRole ? 1f : 0f;

        if (npc != null && boss != null)
        {
            float dx = ArenaBounds.NormalizeX(npc.transform.position.x) - ArenaBounds.NormalizeX(boss.transform.position.x);
            float dz = ArenaBounds.NormalizeZ(npc.transform.position.z) - ArenaBounds.NormalizeZ(boss.transform.position.z);
            buffer[16] = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dz * dz));
        }

        // ---------- 직전 지시 one-hot ----------
        int prevCmd = Mathf.Clamp(CommandBoard.CommandId(npc), 0, 4);
        buffer[18 + prevCmd] = 1f;
    }
}
