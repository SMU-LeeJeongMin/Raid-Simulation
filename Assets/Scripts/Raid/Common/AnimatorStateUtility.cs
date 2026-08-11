using UnityEngine;

/// <summary>
/// Animator 상태 이름을 "Base Layer." 접두어 폴백과 함께 해석하는 공용 유틸리티.
/// 여러 컨트롤러에 동일하게 복제되어 있던 ResolveAnimatorStateName의 단일 구현.
/// </summary>
public static class AnimatorStateUtility
{
    public const string BaseLayerPrefix = "Base Layer.";

    // 상태가 존재하면 재생 가능한 이름을, 없으면 빈 문자열을 반환
    public static string ResolveStateName(Animator animator, string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return string.Empty;

        if (animator.HasState(0, Animator.StringToHash(stateName)))
            return stateName;

        string baseLayerPath = BaseLayerPrefix + stateName;
        if (animator.HasState(0, Animator.StringToHash(baseLayerPath)))
            return baseLayerPath;

        return string.Empty;
    }
}
