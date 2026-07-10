// Boss UI
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossWorldHealthUI : MonoBehaviour
{
    [Header("Target")]
    public Health bossHealth;
    public Transform followTarget;
    public Transform bossHpAnchor;
    public Transform rendererBoundsRoot;
    public Vector3 worldOffset = new Vector3(0f, 0.3f, 0f);

    [Header("References")]
    public Camera worldCamera;
    public RectTransform panelRoot;
    public Image hpFillImage;
    public TMP_Text percentText;
    public TMP_Text bossNameText;

    [Header("Colors")]
    public Color bossHpColor = new Color(1f, 0.1f, 0.1f, 1f);

    [Header("Fill Sprite Fix")]
    public bool useGeneratedSolidFillSprite = false;

    [Header("Positioning")]
    public bool useRendererBoundsWhenPossible = false;
    public float rendererBoundsTopExtraHeight = 0.35f;
    public bool forceOverlayScreenPosition = true;

    [Header("Visibility")]
    public bool hideWhenNoBoss = true;
    public bool hideWhenBossDead = true;
    public bool hideWhenOffScreen = true;
    [Range(0f, 0.25f)] public float viewportMargin = 0.02f;
    public bool clampToScreen = false;

    public Vector2 screenPadding = new Vector2(40f, 40f);

    [Header("Optional Occlusion")]
    public bool hideWhenOccluded = false;
    public LayerMask occlusionMask = ~0;
    public float occlusionSphereRadius = 0.05f;

    private Canvas parentCanvas;
    private RectTransform canvasRect;
    private CanvasGroup canvasGroup;
    private static Sprite solidWhiteSprite;

    private void Awake()
    {
        CacheReferences();
        ConfigureFillImage();
        RefreshBar();
    }

    private void Reset()
    {
        panelRoot = GetComponent<RectTransform>();
        hpFillImage = GetComponentInChildren<Image>(true);
    }

    private void LateUpdate()
    {
        CacheReferences();
        RefreshBar();
        UpdatePositionAndVisibility();
    }

    public void Bind(BossDummyController boss, Transform anchor = null, string displayName = null)
    {
        if (boss == null)
        {
            Bind((Health)null, null, null, displayName);
            return;
        }

        Health health = boss.health != null ? boss.health : boss.GetComponent<Health>();
        Bind(health, boss.transform, anchor, string.IsNullOrEmpty(displayName) ? boss.bossName : displayName);
    }

    public void Bind(BossDummyController boss, Transform follow, Transform anchor, string displayName = null)
    {
        if (boss == null)
        {
            Bind((Health)null, follow, anchor, displayName);
            return;
        }

        Health health = boss.health != null ? boss.health : boss.GetComponent<Health>();
        Bind(health, follow != null ? follow : boss.transform, anchor, string.IsNullOrEmpty(displayName) ? boss.bossName : displayName);
    }

    public void Bind(Health health, Transform follow, string displayName = null)
    {
        Bind(health, follow, null, displayName);
    }

    public void Bind(Health health, Transform follow, Transform anchor, string displayName = null)
    {
        bossHealth = health;
        followTarget = follow;
        bossHpAnchor = anchor;

        if (rendererBoundsRoot == null && followTarget != null)
            rendererBoundsRoot = followTarget;

        if (bossNameText != null)
            bossNameText.text = displayName ?? string.Empty;

        RefreshBar();
        UpdatePositionAndVisibility();
    }

    private void CacheReferences()
    {
        if (panelRoot == null)
            panelRoot = GetComponent<RectTransform>();

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (parentCanvas == null && panelRoot != null)
            parentCanvas = panelRoot.GetComponentInParent<Canvas>();

        if (parentCanvas != null && canvasRect == null)
            canvasRect = parentCanvas.transform as RectTransform;

        if (canvasGroup == null && panelRoot != null)
        {
            canvasGroup = panelRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = panelRoot.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void ConfigureFillImage()
    {
        if (hpFillImage == null)
            return;

        if (useGeneratedSolidFillSprite)
            hpFillImage.sprite = GetSolidWhiteSprite();

        hpFillImage.type = Image.Type.Filled;
        hpFillImage.fillMethod = Image.FillMethod.Horizontal;
        hpFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        hpFillImage.color = bossHpColor;
    }

    private void RefreshBar()
    {
        ConfigureFillImage();

        float normalized = 0f;
        if (bossHealth != null && bossHealth.MaxHealth > 0f)
            normalized = Mathf.Clamp01(bossHealth.CurrentHealth / bossHealth.MaxHealth);

        if (hpFillImage != null)
            hpFillImage.fillAmount = normalized;

        if (percentText != null)
            percentText.text = $"{normalized * 100f:0.0}%";
    }

    private void UpdatePositionAndVisibility()
    {
        if (panelRoot == null)
            return;

        if (bossHealth == null || followTarget == null)
        {
            SetVisible(!hideWhenNoBoss);
            return;
        }

        if (hideWhenBossDead && bossHealth.CurrentHealth <= 0f)
        {
            SetVisible(false);
            return;
        }

        if (worldCamera == null)
        {
            SetVisible(false);
            return;
        }

        Vector3 worldPosition = GetDisplayWorldPosition();
        Vector3 viewportPosition = worldCamera.WorldToViewportPoint(worldPosition);

        bool inFront = viewportPosition.z > 0.01f;
        bool insideViewport = viewportPosition.x >= -viewportMargin && viewportPosition.x <= 1f + viewportMargin &&
                              viewportPosition.y >= -viewportMargin && viewportPosition.y <= 1f + viewportMargin;

        if (hideWhenOffScreen && (!inFront || !insideViewport))
        {
            SetVisible(false);
        }

        if (hideWhenOccluded && IsOccluded(worldPosition))
        {
            SetVisible(false);
        }

        Vector3 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);
        if (clampToScreen)
        {
            screenPosition.x = Mathf.Clamp(screenPosition.x, screenPadding.x, Screen.width - screenPadding.x);
            screenPosition.y = Mathf.Clamp(screenPosition.y, screenPadding.y, Screen.height - screenPadding.y);
        }

        ApplyScreenPosition(screenPosition);
        SetVisible(true);
    }

    private Vector3 GetDisplayWorldPosition()
    {
        if (bossHpAnchor != null)
            return bossHpAnchor.position + worldOffset;

        if (useRendererBoundsWhenPossible && TryGetRendererBounds(out Bounds bounds))
            return new Vector3(bounds.center.x, bounds.max.y + rendererBoundsTopExtraHeight, bounds.center.z) + worldOffset;

        return followTarget.position + worldOffset;
    }

    private bool TryGetRendererBounds(out Bounds combinedBounds)
    {
        combinedBounds = default;
        Transform root = rendererBoundsRoot != null ? rendererBoundsRoot : followTarget;
        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private void ApplyScreenPosition(Vector3 screenPosition)
    {
        if (panelRoot == null)
            return;

        if (forceOverlayScreenPosition || parentCanvas == null || parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            panelRoot.position = screenPosition;
            return;
        }

        Camera uiCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceCamera ? parentCanvas.worldCamera : null;
        if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera, out Vector2 localPoint))
            panelRoot.anchoredPosition = localPoint;
        else
            panelRoot.position = screenPosition;
    }

    private bool IsOccluded(Vector3 worldPosition)
    {
        if (worldCamera == null)
            return false;

        Vector3 origin = worldCamera.transform.position;
        Vector3 direction = worldPosition - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
            return false;

        direction /= distance;

        bool hitSomething = Physics.SphereCast(
            origin,
            Mathf.Max(0.001f, occlusionSphereRadius),
            direction,
            out RaycastHit hit,
            distance,
            occlusionMask,
            QueryTriggerInteraction.Ignore
        );

        if (!hitSomething)
            return false;

        if (followTarget != null && hit.transform.IsChildOf(followTarget))
            return false;

        return true;
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        else if (panelRoot != null)
        {
            panelRoot.gameObject.SetActive(visible);
        }
    }

    private string GetPositionSourceName()
    {
        if (bossHpAnchor != null)
            return bossHpAnchor.name;
        if (useRendererBoundsWhenPossible && rendererBoundsRoot != null)
            return rendererBoundsRoot.name + " RendererBounds";
        return followTarget != null ? followTarget.name : "None";
    }

    private static Sprite GetSolidWhiteSprite()
    {
        if (solidWhiteSprite != null)
            return solidWhiteSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "Generated_UI_SolidWhite";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;

        solidWhiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        solidWhiteSprite.name = "Generated_UI_SolidWhiteSprite";
        solidWhiteSprite.hideFlags = HideFlags.HideAndDontSave;
        return solidWhiteSprite;
    }
}
