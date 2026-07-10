// 스킬 panel의 버튼

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillButtonUI : MonoBehaviour
{
    [Header("Slot")]
    public PlayerSkillSlot slot = PlayerSkillSlot.Skill1;

    [Header("Button / Frame")]
    public Button button;
    public Image frameImage;
    public Image iconImage;

    [Header("Overlays")]
    public Image disabledOverlayImage;
    public Image cooldownOverlayImage;

    [Header("Texts")]
    public TMP_Text cooldownText;
    public TMP_Text ultimateGaugeText;
    public TMP_Text keyText;

    [Header("Visual Options")]
    public bool showUltimateGaugeText = false;
    public bool preserveButtonFrame = true;
    public bool matchOverlayToIconRect = true;
    public bool forceOverlayColors = true;

    public Color disabledOverlayColor = new Color(0f, 0f, 0f, 0.45f);
    public Color cooldownOverlayColor = new Color(0f, 0f, 0f, 0.60f);

    public bool useIconTintWhenNoOverlay = true;
    public Color readyIconColor = Color.white;
    public Color disabledIconColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    public string cooldownFormat = "{0}";

    [Header("Runtime Debug")]
    [SerializeField] private bool lastCanUse;
    [SerializeField] private string lastDisableReason;
    [SerializeField] private int lastUltimateGaugePercent;

    private SkillPanelUI owner;

    private void Reset()
    {
        button = GetComponent<Button>();
        frameImage = GetComponent<Image>();

        iconImage = FindImageByName("Icon");
        disabledOverlayImage = FindImageByName("DisabledOverlay");
        cooldownOverlayImage = FindImageByName("CooldownOverlay");
        cooldownText = FindTextByName("CooldownText");
        ultimateGaugeText = FindTextByName("UltimateGaugeText");
        keyText = FindTextByName("KeyText");
    }

    private void Awake()
    {
        CacheReferencesIfMissing();
        ApplyStaticVisualSettings();
    }

    public void Initialize(SkillPanelUI newOwner, PlayerSkillSlot newSlot)
    {
        owner = newOwner;
        slot = newSlot;

        CacheReferencesIfMissing();
        ApplyStaticVisualSettings();

        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClicked);
            button.onClick.AddListener(OnButtonClicked);
        }
    }

    public void SetIcon(Sprite icon)
    {
        CacheReferencesIfMissing();

        if (iconImage == null)
            return;

        if (icon == null)
        {
            iconImage.sprite = null;
            iconImage.color = new Color(1f, 1f, 1f, 0f);
            iconImage.enabled = false;
            return;
        }

        iconImage.enabled = true;
        iconImage.sprite = icon;
        iconImage.color = readyIconColor;
        iconImage.preserveAspect = true;
    }

    public void Refresh(PlayerSkillController controller, bool checkTargetRangeForDisabled)
    {
        CacheReferencesIfMissing();
        ApplyStaticVisualSettings();

        if (controller == null)
        {
            SetDisabledVisual(true, false);
            SetCooldownOverlay(false, 0f);
            SetCooldownText(false, 0f);
            SetUltimateText(slot == PlayerSkillSlot.Ultimate && showUltimateGaugeText, 0);
            SetButtonInteractable(false);
            return;
        }

        string reason;
        bool canUse = controller.CanUseSkillForUI(slot, checkTargetRangeForDisabled, out reason);
        lastCanUse = canUse;
        lastDisableReason = reason;
        if (slot == PlayerSkillSlot.Ultimate)
            lastUltimateGaugePercent = controller.GetUltimateGaugePercentInt();
        bool isCastingAny = controller.IsSkillInProgress;
        bool isCurrentCasting = controller.IsCastingSlot(slot);
        bool isOtherSkillCasting = isCastingAny && !isCurrentCasting;

        float cooldownRemaining = controller.GetCooldownRemaining(slot);
        float cooldownDuration = controller.GetCooldownDuration(slot);
        bool onCooldown = !isCurrentCasting && cooldownRemaining > 0.05f;

        bool disabledVisual = false;

        if (isOtherSkillCasting)
            disabledVisual = true;
        else if (onCooldown)
            disabledVisual = true;
        else if (!canUse && !isCurrentCasting)
            disabledVisual = true;

        SetDisabledVisual(disabledVisual, isCurrentCasting);
        SetButtonInteractable(canUse && !isCastingAny);

        if (onCooldown)
        {
            float normalized = cooldownDuration <= 0.0001f ? 0f : Mathf.Clamp01(cooldownRemaining / cooldownDuration);
            SetCooldownOverlay(true, normalized);
            SetCooldownText(true, Mathf.Ceil(cooldownRemaining));
        }
        else
        {
            SetCooldownOverlay(false, 0f);
            SetCooldownText(false, 0f);
        }

        if (slot == PlayerSkillSlot.Ultimate && showUltimateGaugeText)
            SetUltimateText(true, controller.GetUltimateGaugePercentInt());
        else
            SetUltimateText(false, 0);
    }

    private void OnButtonClicked()
    {
        if (owner != null)
            owner.TryUseSkill(slot);
    }

    private void CacheReferencesIfMissing()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (frameImage == null)
            frameImage = GetComponent<Image>();

        if (iconImage == null)
            iconImage = FindImageByName("Icon");

        if (disabledOverlayImage == null)
            disabledOverlayImage = FindImageByName("DisabledOverlay");

        if (cooldownOverlayImage == null)
            cooldownOverlayImage = FindImageByName("CooldownOverlay");

        if (cooldownText == null)
            cooldownText = FindTextByName("CooldownText");

        if (ultimateGaugeText == null)
            ultimateGaugeText = FindTextByName("UltimateGaugeText");

        if (keyText == null)
            keyText = FindTextByName("KeyText");
    }

    private Image FindImageByName(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return null;

        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
                continue;

            if (image.gameObject == gameObject)
                continue;

            if (image.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return image;
        }

        return null;
    }

    private TMP_Text FindTextByName(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return null;

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            if (text.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return text;
        }

        return null;
    }

    private void ApplyStaticVisualSettings()
    {
        if (button != null && preserveButtonFrame)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            button.colors = colors;
        }

        if (iconImage != null)
        {
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
        }

        PrepareDisabledOverlay();
        PrepareCooldownOverlay();
        SyncOverlayRectsToIcon();
    }

    private void PrepareDisabledOverlay()
    {
        if (disabledOverlayImage == null)
            return;

        disabledOverlayImage.raycastTarget = false;
        disabledOverlayImage.type = Image.Type.Simple;

        if (forceOverlayColors)
            disabledOverlayImage.color = disabledOverlayColor;
    }

    private void PrepareCooldownOverlay()
    {
        if (cooldownOverlayImage == null)
            return;

        cooldownOverlayImage.raycastTarget = false;
        cooldownOverlayImage.type = Image.Type.Filled;
        cooldownOverlayImage.fillMethod = Image.FillMethod.Radial360;
        cooldownOverlayImage.fillOrigin = 2;

        if (forceOverlayColors)
            cooldownOverlayImage.color = cooldownOverlayColor;
    }

    private void SyncOverlayRectsToIcon()
    {
        if (!matchOverlayToIconRect || iconImage == null)
            return;

        RectTransform iconRect = iconImage.rectTransform;
        CopyRectTransform(iconRect, disabledOverlayImage != null ? disabledOverlayImage.rectTransform : null);
        CopyRectTransform(iconRect, cooldownOverlayImage != null ? cooldownOverlayImage.rectTransform : null);
    }

    private void CopyRectTransform(RectTransform source, RectTransform target)
    {
        if (source == null || target == null)
            return;

        if (source.parent != target.parent)
            return;

        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.localScale = source.localScale;
        target.localRotation = source.localRotation;
    }

    private void SetButtonInteractable(bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    private void SetDisabledVisual(bool disabled, bool isCurrentCasting)
    {
        bool visible = disabled && !isCurrentCasting;

        if (disabledOverlayImage != null)
        {
            if (forceOverlayColors)
                disabledOverlayImage.color = disabledOverlayColor;
            disabledOverlayImage.gameObject.SetActive(visible);
        }

        if (iconImage != null && useIconTintWhenNoOverlay && disabledOverlayImage == null)
            iconImage.color = visible ? disabledIconColor : readyIconColor;
        else if (iconImage != null && iconImage.sprite != null)
            iconImage.color = readyIconColor;
    }

    private void SetCooldownOverlay(bool visible, float normalized)
    {
        if (cooldownOverlayImage == null)
            return;

        if (forceOverlayColors)
            cooldownOverlayImage.color = cooldownOverlayColor;

        cooldownOverlayImage.gameObject.SetActive(visible);
        cooldownOverlayImage.type = Image.Type.Filled;
        cooldownOverlayImage.fillMethod = Image.FillMethod.Radial360;
        cooldownOverlayImage.fillOrigin = 2;
        cooldownOverlayImage.fillAmount = Mathf.Clamp01(normalized);
    }

    private void SetCooldownText(bool visible, float value)
    {
        if (cooldownText == null)
            return;

        cooldownText.gameObject.SetActive(visible);
        if (visible)
            cooldownText.text = string.Format(cooldownFormat, Mathf.Max(0, Mathf.CeilToInt(value)));
    }

    private void SetUltimateText(bool visible, int percent)
    {
        if (ultimateGaugeText == null)
            return;

        ultimateGaugeText.gameObject.SetActive(visible);
        if (visible)
            ultimateGaugeText.text = Mathf.Clamp(percent, 0, 100).ToString() + "%";
    }
}
