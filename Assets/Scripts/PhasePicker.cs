using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Lista de fases dentro do jogo (criada inteiramente por script, nada a montar na cena). Abre por cima de tudo e, ao tocar
// numa fase, avisa o RunnerGame, que troca a fase sem escanear de novo. Mostra as fases já liberadas; com
// GameSettings.testUnlockAllLevels marcado, mostra todas (as bloqueadas aparecem marcadas).
public class PhasePicker : MonoBehaviour
{
    public event Action<LevelConfig> Chosen;

    GameObject root;

    public void Open(LevelCatalog catalog, bool showAll, LevelConfig current)
    {
        Close();
        root = new GameObject("SeletorDeFases");
        root.transform.SetParent(transform, false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6000;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        var bg = new GameObject("Fundo", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(root.transform, false);
        var br = (RectTransform)bg.transform;
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.offsetMin = Vector2.zero;
        br.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

        var list = new GameObject("Lista", typeof(RectTransform), typeof(VerticalLayoutGroup));
        list.transform.SetParent(bg.transform, false);
        var lr = (RectTransform)list.transform;
        lr.anchorMin = new Vector2(0.5f, 0.5f);
        lr.anchorMax = new Vector2(0.5f, 0.5f);
        lr.pivot = new Vector2(0.5f, 0.5f);
        lr.sizeDelta = new Vector2(800f, 1200f);
        var v = list.GetComponent<VerticalLayoutGroup>();
        v.spacing = 14f;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        v.childAlignment = TextAnchor.MiddleCenter;

        AddText(list.transform, "Escolha a fase", 48f, 90f);
        int listadas = 0;
        if (catalog != null && catalog.levels != null)
        {
            foreach (var lv in catalog.levels)
            {
                if (lv == null) continue;
                bool unlocked = SaveSystem.IsUnlocked(lv) || lv == current;
                if (!unlocked && !showAll) continue;
                string label = lv.displayName + (lv == current ? "  (atual)" : "") + (unlocked ? "" : "  (bloqueada)");
                var chosen = lv;
                listadas++;
                AddButton(list.transform, label, delegate
                {
                    Close();
                    if (Chosen != null) Chosen(chosen);
                });
            }
        }
        AddButton(list.transform, "Fechar", Close);
        Debug.Log("[Lu] Seletor de fases aberto: " + listadas + " fase(s) listada(s); testUnlockAllLevels = " + showAll + ".");
    }

    public void Close()
    {
        if (root != null) Destroy(root);
        root = null;
    }

    static void AddText(Transform parent, string text, float size, float height)
    {
        var go = new GameObject("Titulo", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
    }

    static void AddButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Botao", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 120f;
        var img = go.GetComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.3f, 0.95f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(Sfx.PlayButton);
        btn.onClick.AddListener(onClick);
        var tgo = new GameObject("Texto", typeof(RectTransform));
        tgo.transform.SetParent(go.transform, false);
        var t = tgo.AddComponent<TextMeshProUGUI>();
        t.text = label;
        t.fontSize = 40f;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        var rt = (RectTransform)tgo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
