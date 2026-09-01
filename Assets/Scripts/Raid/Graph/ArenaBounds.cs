using UnityEngine;

/// <summary>
/// 좌표와 거리 정규화의 기준이 되는 아레나 범위 (Node 문서 5.5).
/// pos_x_norm = (x - center.x) / halfWidth, distance_ratio = 거리 / ReferenceDistance.
/// 씬에 배치하지 않으면 첫 스냅샷 시점의 보스 위치를 중심으로 한 기본값 사용.
/// </summary>
public class ArenaBounds : MonoBehaviour
{
    [Header("Bounds")]
    [Min(1f)] public float halfWidth = 25f;
    [Min(1f)] public float halfDepth = 25f;
    [Tooltip("distance_ratio의 기준 최대 거리. 0이면 halfWidth와 halfDepth 중 큰 값의 2배 사용")]
    [Min(0f)] public float referenceDistance = 0f;

    public static ArenaBounds Instance { get; private set; }

    // 씬에 컴포넌트가 없을 때의 폴백 기준 (첫 조회 시 보스 위치로 초기화)
    private static Vector3 fallbackCenter;
    private static bool fallbackCenterSet;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static Vector3 Center
    {
        get
        {
            if (Instance != null)
                return Instance.transform.position;

            if (!fallbackCenterSet)
            {
                BossDummyController boss = CombatRegistry.FirstBossDummy;
                fallbackCenter = boss != null ? boss.transform.position : Vector3.zero;
                fallbackCenterSet = true;
            }

            return fallbackCenter;
        }
    }

    public static float HalfWidth => Instance != null ? Instance.halfWidth : 25f;
    public static float HalfDepth => Instance != null ? Instance.halfDepth : 25f;

    public static float ReferenceDistance
    {
        get
        {
            if (Instance != null && Instance.referenceDistance > 0f)
                return Instance.referenceDistance;

            return Mathf.Max(HalfWidth, HalfDepth) * 2f;
        }
    }

    // 정규화 X 위치 (-1 ~ 1 근방, 아레나 밖이면 범위 초과 가능)
    public static float NormalizeX(float worldX)
    {
        return (worldX - Center.x) / Mathf.Max(1f, HalfWidth);
    }

    public static float NormalizeZ(float worldZ)
    {
        return (worldZ - Center.z) / Mathf.Max(1f, HalfDepth);
    }

    // 두 지점 사이 평면 거리의 정규화값 (0~1로 클램프)
    public static float DistanceRatio(Vector3 a, Vector3 b)
    {
        return Mathf.Clamp01(NPCSimpleFSMController.FlatDistance(a, b) / Mathf.Max(1f, ReferenceDistance));
    }

    // 장판 반경 등 길이의 정규화값
    public static float LengthRatio(float length)
    {
        return Mathf.Max(0f, length) / Mathf.Max(1f, ReferenceDistance);
    }

    // 재생 시작 시 폴백 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        fallbackCenterSet = false;
        fallbackCenter = Vector3.zero;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, new Vector3(halfWidth * 2f, 0.5f, halfDepth * 2f));
    }
}
