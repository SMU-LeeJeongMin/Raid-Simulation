// 플레이어 UI
// HP / Shield / Mana / Portrait 표시

using UnityEngine;
using UnityEngine.UI;

public enum PlayerShieldUIDisplayMode
{
    OverlayCurrentShield,
    ExtraBehindHealth,
    AdjacentToCurrentHealth
}

public class PlayerStatusUI : MonoBehaviour
{
    [Header("Target")]
    public PlayerStatus playerStatus;
    public Health playerHealth;
    public Mana playerMana;
    public ShieldResource playerShield;
    public PlayerClassInfo playerClassInfo;

    [Header("Fill Images")]
    public Image hpFillImage;
    public Image shieldFillImage;
    public Image manaFillImage;

    [Header("Portrait")]
    public Image portraitImage;
    public Sprite portraitFallbackSprite;
    public bool hidePortraitWhenMissing = false;

    [Header("Colors")]
    public Color hpColor = new Color(0.2f, 0.95f, 0.35f, 1f);
    public Color shieldColor = new Color(1f, 1f, 1f, 0.85f);
    public Color manaColor = new Color(0.2f, 0.55f, 1f, 1f);

    [Header("Shield Display")]
    public PlayerShieldUIDisplayMode shieldDisplayMode = PlayerShieldUIDisplayMode.AdjacentToCurrentHealth;
    public bool shieldFillFromRight = false;
    public bool hideShieldWhenEmpty = true;
    [Min(0f)] public float customShieldDisplayMax = 0f;
    public bool showShieldAtRightWhenHealthFull = true;

    [Header("Fill Sprite Fix")]
    public bool useGeneratedSolidFillSprite = true;

    [Header("Unbound Display")]
    public bool emptyWhenUnbound = true;

    private static Sprite solidFillSprite;

    private void Awake()
    {
        PrepareFillImages();
        ApplyColors();
    }

    private void OnEnable()
    {
        PrepareFillImages();
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    public void Bind(PlayerStatus status)
    {
        playerStatus = status;
        if (playerStatus != null)
        {
            playerStatus.ResolveReferences();
            playerHealth = playerStatus.Health;
            playerMana = playerStatus.Mana;
            playerShield = playerStatus.Shield;
            playerClassInfo = playerStatus.ClassInfo != null ? playerStatus.ClassInfo : playerStatus.GetComponent<PlayerClassInfo>();
        }
        else
        {
            playerHealth = null;
            playerMana = null;
            playerShield = null;
            playerClassInfo = null;
        }

        PrepareFillImages();
        ApplyColors();
        RefreshPortrait();
        Refresh();
    }

    public void Bind(Health health, Mana mana, PlayerClassInfo info = null)
    {
        playerStatus = health != null ? health.GetComponent<PlayerStatus>() : null;
        playerHealth = health;
        playerMana = mana;
        playerShield = playerStatus != null ? playerStatus.Shield : health != null ? health.GetComponent<ShieldResource>() : null;
        playerClassInfo = info != null ? info : health != null ? health.GetComponent<PlayerClassInfo>() : null;

        PrepareFillImages();
        ApplyColors();
        RefreshPortrait();
        Refresh();
    }

    public void Refresh()
    {
        RefreshHealth();
        RefreshShield();
        RefreshMana();
        RefreshPortrait();
    }

    private void RefreshHealth()
    {
        if (playerHealth == null)
        {
            if (emptyWhenUnbound)
                SetFill(hpFillImage, 0f, false);
            return;
        }

        float max = Mathf.Max(1f, playerHealth.MaxHealth);
        float current = Mathf.Clamp(playerHealth.CurrentHealth, 0f, max);
        SetFill(hpFillImage, current / max, false);
    }

    private void RefreshShield()
    {
        if (shieldFillImage == null)
            return;

        float shieldAmount = playerShield != null ? Mathf.Max(0f, playerShield.CurrentShield) : 0f;
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
            : playerHealth != null ? Mathf.Max(1f, playerHealth.MaxHealth) : 100f;

        if (shieldDisplayMode == PlayerShieldUIDisplayMode.AdjacentToCurrentHealth)
        {
            RefreshAdjacentShield(shieldAmount, displayMax);
            return;
        }

        ResetShieldRectToFullWidth();

        float fill;
        if (shieldDisplayMode == PlayerShieldUIDisplayMode.ExtraBehindHealth && playerHealth != null)
        {
            float combined = Mathf.Clamp(playerHealth.CurrentHealth + shieldAmount, 0f, displayMax);
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
        if (playerHealth != null)
        {
            float maxHp = Mathf.Max(1f, playerHealth.MaxHealth);
            hpRatio = Mathf.Clamp01(playerHealth.CurrentHealth / maxHp);
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

    private void RefreshMana()
    {
        if (playerMana == null)
        {
            if (emptyWhenUnbound)
                SetFill(manaFillImage, 0f, false);
            return;
        }

        float max = Mathf.Max(1f, playerMana.MaxMana);
        float current = Mathf.Clamp(playerMana.CurrentMana, 0f, max);
        SetFill(manaFillImage, current / max, false);
    }

    private void RefreshPortrait()
    {
        if (portraitImage == null)
            return;

        Sprite sprite = null;
        if (playerClassInfo != null)
            sprite = playerClassInfo.portraitSprite;

        if (sprite == null)
            sprite = portraitFallbackSprite;

        portraitImage.sprite = sprite;
        portraitImage.color = Color.white;
        portraitImage.enabled = sprite != null || !hidePortraitWhenMissing;
    }

    private void PrepareFillImages()
    {
        PrepareFillImage(hpFillImage, false);
        PrepareShieldImage();
        PrepareFillImage(manaFillImage, false);
    }

    private void PrepareShieldImage()
    {
        if (shieldFillImage == null)
            return;

        if (shieldDisplayMode == PlayerShieldUIDisplayMode.AdjacentToCurrentHealth)
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

        if (shieldDisplayMode == PlayerShieldUIDisplayMode.AdjacentToCurrentHealth)
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

        if (manaFillImage != null)
            manaFillImage.color = manaColor;
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
