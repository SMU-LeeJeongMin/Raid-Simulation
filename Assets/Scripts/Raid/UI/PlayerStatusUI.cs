// 플레이어 UI
// HP / Shield / Mana / Portrait 표시
// 공통 바 렌더링 로직은 ShieldedHealthBarUI 사용

using UnityEngine;
using UnityEngine.UI;

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
    public ShieldUIDisplayMode shieldDisplayMode = ShieldUIDisplayMode.AdjacentToCurrentHealth;
    public bool shieldFillFromRight = false;
    public bool hideShieldWhenEmpty = true;
    [Min(0f)] public float customShieldDisplayMax = 0f;
    public bool showShieldAtRightWhenHealthFull = true;

    [Header("Fill Sprite Fix")]
    public bool useGeneratedSolidFillSprite = true;

    [Header("Unbound Display")]
    public bool emptyWhenUnbound = true;

    private void Awake()
    {
        PrepareFillImages();
        ApplyColors();
    }

    private void OnEnable()
    {
        PrepareFillImages();
        RefreshPortrait();
        Refresh();
    }

    private void Update()
    {
        // 정적 설정(sprite, type, 색상, 초상화)은 바인딩 시 1회만 적용하고
        // 매 프레임에는 변경된 값만 기록
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
        RefreshShieldBar();
        RefreshMana();
    }

    private void RefreshHealth()
    {
        if (playerHealth == null)
        {
            if (emptyWhenUnbound)
                ShieldedHealthBarUI.SetFill(hpFillImage, 0f);
            return;
        }

        float max = Mathf.Max(1f, playerHealth.MaxHealth);
        float current = Mathf.Clamp(playerHealth.CurrentHealth, 0f, max);
        ShieldedHealthBarUI.SetFill(hpFillImage, current / max);
    }

    private void RefreshShieldBar()
    {
        float shieldAmount = playerShield != null ? Mathf.Max(0f, playerShield.CurrentShield) : 0f;
        ShieldBarSettings settings = BuildShieldSettings();

        ShieldedHealthBarUI.RefreshShield(
            shieldFillImage,
            shieldAmount,
            playerHealth != null ? playerHealth.CurrentHealth : 0f,
            playerHealth != null ? playerHealth.MaxHealth : 0f,
            playerHealth != null,
            settings);
    }

    private void RefreshMana()
    {
        if (playerMana == null)
        {
            if (emptyWhenUnbound)
                ShieldedHealthBarUI.SetFill(manaFillImage, 0f);
            return;
        }

        float max = Mathf.Max(1f, playerMana.MaxMana);
        float current = Mathf.Clamp(playerMana.CurrentMana, 0f, max);
        ShieldedHealthBarUI.SetFill(manaFillImage, current / max);
    }

    // 초상화는 바인딩 시에만 갱신 (매 프레임 sprite 재할당 제거)
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
        ShieldedHealthBarUI.PrepareFillImage(hpFillImage, false, useGeneratedSolidFillSprite);
        ShieldedHealthBarUI.PrepareShieldImage(shieldFillImage, BuildShieldSettings());
        ShieldedHealthBarUI.PrepareFillImage(manaFillImage, false, useGeneratedSolidFillSprite);
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

    private ShieldBarSettings BuildShieldSettings()
    {
        return new ShieldBarSettings
        {
            displayMode = shieldDisplayMode,
            fillFromRight = shieldFillFromRight,
            hideWhenEmpty = hideShieldWhenEmpty,
            customDisplayMax = customShieldDisplayMax,
            showAtRightWhenHealthFull = showShieldAtRightWhenHealthFull,
            useGeneratedSprite = useGeneratedSolidFillSprite,
            shieldColor = shieldColor
        };
    }
}
