using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Única opção de teste em tela: o checkbox "Show boxes", no canto superior esquerdo, antes de a corrida começar.
// Criado inteiramente por script (nada a montar na cena). Ligado, o jogo desenha as caixas dos assets, as 5 pistas e o caminho
// dos itens. O RunnerGame mostra e esconde o checkbox conforme a fase do jogo.
public class ShowBoxesToggle : MonoBehaviour
{
    public event System.Action<bool> Changed;
    public bool IsOn { get; private set; }

    GameObject root;
    RectTransform rect;
    Image mark;

    void Awake()
    {
        root = new GameObject("ShowBoxes");
        root.transform.SetParent(transform, false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        var row = new GameObject("Linha", typeof(RectTransform), typeof(Image), typeof(Button));
        row.transform.SetParent(root.transform, false);
        rect = (RectTransform)row.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(20f, -20f);
        rect.sizeDelta = new Vector2(360f, 80f);
        var bg = row.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);
        var btn = row.GetComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(Sfx.PlayButton);
        btn.onClick.AddListener(Toggle);

        var box = new GameObject("Caixa", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(row.transform, false);
        var br = (RectTransform)box.transform;
        br.anchorMin = new Vector2(0f, 0.5f);
        br.anchorMax = new Vector2(0f, 0.5f);
        br.pivot = new Vector2(0f, 0.5f);
        br.anchoredPosition = new Vector2(16f, 0f);
        br.sizeDelta = new Vector2(48f, 48f);
        box.GetComponent<Image>().color = Color.white;

        var m = new GameObject("Marca", typeof(RectTransform), typeof(Image));
        m.transform.SetParent(box.transform, false);
        var mr = (RectTransform)m.transform;
        mr.anchorMin = Vector2.zero;
        mr.anchorMax = Vector2.one;
        mr.offsetMin = new Vector2(8f, 8f);
        mr.offsetMax = new Vector2(-8f, -8f);
        mark = m.GetComponent<Image>();
        mark.color = new Color(0.1f, 0.6f, 0.2f, 1f);
        mark.enabled = false;
        mark.raycastTarget = false;

        var tgo = new GameObject("Texto", typeof(RectTransform));
        tgo.transform.SetParent(row.transform, false);
        var t = tgo.AddComponent<TextMeshProUGUI>();
        if (LevelThemeApplier.ButtonFont != null) t.font = LevelThemeApplier.ButtonFont;
        t.text = "Show boxes";
        t.fontSize = 10f;
        t.alignment = TextAlignmentOptions.Left;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.color = Color.white;
        t.raycastTarget = false;
        var tr = (RectTransform)tgo.transform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(80f, 0f);
        tr.offsetMax = new Vector2(-10f, 0f);
    }

    void Toggle()
    {
        IsOn = !IsOn;
        mark.enabled = IsOn;
        if (Changed != null) Changed(IsOn);
    }

    public void SetVisible(bool visible)
    {
        if (root != null && root.activeSelf != visible) root.SetActive(visible);
    }

    // O toque cai sobre o checkbox? (o jogo ignora esse toque)
    public bool BlocksTap(Vector2 screenPos)
    {
        return root != null && root.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, null);
    }
}
