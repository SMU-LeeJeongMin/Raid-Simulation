using UnityEngine;

/// <summary>
/// 두 보스 컨트롤러에 중복 구현되어 있던 VFX 생성 보조 로직의 단일 구현.
/// </summary>
public static class VFXUtility
{
    // 프리팹 생성 + 스케일 적용 + 파티클 재시작 + 자동 파괴 예약
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, float destroyDelay)
    {
        if (prefab == null)
            return null;

        GameObject go = Object.Instantiate(prefab, position, rotation);
        go.transform.localScale = Vector3.Scale(go.transform.localScale, scale);
        RestartParticles(go);

        if (destroyDelay > 0f)
            Object.Destroy(go, destroyDelay);

        return go;
    }

    // 하위 파티클 시스템 전체를 정지 후 재생 (프리팹 저장 상태와 무관하게 처음부터 재생)
    public static void RestartParticles(GameObject root)
    {
        if (root == null)
            return;

        ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem ps = systems[i];
            if (ps == null)
                continue;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play(true);
        }
    }

    // 이름으로 하위 Transform 탐색 (VFX 부착 앵커 조회용)
    public static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == childName)
                return children[i];
        }

        return null;
    }
}
