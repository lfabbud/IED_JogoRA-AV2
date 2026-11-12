using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Painel de teste em tela, criado inteiramente por script (nada a montar na cena além do objeto com este script).
// Um botão "DEBUG" centralizado no topo da tela abre e fecha o painel; funciona também no build.
// O RunnerGame cadastra as linhas (valores ajustáveis, informações e botões de ação).
// Só aparece se GameSettings.debugEnabled estiver marcado.
public class DebugPanel : MonoBehaviour
{
    class Live
    {
        public TMP_Text text;
        public Func<string> read;
    }

    GameObject root;
    GameObject panel;
    RectTransform toggleRect;
    RectTransform panelRect;
    Transform content;
    TMP_Text toggleLabel;
    readonly List<Live> live = new List<Live>();
    float refreshTimer;
    float fps;
    int frames;
    float fpsTimer;

    void Awake()
    {
        Build();
    }

    public void Setup(bool enabled)
    {
        if (root != null) root.SetActive(enabled);
    }

    // O toque cai sobre o botão ou o painel? (o jogo ignora esses toques)
    public bool BlocksTap(Vector2 screenPos)
    {
        if (root == null || !root.activeSelf) return false;
        if (RectTransformUtility.RectangleContainsScreenPoint(toggleRect, screenPos, null)) return true;
        return panel.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(panelRect, screenPos, null);
    }

    void Build()
    {
        root = new GameObject("DebugCanvas");
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

        var toggle = CreateButton(root.transform, "BotaoDebug", "DEBUG", new Vector2(220f, 80f), Toggle);
        toggleRect = (RectTransform)toggle.transform;
        toggleRect.anchorMin = new Vector2(0.5f, 1f);
        toggleRect.anchorMax = new Vector2(0.5f, 1f);
        toggleRect.pivot = new Vector2(0.5f, 1f);
        toggleRect.anchoredPosition = new Vector2(0f, -20f);
        toggleLabel = toggle.GetComponentInChildren<TMP_Text>();

        panel = new GameObject("PainelDebug", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(root.transform, false);
        panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.sizeDelta = new Vector2(980f, 1560f);
        panelRect.anchoredPosition = new Vector2(0f, -110f);
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);
        var v = panel.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(16, 16, 16, 16);
        v.spacing = 8f;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        v.childAlignment = TextAnchor.UpperCenter;
        content = panel.transform;
        panel.SetActive(false);

        AddInfo(delegate { return "FPS " + fps.ToString("0"); });
    }

    void Toggle()
    {
        panel.SetActive(!panel.activeSelf);
        if (toggleLabel) toggleLabel.text = panel.activeSelf ? "FECHAR" : "DEBUG";
        Refresh();
    }

    void Update()
    {
        frames++;
        fpsTimer += Time.unscaledDeltaTime;
        if (fpsTimer >= 0.5f)
        {
            fps = frames / fpsTimer;
            frames = 0;
            fpsTimer = 0f;
        }
        if (panel == null || !panel.activeSelf) return;
        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer >= 0.2f)
        {
            refreshTimer = 0f;
            Refresh();
        }
    }

    void Refresh()
    {
        foreach (var l in live)
        {
            if (l.text != null) l.text.text = l.read();
        }
    }

    // ---------- linhas ----------

    public void AddInfo(Func<string> read)
    {
        var row = NewRow();
        var t = CreateText(row.transform, "", 26f, TextAlignmentOptions.Left);
        SetWidth(t.gameObject, 0f, 1f);
        live.Add(new Live { text = t, read = read });
    }

    public void AddFloat(string label, Func<float> get, Action<float> set, float step, float min, float max, string format = "0.##")
    {
        var row = NewRow();
        var lab = CreateText(row.transform, label, 26f, TextAlignmentOptions.Left);
        SetWidth(lab.gameObject, 0f, 1f);
        var minus = CreateButton(row.transform, "Menos", "-", new Vector2(90f, 64f), delegate
        {
            set(Mathf.Clamp(Mathf.Round((get() - step) * 1000f) / 1000f, min, max));
            Refresh();
        });
        SetWidth(minus.gameObject, 90f, 0f);
        var val = CreateText(row.transform, "", 26f, TextAlignmentOptions.Center);
        SetWidth(val.gameObject, 170f, 0f);
        var plus = CreateButton(row.transform, "Mais", "+", new Vector2(90f, 64f), delegate
        {
            set(Mathf.Clamp(Mathf.Round((get() + step) * 1000f) / 1000f, min, max));
            Refresh();
        });
        SetWidth(plus.gameObject, 90f, 0f);
        live.Add(new Live { text = val, read = delegate { return get().ToString(format); } });
    }

    public void AddBool(string label, Func<bool> get, Action<bool> set)
    {
        var row = NewRow();
        var lab = CreateText(row.transform, label, 26f, TextAlignmentOptions.Left);
        SetWidth(lab.gameObject, 0f, 1f);
        var btn = CreateButton(row.transform, "Chave", "", new Vector2(200f, 64f), delegate
        {
            set(!get());
            Refresh();
        });
        SetWidth(btn.gameObject, 200f, 0f);
        live.Add(new Live { text = btn.GetComponentInChildren<TMP_Text>(), read = delegate { return get() ? "LIGADO" : "desligado"; } });
    }

    public void AddButtons(string[] labels, Action[] actions)
    {
        var row = NewRow();
        for (int i = 0; i < labels.Length; i++)
        {
            var action = actions[i];
            var b = CreateButton(row.transform, "Acao", labels[i], new Vector2(200f, 64f), delegate { action(); Refresh(); });
            SetWidth(b.gameObject, 0f, 1f);
        }
    }

    // ---------- construção de interface ----------

    GameObject NewRow()
    {
        var go = new GameObject("Linha", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(content, false);
        var h = go.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 8f;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;
        go.GetComponent<LayoutElement>().preferredHeight = 64f;
        return go;
    }

    static void SetWidth(GameObject go, float preferred, float flexible)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredWidth = preferred;
        le.flexibleWidth = flexible;
    }

    static TMP_Text CreateText(Transform parent, string text, float size, TextAlignmentOptions align)
    {
        var go = new GameObject("Texto", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    static Button CreateButton(Transform parent, string name, string label, Vector2 size, UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        ((RectTransform)go.transform).sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.3f, 0.95f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        var t = CreateText(go.transform, label, 28f, TextAlignmentOptions.Center);
        var rt = (RectTransform)t.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return btn;
    }
}
