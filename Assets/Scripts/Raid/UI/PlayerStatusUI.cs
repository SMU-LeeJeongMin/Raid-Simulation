// 플레이어 UI
using UnityEngine;
using UnityEngine.UI;

public class PlayerStatusUI : MonoBehaviour
{
    [Header("Target")]
    public PlayerStatus playerStatus;
    public Health playerHealth;
    public Mana playerMana;

    [Header("Fill Images")]
    public Image hpFillImage;
    public Image manaFillImage;

    [Header("Colors")]
    public Color hpColor = new Color(0.2f, 0.95f, 0.35f, 1f);
    public Color manaColor = new Color(0.2f, 0.55f, 1f, 1f);

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
        }
        else
        {
            playerHealth = null;
            playerMana = null;
        }

        PrepareFillImages();
        ApplyColors();
        Refresh();
    }

    public void Bind(Health health, Mana mana, PlayerClassInfo info = null)
    {
        playerStatus = health != null ? health.GetComponent<PlayerStatus>() : null;
        playerHealth = health;
        playerMana = mana;

        PrepareFillImages();
        ApplyColors();
        Refresh();
    }

    public void Refresh()
    {
        RefreshHealth();
        RefreshMana();
    }

    private void RefreshHealth()
    {
        if (playerHealth == null)
        {
            if (emptyWhenUnbound)
                SetFill(hpFillImage, 0f);
            return;
        }

        float max = Mathf.Max(1f, playerHealth.MaxHealth);
        float current = Mathf.Clamp(playerHealth.CurrentHealth, 0f, max);
        SetFill(hpFillImage, current / max);
    }

    private void RefreshMana()
    {
        if (playerMana == null)
        {
            if (emptyWhenUnbound)
                SetFill(manaFillImage, 0f);
            return;
        }

        float max = Mathf.Max(1f, playerMana.MaxMana);
        float current = Mathf.Clamp(playerMana.CurrentMana, 0f, max);
        SetFill(manaFillImage, current / max);
    }

    private void PrepareFillImages()
    {
        PrepareFillImage(hpFillImage);
        PrepareFillImage(manaFillImage);
    }

    private void PrepareFillImage(Image image)
    {
        if (image == null)
            return;

        if (useGeneratedSolidFillSprite)
            image.sprite = GetSolidFillSprite();

        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = 0;
        image.preserveAspect = false;
    }

    private void ApplyColors()
    {
        if (hpFillImage != null)
            hpFillImage.color = hpColor;

        if (manaFillImage != null)
            manaFillImage.color = manaColor;
    }

    private void SetFill(Image image, float amount)
    {
        if (image == null)
            return;

        PrepareFillImage(image);
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
