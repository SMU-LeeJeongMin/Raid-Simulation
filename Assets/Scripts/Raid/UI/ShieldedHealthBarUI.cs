using UnityEngine;
using UnityEngine.UI;

// HP 바 위 실드 표시 방식 (기존 Player/Party 개별 enum의 통합)
public enum ShieldUIDisplayMode
{
    OverlayCurrentShield,
    ExtraBehindHealth,
    AdjacentToCurrentHealth
}

// 실드 표시 설정 묶음 (각 UI 컴포넌트의 직렬화 필드에서 호출 시 구성)
public struct ShieldBarSettings
{
    public ShieldUIDisplayMode displayMode;
    public bool fillFromRight;
    public bool hideWhenEmpty;
    public float customDisplayMax;
    public bool showAtRightWhenHealthFull;
    public bool useGeneratedSprite;
    public Color shieldColor;
}

/// <summary>
/// PlayerStatusUI와 PartyMemberStatusUI에 중복되어 있던 HP/실드 바 렌더링 로직의 단일 구현.
/// Prepare 계열은 바인딩 시 1회만 호출하고, Refresh 계열은 값이 변한 경우에만 UI에 기록하여
/// 매 프레임 sprite/type/RectTransform 재설정으로 인한 캔버스 dirty를 방지.
/// </summary>
public static class ShieldedHealthBarUI
{
    private static Sprite solidFillSprite;

    // 프로젝트 공용 1x1 흰색 스프라이트 (4곳에 각각 생성되던 것의 통합)
    public static Sprite SolidFillSprite
    {
        get
        {
            if (solidFillSprite != null)
                return solidFillSprite;

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "Generated_UI_SolidFill";
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);

            solidFillSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            solidFillSprite.name = "Generated_UI_SolidFillSprite";
            solidFillSprite.hideFlags = HideFlags.HideAndDontSave;
            return solidFillSprite;
        }
    }

    // 가로 채움 이미지 설정 (바인딩 또는 활성화 시 1회 호출)
    public static void PrepareFillImage(Image image, bool fillFromRight, bool useGeneratedSprite)
    {
        if (image == null)
            return;

        if (useGeneratedSprite)
            image.sprite = SolidFillSprite;

        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = fillFromRight ? 1 : 0;
        image.preserveAspect = false;
    }

    // 실드 이미지 설정 (표시 방식에 따라 채움형 또는 단순형으로 구성)
    public static void PrepareShieldImage(Image shieldFillImage, in ShieldBarSettings settings)
    {
        if (shieldFillImage == null)
            return;

        if (settings.displayMode == ShieldUIDisplayMode.AdjacentToCurrentHealth)
            PrepareAdjacentShieldImage(shieldFillImage, settings);
        else
            PrepareFillImage(shieldFillImage, settings.fillFromRight, settings.useGeneratedSprite);
    }

    private static void PrepareAdjacentShieldImage(Image image, in ShieldBarSettings settings)
    {
        if (settings.useGeneratedSprite)
            image.sprite = SolidFillSprite;

        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = settings.shieldColor;
    }

    // fillAmount만 기록, 값이 같으면 건너뜀
    public static void SetFill(Image image, float amount)
    {
        if (image == null)
            return;

        amount = Mathf.Clamp01(amount);
        if (!Mathf.Approximately(image.fillAmount, amount))
            image.fillAmount = amount;
    }

    // 실드 표시 갱신 (hasHealth가 false면 hpCurrent/hpMax는 무시)
    public static void RefreshShield(Image shieldFillImage, float shieldAmount, float hpCurrent, float hpMax, bool hasHealth, in ShieldBarSettings settings)
    {
        if (shieldFillImage == null)
            return;

        if (shieldAmount <= 0f)
        {
            ClearShieldFill(shieldFillImage, settings);
            if (settings.hideWhenEmpty)
                SetActiveIfChanged(shieldFillImage.gameObject, false);
            return;
        }

        if (settings.hideWhenEmpty)
            SetActiveIfChanged(shieldFillImage.gameObject, true);

        float displayMax = settings.customDisplayMax > 0f
            ? settings.customDisplayMax
            : hasHealth ? Mathf.Max(1f, hpMax) : 100f;

        if (settings.displayMode == ShieldUIDisplayMode.AdjacentToCurrentHealth)
        {
            RefreshAdjacentShield(shieldFillImage, shieldAmount, displayMax, hpCurrent, hpMax, hasHealth, settings);
            return;
        }

        ResetShieldRectToFullWidth(shieldFillImage);

        float fill;
        if (settings.displayMode == ShieldUIDisplayMode.ExtraBehindHealth && hasHealth)
        {
            float combined = Mathf.Clamp(hpCurrent + shieldAmount, 0f, displayMax);
            fill = combined / displayMax;
        }
        else
        {
            fill = Mathf.Clamp01(shieldAmount / displayMax);
        }

        SetFill(shieldFillImage, fill);
    }

    // HP 끝 지점부터 실드 폭만큼 이어붙이는 표시 방식
    private static void RefreshAdjacentShield(Image shieldFillImage, float shieldAmount, float displayMax, float hpCurrent, float hpMax, bool hasHealth, in ShieldBarSettings settings)
    {
        float hpRatio = 0f;
        if (hasHealth)
            hpRatio = Mathf.Clamp01(hpCurrent / Mathf.Max(1f, hpMax));

        float shieldRatio = Mathf.Clamp01(shieldAmount / Mathf.Max(1f, displayMax));
        float start = hpRatio;
        float end = Mathf.Clamp01(hpRatio + shieldRatio);

        if (end <= start + 0.001f && settings.showAtRightWhenHealthFull)
        {
            end = 1f;
            start = Mathf.Clamp01(1f - shieldRatio);
        }

        if (end <= start + 0.001f)
        {
            ClearShieldFill(shieldFillImage, settings);
            return;
        }

        SetAnchoredRect(shieldFillImage, new Vector2(start, 0f), new Vector2(end, 1f), new Vector2(0f, 0.5f));

        if (!Mathf.Approximately(shieldFillImage.fillAmount, 1f))
            shieldFillImage.fillAmount = 1f;
    }

    private static void ResetShieldRectToFullWidth(Image shieldFillImage)
    {
        SetAnchoredRect(shieldFillImage, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
    }

    private static void ClearShieldFill(Image shieldFillImage, in ShieldBarSettings settings)
    {
        if (settings.displayMode == ShieldUIDisplayMode.AdjacentToCurrentHealth)
        {
            SetAnchoredRect(shieldFillImage, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));

            if (!Mathf.Approximately(shieldFillImage.fillAmount, 1f))
                shieldFillImage.fillAmount = 1f;
        }
        else
        {
            SetFill(shieldFillImage, 0f);
        }
    }

    // 변경이 없으면 RectTransform 기록 생략 (매 프레임 레이아웃 dirty 방지)
    private static void SetAnchoredRect(Image image, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        RectTransform rect = image.rectTransform;
        if (rect.anchorMin == anchorMin && rect.anchorMax == anchorMax && rect.pivot == pivot &&
            rect.offsetMin == Vector2.zero && rect.offsetMax == Vector2.zero)
            return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = pivot;
    }

    public static void SetActiveIfChanged(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }
}
