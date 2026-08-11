using System.Collections.Generic;

/// <summary>
/// 파티원 대상 유효성 판정의 단일 구현.
/// 보스 컨트롤러, 투사체, 슬라임, 장판에 각각 복제되어 있던
/// IsValidPlayerTarget 계열 검사의 통합 버전.
/// </summary>
public static class PartyTargetUtility
{
    // 살아있는 파티원(보스 더미 제외) 판정
    public static bool IsValidPlayerTarget(PlayerStatus player)
    {
        if (player == null)
            return false;

        // 보스 더미에 붙은 PlayerStatus는 파티 대상에서 제외
        if (player.IsBossDummy)
            return false;

        return player.Health != null && !player.Health.IsDead;
    }

    // 유효한 파티원을 buffer에 수집 (buffer는 호출 측에서 재사용하여 할당 최소화)
    public static int CollectValidPlayerTargets(List<PlayerStatus> buffer)
    {
        buffer.Clear();

        IReadOnlyList<PlayerStatus> all = CombatRegistry.PlayerStatuses;
        for (int i = 0; i < all.Count; i++)
        {
            if (IsValidPlayerTarget(all[i]))
                buffer.Add(all[i]);
        }

        return buffer.Count;
    }
}
