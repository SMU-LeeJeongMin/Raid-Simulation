using UnityEngine;

/// <summary>
/// 3곳에 각각 구현되어 있던 레이어 재귀 변경의 단일 구현.
/// </summary>
public static class LayerUtility
{
    // 대상과 모든 하위 오브젝트의 레이어 변경
    public static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null || layer < 0)
            return;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null)
                children[i].gameObject.layer = layer;
        }
    }

    // 레이어 이름으로 변경 (이름이 유효하지 않으면 아무 것도 하지 않음)
    public static void SetLayerRecursively(GameObject root, string layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName))
            return;

        SetLayerRecursively(root, LayerMask.NameToLayer(layerName));
    }
}
