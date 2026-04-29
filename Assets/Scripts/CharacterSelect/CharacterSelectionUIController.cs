using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectionUIController : MonoBehaviour
{
    [Header("Buttons")]
    public Button backButton;
    public Button selectButton;

    [Header("Groups")]
    public CanvasGroup sideShadowGroup;
    public CanvasGroup rightInfoGroup;

    [Header("Side Shadow Auto Layout")]
    public bool autoFitSideShadows = true;
    public float sideShadowWidth = 480f;
    public RectTransform sideShadowRoot;
    public RectTransform leftShadow;
    public RectTransform rightShadow;

    [Header("Job Name Header")]
    // RawImage 또는 Image 모두 연결할 수 있습니다.
    // 직업 이름 자체는 이미지가 아니라 아래 jobNameText에 표시됩니다.
    public Graphic jobNameHeaderBackground;
    public TMP_Text jobNameText;

    [Header("Info Text")]
    public TMP_Text roleText;
    public TMP_Text descriptionText;

    private CharacterLineupSelectionController owner;

    public void Initialize(CharacterLineupSelectionController controller)
    {
        owner = controller;

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(OnBackClicked);
            backButton.onClick.AddListener(OnBackClicked);
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(OnSelectClicked);
            selectButton.onClick.AddListener(OnSelectClicked);
        }

        CacheShadowRectsIfMissing();
        ApplySideShadowLayout();
        HideSelectedState();
    }

    public void ShowSelectedState(CharacterLineupEntry entry)
    {
        if (entry == null)
            return;

        ApplySideShadowLayout();
        SetSideShadowVisible(true);
        SetGroupVisible(rightInfoGroup, true, true);

        if (backButton != null)
            backButton.gameObject.SetActive(true);

        if (selectButton != null)
        {
            selectButton.gameObject.SetActive(true);
            selectButton.interactable = true;
        }

        if (jobNameHeaderBackground != null)
            jobNameHeaderBackground.gameObject.SetActive(true);

        SetText(jobNameText, entry.jobName);
        SetText(roleText, entry.roleName);
        SetText(descriptionText, entry.description);
    }

    public void HideSelectedState()
    {
        SetSideShadowVisible(false);
        SetGroupVisible(rightInfoGroup, false, true);

        if (backButton != null)
            backButton.gameObject.SetActive(false);

        if (selectButton != null)
        {
            selectButton.interactable = false;
            selectButton.gameObject.SetActive(false);
        }
    }

    private void SetSideShadowVisible(bool visible)
    {
        SetGroupVisible(sideShadowGroup, visible, false);
    }

    private void SetGroupVisible(CanvasGroup group, bool visible, bool blockRaycasts)
    {
        if (group == null)
            return;

        group.alpha = visible ? 1f : 0f;
        group.interactable = visible && blockRaycasts;
        group.blocksRaycasts = visible && blockRaycasts;
    }

    private void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value ?? string.Empty;
    }

    private void OnBackClicked()
    {
        if (owner != null)
            owner.GoBackToLineup();
    }

    private void OnSelectClicked()
    {
        if (owner != null)
            owner.ConfirmSelectionAndLoadRaid();
    }

    private void CacheShadowRectsIfMissing()
    {
        if (sideShadowRoot == null && sideShadowGroup != null)
            sideShadowRoot = sideShadowGroup.GetComponent<RectTransform>();

        if (sideShadowRoot == null)
            return;

        if (leftShadow == null)
            leftShadow = FindChildRect(sideShadowRoot, "left");

        if (rightShadow == null)
            rightShadow = FindChildRect(sideShadowRoot, "right");
    }

    private RectTransform FindChildRect(Transform root, string keyword)
    {
        if (root == null || string.IsNullOrEmpty(keyword))
            return null;

        string lowerKeyword = keyword.ToLowerInvariant();
        RectTransform[] children = root.GetComponentsInChildren<RectTransform>(true);
        foreach (RectTransform child in children)
        {
            if (child == root)
                continue;

            if (child.name.ToLowerInvariant().Contains(lowerKeyword))
                return child;
        }

        return null;
    }

    private void ApplySideShadowLayout()
    {
        if (!autoFitSideShadows)
            return;

        CacheShadowRectsIfMissing();

        if (sideShadowRoot != null)
        {
            sideShadowRoot.anchorMin = Vector2.zero;
            sideShadowRoot.anchorMax = Vector2.one;
            sideShadowRoot.pivot = new Vector2(0.5f, 0.5f);
            sideShadowRoot.offsetMin = Vector2.zero;
            sideShadowRoot.offsetMax = Vector2.zero;
            sideShadowRoot.anchoredPosition = Vector2.zero;
            sideShadowRoot.localScale = Vector3.one;

            Graphic rootGraphic = sideShadowRoot.GetComponent<Graphic>();
            if (rootGraphic != null)
            {
                rootGraphic.raycastTarget = false;
                rootGraphic.enabled = false;
            }
        }

        if (leftShadow != null)
        {
            leftShadow.anchorMin = new Vector2(0f, 0f);
            leftShadow.anchorMax = new Vector2(0f, 1f);
            leftShadow.pivot = new Vector2(0f, 0.5f);
            leftShadow.sizeDelta = new Vector2(sideShadowWidth, 0f);
            leftShadow.anchoredPosition = Vector2.zero;
            leftShadow.localScale = Vector3.one;
            DisableGraphicRaycast(leftShadow);
        }

        if (rightShadow != null)
        {
            rightShadow.anchorMin = new Vector2(1f, 0f);
            rightShadow.anchorMax = new Vector2(1f, 1f);
            rightShadow.pivot = new Vector2(1f, 0.5f);
            rightShadow.sizeDelta = new Vector2(sideShadowWidth, 0f);
            rightShadow.anchoredPosition = Vector2.zero;
            rightShadow.localScale = Vector3.one;
            DisableGraphicRaycast(rightShadow);
        }
    }

    private void DisableGraphicRaycast(RectTransform root)
    {
        if (root == null)
            return;

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in graphics)
            graphic.raycastTarget = false;
    }
}
