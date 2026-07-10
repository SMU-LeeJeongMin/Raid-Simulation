// 소환되는 enemy Hp bar
using UnityEngine;
using UnityEngine.UI;

public class WorldHealthBarUI : MonoBehaviour
{
    [Header("Target")]
    public Health targetHealth;
    public Transform followTarget;
    public Vector3 worldOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Style")]
    public float width = 1.2f;
    public float height = 0.16f;
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
    public Color fillColor = new Color(1f, 0.1f, 0.1f, 0.95f);
    public bool hideWhenDead = true;

    private Canvas canvas;
    private Image fillImage;
    private static Sprite solidSprite;

    private void Awake()
    {
        if (targetHealth == null)
            targetHealth = GetComponent<Health>();

        if (followTarget == null)
            followTarget = transform;

        BuildUIIfNeeded();
    }

    private void LateUpdate()
    {
        if (canvas == null || fillImage == null)
            BuildUIIfNeeded();

        Refresh();
        FaceCamera();
    }

    private void BuildUIIfNeeded()
    {
        if (canvas != null)
            return;

        GameObject canvasObject = new GameObject("WorldHealthBarUI");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(width, height);
        canvasRect.localPosition = worldOffset;
        canvasRect.localRotation = Quaternion.identity;
        canvasRect.localScale = Vector3.one;

        GameObject bgObject = new GameObject("Background");
        bgObject.transform.SetParent(canvasObject.transform, false);
        Image bg = bgObject.AddComponent<Image>();
        bg.sprite = GetSolidSprite();
        bg.color = backgroundColor;
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        Stretch(bgRect);

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(canvasObject.transform, false);
        fillImage = fillObject.AddComponent<Image>();
        fillImage.sprite = GetSolidSprite();
        fillImage.color = fillColor;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;
        RectTransform fillRect = fillImage.GetComponent<RectTransform>();
        Stretch(fillRect);
    }

    private void Refresh()
    {
        if (targetHealth == null || fillImage == null)
            return;

        if (hideWhenDead && targetHealth.IsDead)
        {
            if (canvas != null)
                canvas.gameObject.SetActive(false);
            return;
        }

        if (canvas != null && !canvas.gameObject.activeSelf)
            canvas.gameObject.SetActive(true);

        fillImage.fillAmount = Mathf.Clamp01(targetHealth.NormalizedHealth);
    }

    private void FaceCamera()
    {
        if (canvas == null)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Transform t = canvas.transform;
        t.position = (followTarget != null ? followTarget.position : transform.position) + worldOffset;
        t.rotation = Quaternion.LookRotation(t.position - cam.transform.position, Vector3.up);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Sprite GetSolidSprite()
    {
        if (solidSprite != null)
            return solidSprite;

        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.SetPixel(0, 0, Color.white);
        tex.Apply(false, true);
        solidSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        solidSprite.hideFlags = HideFlags.HideAndDontSave;
        return solidSprite;
    }
}
