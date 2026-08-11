// NPC 1명의 HP/Shield UI 슬롯
// 공통 바 렌더링 로직은 ShieldedHealthBarUI 사용

using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    public ShieldUIDisplayMode shieldDisplayMode = ShieldUIDisplayMode.AdjacentToCurrentHealth;
    public bool shieldFillFromRight = false;
    public bool hideShieldWhenEmpty = true;
    [Min(0f)] public float customShieldDisplayMax = 0f;
    public bool showShieldAtRightWhenHealthFull = true;

    [Header("Options")]
    public bool useGeneratedSolidFillSprite = true;
    public bool emptyWhenUnbound = true;
    public bool showName = false;

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
        // 정적 설정은 바인딩 시 1회만 적용하고 매 프레임에는 변경된 값만 기록
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
        RefreshShieldBar();
    }

    private void RefreshHealth()
    {
        if (health == null)
        {
            if (emptyWhenUnbound)
                ShieldedHealthBarUI.SetFill(hpFillImage, 0f);
            return;
        }

        float max = Mathf.Max(1f, health.MaxHealth);
        float current = Mathf.Clamp(health.CurrentHealth, 0f, max);
        ShieldedHealthBarUI.SetFill(hpFillImage, current / max);
    }

    private void RefreshShieldBar()
    {
        float shieldAmount = shield != null ? Mathf.Max(0f, shield.CurrentShield) : 0f;
        ShieldBarSettings settings = BuildShieldSettings();

        ShieldedHealthBarUI.RefreshShield(
            shieldFillImage,
            shieldAmount,
            health != null ? health.CurrentHealth : 0f,
            health != null ? health.MaxHealth : 0f,
            health != null,
            settings);
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
        ShieldedHealthBarUI.PrepareFillImage(hpFillImage, false, useGeneratedSolidFillSprite);
        ShieldedHealthBarUI.PrepareShieldImage(shieldFillImage, BuildShieldSettings());
    }

    private void ApplyColors()
    {
        if (hpFillImage != null)
            hpFillImage.color = hpColor;

        if (shieldFillImage != null)
            shieldFillImage.color = shieldColor;
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
