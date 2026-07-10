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

    public static void ClearAll()
    {
        zones.Clear();
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
