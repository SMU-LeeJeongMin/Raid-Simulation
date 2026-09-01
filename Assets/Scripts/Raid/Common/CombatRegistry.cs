using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 개체의 중앙 등록소.
/// 매 프레임 또는 데미지 틱마다 씬 전체를 탐색하던 FindObjectsByType 호출을
/// 리스트 순회로 대체하기 위한 구조.
/// 각 컴포넌트가 Awake에서 등록하고 OnDestroy에서 해제.
/// </summary>
public static class CombatRegistry
{
    private static readonly List<PlayerStatus> playerStatuses = new List<PlayerStatus>();
    private static readonly List<DamageReceiver> damageReceivers = new List<DamageReceiver>();
    private static readonly List<Health> healths = new List<Health>();
    private static readonly List<BossDummyController> bossDummies = new List<BossDummyController>();
    private static readonly List<BossSkillPatternController> bossSkillControllers = new List<BossSkillPatternController>();
    private static readonly List<SlimeAddEnemy> slimes = new List<SlimeAddEnemy>();

    public static IReadOnlyList<PlayerStatus> PlayerStatuses => playerStatuses;
    public static IReadOnlyList<DamageReceiver> DamageReceivers => damageReceivers;
    public static IReadOnlyList<Health> Healths => healths;
    public static IReadOnlyList<BossDummyController> BossDummies => bossDummies;
    public static IReadOnlyList<BossSkillPatternController> BossSkillControllers => bossSkillControllers;
    public static IReadOnlyList<SlimeAddEnemy> Slimes => slimes;

    // 씬에 하나만 존재하는 보스 참조용 편의 접근자
    public static BossDummyController FirstBossDummy => bossDummies.Count > 0 ? bossDummies[0] : null;
    public static BossSkillPatternController FirstBossSkillController => bossSkillControllers.Count > 0 ? bossSkillControllers[0] : null;

    public static void Register(PlayerStatus status)
    {
        if (status != null && !playerStatuses.Contains(status))
            playerStatuses.Add(status);
    }

    public static void Unregister(PlayerStatus status)
    {
        playerStatuses.Remove(status);
    }

    public static void Register(DamageReceiver receiver)
    {
        if (receiver != null && !damageReceivers.Contains(receiver))
            damageReceivers.Add(receiver);
    }

    public static void Unregister(DamageReceiver receiver)
    {
        damageReceivers.Remove(receiver);
    }

    public static void Register(Health health)
    {
        if (health != null && !healths.Contains(health))
            healths.Add(health);
    }

    public static void Unregister(Health health)
    {
        healths.Remove(health);
    }

    public static void Register(BossDummyController boss)
    {
        if (boss != null && !bossDummies.Contains(boss))
            bossDummies.Add(boss);
    }

    public static void Unregister(BossDummyController boss)
    {
        bossDummies.Remove(boss);
    }

    public static void Register(BossSkillPatternController controller)
    {
        if (controller != null && !bossSkillControllers.Contains(controller))
            bossSkillControllers.Add(controller);
    }

    public static void Unregister(BossSkillPatternController controller)
    {
        bossSkillControllers.Remove(controller);
    }

    public static void Register(SlimeAddEnemy slime)
    {
        if (slime != null && !slimes.Contains(slime))
            slimes.Add(slime);
    }

    public static void Unregister(SlimeAddEnemy slime)
    {
        slimes.Remove(slime);
    }

    // 도메인 리로드 비활성화 환경에서 이전 플레이 세션의 잔존 등록 제거
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        playerStatuses.Clear();
        damageReceivers.Clear();
        healths.Clear();
        bossDummies.Clear();
        bossSkillControllers.Clear();
        slimes.Clear();
    }
}
