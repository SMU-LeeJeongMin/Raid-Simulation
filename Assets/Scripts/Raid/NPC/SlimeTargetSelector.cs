using UnityEngine;

/// <summary>
/// 슬라임 대상 선택의 단일 구현.
/// 규칙: 폭발까지 남은 시간이 가장 짧은 생존 슬라임 우선, 동률이면 가까운 쪽.
/// 이후 GAT가 대상을 선택하게 되면 이 선택기만 교체하는 구조.
/// </summary>
public static class SlimeTargetSelector
{
    // 가장 시급한 생존 슬라임 반환 (없으면 null)
    public static SlimeAddEnemy FindMostUrgent(Vector3 fromPosition)
    {
        var slimes = CombatRegistry.Slimes;
        SlimeAddEnemy best = null;
        float bestTime = float.PositiveInfinity;
        float bestSqrDistance = float.PositiveInfinity;

        for (int i = 0; i < slimes.Count; i++)
        {
            SlimeAddEnemy slime = slimes[i];
            if (slime == null || !slime.IsAlive)
                continue;

            float remaining = slime.ExplosionTimeRemaining;
            float sqrDistance = (slime.transform.position - fromPosition).sqrMagnitude;

            bool moreUrgent = remaining < bestTime - 0.05f;
            bool tieButCloser = Mathf.Abs(remaining - bestTime) <= 0.05f && sqrDistance < bestSqrDistance;

            if (moreUrgent || tieButCloser)
            {
                best = slime;
                bestTime = remaining;
                bestSqrDistance = sqrDistance;
            }
        }

        return best;
    }

    // 생존 슬라임 존재 여부 (행동 Mask용 경량 조회)
    public static bool AnyAlive()
    {
        var slimes = CombatRegistry.Slimes;
        for (int i = 0; i < slimes.Count; i++)
        {
            if (slimes[i] != null && slimes[i].IsAlive)
                return true;
        }

        return false;
    }
}
