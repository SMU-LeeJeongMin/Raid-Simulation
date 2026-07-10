// NPC 1명의 HP/Shield UI 슬롯

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum PartyShieldUIDisplayMode
{
    OverlayCurrentShield,
    ExtraBehindHealth,
    AdjacentToCurrentHealth
}

public class PartyMemberStatusUI : MonoBehaviour
{
    [Header("Target")]
    public NPCPartyMember member;
    public PlayerStatus playerStatus;
    public Health health;
    public ShieldResource shield;
    public PlayerClassInfo classInfo;

    [Header("UI")]
    public Image hpFillImage;
    public Image shieldFillImage;
    public Image portraitImage;
    public TMP_Text nameText;

    [Header("Colors")]
    public Color hpColor = new Color(0.2f, 0.95f, 0.35f, 1f);
    public Color shieldColor = new Color(1f, 1f, 1f, 0.85f);

    [Header("Shield Display")]
    public PartyShieldUIDisplayMode shieldDisplayMode = PartyShieldUIDisplayMode.AdjacentToCurrentHealth;
    public bool shieldFillFromRight = false;
    public bool hideShieldWhenEmpty = true;
    [Min(0f)] public float customShieldDisplayMax = 0f;
    public bool showShieldAtRightWhenHealthFull = true;

    [Header("Options")]
    public bool useGeneratedSolidFillSprite = true;
    public bool emptyWhenUnbound = true;
    public bool showName = false;

    private static Sprite solidFillSprite;

    private void Awake()
    {
        PrepareFillImages();
        ApplyColors();
    }

    private void OnEnable()
    {
        PrepareFillImages();
        ApplyColors();
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    public void Bind(NPCPartyMember newMember)
    {
        member = newMember;
        playerStatus = null;
        health = null;
        shield = null;
        classInfo = null;

        if (member != null)
        {
            member.ResolveReferences();
            playerStatus = member.playerStatus;
            health = member.health;
            shield = member.shield != null ? member.shield : playerStatus != null ? playerStatus.Shield : null;
            classInfo = member.classInfo;
        }

        PrepareFillImages();
        ApplyColors();
        ApplyPortrait();
        ApplyName();
        Refresh();
    }

    public void Clear()
    {
        member = null;
        playerStatus = null;
        health = null;
        shield = null;
        classInfo = null;
        ApplyPortrait();
        ApplyName();
        Refresh();
    }

    public void Refresh()
    {
        RefreshHealth();
        RefreshShield();
    }

    private void RefreshHealth()
    {
        if (health == null)
        {
            if (emptyWhenUnbound)
                SetFill(hpFillImage, 0f, false);
            return;
        }

        float max = Mathf.Max(1f, health.MaxHealth);
        float current = Mathf.Clamp(health.CurrentHealth, 0f, max);
        SetFill(hpFillImage, current / max, false);
    }

    private void RefreshShield()
    {
        if (shieldFillImage == null)
            return;

        float shieldAmount = shield != null ? Mathf.Max(0f, shield.CurrentShield) : 0f;
        if (shieldAmount <= 0f)
        {
            ClearShieldFill();
            if (hideShieldWhenEmpty)
                shieldFillImage.gameObject.SetActive(false);
            return;
        }

        if (hideShieldWhenEmpty)
            shieldFillImage.gameObject.SetActive(true);

        float displayMax = customShieldDisplayMax > 0f
            ? customShieldDisplayMax
            : health != null ? Mathf.Max(1f, health.MaxHealth) : 100f;

        if (shieldDisplayMode == PartyShieldUIDisplayMode.AdjacentToCurrentHealth)
        {
            RefreshAdjacentShield(shieldAmount, displayMax);
            return;
        }

        ResetShieldRectToFullWidth();

        float fill;
        if (shieldDisplayMode == PartyShieldUIDisplayMode.ExtraBehindHealth && health != null)
        {
            float combined = Mathf.Clamp(health.CurrentHealth + shieldAmount, 0f, displayMax);
            fill = combined / displayMax;
        }
        else
        {
            fill = Mathf.Clamp01(shieldAmount / displayMax);
        }

        SetFill(shieldFillImage, fill, shieldFillFromRight);
    }

    private void RefreshAdjacentShield(float shieldAmount, float displayMax)
    {
        if (shieldFillImage == null)
            return;

        float hpRatio = 0f;
        if (health != null)
        {
            float maxHp = Mathf.Max(1f, health.MaxHealth);
            hpRatio = Mathf.Clamp01(health.CurrentHealth / maxHp);
        }

        float shieldRatio = Mathf.Clamp01(shieldAmount / Mathf.Max(1f, displayMax));
        float start = hpRatio;
        float end = Mathf.Clamp01(hpRatio + shieldRatio);

        if (end <= start + 0.001f && showShieldAtRightWhenHealthFull)
        {
            end = 1f;
            start = Mathf.Clamp01(1f - shieldRatio);
        }

        if (end <= start + 0.001f)
        {
            ClearShieldFill();
            return;
        }

        PrepareAdjacentShieldImage();

        RectTransform rect = shieldFillImage.rectTransform;
        rect.anchorMin = new Vector2(start, 0f);
        rect.anchorMax = new Vector2(end, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0f, 0.5f);

        shieldFillImage.fillAmount = 1f;
    }

    private void ApplyPortrait()
    {
        if (portraitImage == null)
            return;

        Sprite sprite = classInfo != null ? classInfo.portraitSprite : null;
        portraitImage.sprite = sprite;
        portraitImage.enabled = sprite != null;
        portraitImage.preserveAspect = true;
    }

    private void ApplyName()
    {
        if (nameText == null)
            return;

        nameText.gameObject.SetActive(showName);
        if (!showName)
            return;

        if (classInfo != null && !string.IsNullOrWhiteSpace(classInfo.jobName))
            nameText.text = classInfo.jobName;
        else if (member != null)
            nameText.text = member.displayName;
        else
            nameText.text = string.Empty;
    }

    private void PrepareFillImages()
    {
        PrepareFillImage(hpFillImage, false);
        PrepareShieldImage();
    }

    private void PrepareShieldImage()
    {
        if (shieldFillImage == null)
            return;

        if (shieldDisplayMode == PartyShieldUIDisplayMode.AdjacentToCurrentHealth)
            PrepareAdjacentShieldImage();
        else
            PrepareFillImage(shieldFillImage, shieldFillFromRight);
    }

    private void PrepareFillImage(Image image, bool fillFromRight)
    {
        if (image == null)
            return;

        if (useGeneratedSolidFillSprite)
            image.sprite = GetSolidFillSprite();

        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = fillFromRight ? 1 : 0;
        image.preserveAspect = false;
    }

    private void PrepareAdjacentShieldImage()
    {
        if (shieldFillImage == null)
            return;

        if (useGeneratedSolidFillSprite)
            shieldFillImage.sprite = GetSolidFillSprite();

        shieldFillImage.type = Image.Type.Simple;
        shieldFillImage.preserveAspect = false;
        shieldFillImage.color = shieldColor;
    }

    private void ResetShieldRectToFullWidth()
    {
        if (shieldFillImage == null)
            return;

        RectTransform rect = shieldFillImage.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private void ClearShieldFill()
    {
        if (shieldFillImage == null)
            return;

        if (shieldDisplayMode == PartyShieldUIDisplayMode.AdjacentToCurrentHealth)
        {
            PrepareAdjacentShieldImage();
            RectTransform rect = shieldFillImage.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            shieldFillImage.fillAmount = 1f;
        }
        else
        {
            SetFill(shieldFillImage, 0f, shieldFillFromRight);
        }
    }

    private void ApplyColors()
    {
        if (hpFillImage != null)
            hpFillImage.color = hpColor;

        if (shieldFillImage != null)
            shieldFillImage.color = shieldColor;
    }

    private void SetFill(Image image, float amount, bool fillFromRight)
    {
        if (image == null)
            return;

        PrepareFillImage(image, fillFromRight);
        image.fillAmount = Mathf.Clamp01(amount);
    }

    private static Sprite GetSolidFillSprite()
    {
        if (solidFillSprite != null)
            return solidFillSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "Generated_PartyUI_SolidFill";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(false, true);

        solidFillSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        solidFillSprite.name = "Generated_PartyUI_SolidFillSprite";
        solidFillSprite.hideFlags = HideFlags.HideAndDontSave;
        return solidFillSprite;
    }
}
