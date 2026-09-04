using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 씬에 활성화된 위험 범위를 전역으로 관리합니다.
/// NPC 회피, Healer 위험 판단, GNN/GAT용 그래프 상태 생성에서 사용합니다.
/// </summary>
public static class DangerZoneRegistry
{
    private static readonly List<DangerZoneHandle> zones = new List<DangerZoneHandle>();
    private static int nextId = 1;

    public static int Count => zones.Count;

    public static int Register(DangerZoneHandle zone)
    {
        if (zone == null)
            return -1;

        CleanupNulls();

        if (!zones.Contains(zone))
            zones.Add(zone);

        if (zone.zoneId <= 0)
            zone.zoneId = nextId++;

        return zone.zoneId;
    }

    public static void Unregister(DangerZoneHandle zone)
    {
        if (zone == null)
            return;

        zones.Remove(zone);
    }

    public static IReadOnlyList<DangerZoneHandle> GetActiveZones()
    {
        CleanupNulls();
        return zones;
    }

    public static bool IsPointInAnyZone(Vector3 point, float padding = 0f)
    {
        return FindFirstZoneContaining(point, padding) != null;
    }

    public static DangerZoneHandle FindFirstZoneContaining(Vector3 point, float padding = 0f)
    {
        CleanupNulls();

        for (int i = 0; i < zones.Count; i++)
        {
            DangerZoneHandle zone = zones[i];
            if (zone == null || !zone.isActiveAndEnabled)
                continue;

            if (zone.ContainsPoint(point, padding))
                return zone;
        }

        return null;
    }

    public static int CountZonesContaining(Vector3 point, float padding = 0f)
    {
        CleanupNulls();

        int count = 0;
        for (int i = 0; i < zones.Count; i++)
        {
            DangerZoneHandle zone = zones[i];
            if (zone == null || !zone.isActiveAndEnabled)
                continue;

            if (zone.ContainsPoint(point, padding))
                count++;
        }
        return count;
    }

    public static List<DangerZoneSnapshot> GetSnapshots()
    {
        CleanupNulls();

        List<DangerZoneSnapshot> snapshots = new List<DangerZoneSnapshot>(zones.Count);
        for (int i = 0; i < zones.Count; i++)
        {
            DangerZoneHandle zone = zones[i];
            if (zone == null || !zone.isActiveAndEnabled)
                continue;

            snapshots.Add(zone.ToSnapshot());
        }
        return snapshots;
    }

    public static DangerZoneHandle FindNearestZone(Vector3 point, out float sqrDistance)
    {
        CleanupNulls();

        sqrDistance = float.PositiveInfinity;
        DangerZoneHandle best = null;
        for (int i = 0; i < zones.Count; i++)
        {
            DangerZoneHandle zone = zones[i];
            if (zone == null || !zone.isActiveAndEnabled)
                continue;

            Vector3 delta = zone.transform.position - point;
            delta.y = 0f;
            float sqr = delta.sqrMagnitude;
            if (sqr < sqrDistance)
            {
                sqrDistance = sqr;
                best = zone;
            }
        }
        return best;
    }

    // 지점이 "차폐 덕분에만" 안전한지 (도형상 위험 범위 안이지만 벽 그림자로 보호).
    // 그림자 밖으로 나가면 다시 위험해지므로, NPC가 이런 위치에서는 대피 상태를 유지해야 함
    public static bool IsPointShadowProtected(Vector3 point, float padding = 0f)
    {
        CleanupNulls();

        for (int i = 0; i < zones.Count; i++)
        {
            DangerZoneHandle zone = zones[i];
            if (zone == null || !zone.isActiveAndEnabled || zone.losOrigin == null)
                continue;

            if (zone.ContainsPointIgnoringShadow(point, padding) && !zone.ContainsPoint(point, padding))
                return true;
        }

        return false;
    }

    // ---------- 안전 지점 힌트 ----------
    // 즉사기 벽 그림자처럼 좁은 안전 지대는 원형 표본 추출로 놓칠 수 있으므로,
    // 기믹이 안전 지점을 직접 등록하고 NPC의 안전 위치 탐색이 이를 우선 검토.
    // 실행 계층의 공용 확장이므로 모든 정책에 동일하게 적용 (비교 공정성 유지)
    private static readonly List<Vector3> safeHints = new List<Vector3>();

    public static IReadOnlyList<Vector3> SafeHints => safeHints;

    public static void AddSafeHint(Vector3 point)
    {
        safeHints.Add(point);
    }

    public static void ClearSafeHints()
    {
        safeHints.Clear();
    }

    public static void ClearAll()
    {
        zones.Clear();
        safeHints.Clear();
    }

    // 씬 재로드(에피소드 반복) 시 정적 상태 초기화
    // (핸들은 OnDisable로 스스로 해제되지만 힌트는 소유자가 없어 명시적 초기화 필요)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        zones.Clear();
        safeHints.Clear();
        nextId = 1;
    }

    private static void CleanupNulls()
    {
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            if (zones[i] == null)
                zones.RemoveAt(i);
        }
    }
}
