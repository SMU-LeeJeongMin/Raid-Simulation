using TMPro;
using UnityEngine;

/// <summary>
/// 즉사기 경고 문구의 화면 표시.
/// 별도 씬 구성 없이 호출 시점에 오버레이 캔버스를 생성하고 남은 시간을 함께 표시.
/// </summary>
public class OneShotWarningUI : MonoBehaviour
{
    private static OneShotWarningUI instance;

    private TMP_Text text;
    private string message;
    private float endTime;

    public static void Show(string message, float duration)
    {
        if (instance == null)
            instance = Create();

        instance.message = message;
        instance.endTime = Time.time + Mathf.Max(0.1f, duration);
        instance.gameObject.SetActive(true);
    }

    public static void Hide()
    {
        if (instance != null)
            instance.gameObject.SetActive(false);
    }

    private static OneShotWarningUI Create()
    {
        GameObject root = new GameObject("OneShotWarningUI");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        GameObject textObject = new GameObject("WarningText");
        textObject.transform.SetParent(root.transform, false);

        TMP_Text label = textObject.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 26f;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(1f, 0.25f, 0.2f, 1f);
        label.outlineWidth = 0.2f;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -90f);
        rect.sizeDelta = new Vector2(900f, 70f);

        OneShotWarningUI ui = root.AddComponent<OneShotWarningUI>();
        ui.text = label;
        return ui;
    }

    private void Update()
    {
        if (text == null)
            return;

        text.text = message;

        // 깜빡임: 남은 시간이 줄수록 빠르게
        float remaining = Mathf.Max(0f, endTime - Time.time);
        float speed = remaining > 2f ? 4f : 10f;
        float alpha = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * speed));
        Color color = text.color;
        color.a = alpha;
        text.color = color;
    }
}
