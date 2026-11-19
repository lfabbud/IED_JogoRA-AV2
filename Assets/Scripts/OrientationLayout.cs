using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Guarda duas posições (vertical e horizontal) para todos os elementos de interface DENTRO do objeto onde este script está
// (em qualquer nível, identificados pelo caminho, por exemplo "TelaGameOver/BotaoTentar") e aplica a certa conforme a
// orientação do aparelho, trocando ao girar o celular. Não há nada para arrastar.
// Pode ficar no Canvas (um por cena, cobre todas as telas) ou em uma tela só.
// No Canvas, as telas (filhos diretos) são o "quadro" que muda de tamanho na edição; numa tela só, o quadro é ela mesma.
// O leiaute padrão é o vertical (o quadro ocupa o Canvas, com a janela Game na vertical). Escolhendo "Horizontal" na chave
// "Leiaute" do Inspector, o quadro passa a ter a proporção em pixels do horizontal. Posicione na Scene e clique em
// "Salvar posição deste leiaute". Voltar a "Vertical" devolve tudo ao normal. Sem horizontal salva, vale a vertical.
// Filhos de objetos com Layout Group não são guardados (o Layout Group manda neles).
public class OrientationLayout : MonoBehaviour
{
    public enum Layout { Vertical, Horizontal }

    // GameSettings.followDeviceOrientation: desmarcado (padrão) = interface sempre no leiaute vertical;
    // marcado = o leiaute gira com o aparelho (horizontal quando a largura da tela passa da altura).
    public static bool FollowDevice = false;

    [Serializable]
    public struct Placement
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
    }

    [Serializable]
    public class Saved
    {
        public string path;
        public bool hasPortrait;
        public bool hasLandscape;
        public Placement portrait;
        public Placement landscape;
    }

    [Serializable]
    public class Normal
    {
        public string path;
        public Placement placement;
    }

    [Header("Leiaute em edição")]
    [Tooltip("Qual leiaute mostrar e editar na Scene.")]
    public Layout layout = Layout.Vertical;

    [Header("Proporção da tela em pixels")]
    public Vector2 verticalSize = new Vector2(1080f, 1920f);
    public Vector2 horizontalSize = new Vector2(1920f, 1080f);

    [HideInInspector] public List<Saved> saved = new List<Saved>();
    [HideInInspector] public List<Normal> normals = new List<Normal>();
    [HideInInspector] public bool editing;

    int shown = -1; // -1 nenhuma, 0 vertical, 1 horizontal

    void OnEnable()
    {
        if (Application.isPlaying && editing) EndEdit();
        shown = -1;
        if (!editing) Refresh();
    }

    void Update()
    {
        if (!editing) Refresh();
    }

    void Refresh()
    {
        int want = FollowDevice && Screen.width > Screen.height ? 1 : 0;
        if (want == shown) return;
        shown = want;
        Show(want == 1);
    }

    // ---------- elementos ----------

    // Quadros: no Canvas, as telas (filhos diretos); fora dele, o próprio objeto.
    List<RectTransform> Roots()
    {
        var l = new List<RectTransform>();
        if (GetComponent<Canvas>() != null)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var rt = transform.GetChild(i) as RectTransform;
                if (rt != null) l.Add(rt);
            }
        }
        else
        {
            var rt = transform as RectTransform;
            if (rt != null) l.Add(rt);
        }
        return l;
    }

    // Todos os elementos dentro dos quadros (sem os quadros), menos os que um Layout Group controla.
    List<RectTransform> Entries()
    {
        var l = new List<RectTransform>();
        foreach (var root in Roots())
        {
            foreach (var t in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (t == root) continue;
                if (t.parent != null && t.parent.GetComponent<LayoutGroup>() != null) continue;
                l.Add(t);
            }
        }
        return l;
    }

    string PathOf(Transform t)
    {
        string p = t.name;
        for (var x = t.parent; x != null && x != transform; x = x.parent) p = x.name + "/" + p;
        return p;
    }

    void Show(bool landscape)
    {
        foreach (var s in saved)
        {
            var t = transform.Find(s.path) as RectTransform;
            if (t == null) continue;
            if (landscape && s.hasLandscape) Apply(t, s.landscape);
            else if (s.hasPortrait) Apply(t, s.portrait);
        }
    }

    static void Apply(RectTransform t, Placement p)
    {
        t.anchorMin = p.anchorMin;
        t.anchorMax = p.anchorMax;
        t.pivot = p.pivot;
        t.anchoredPosition = p.anchoredPosition;
        t.sizeDelta = p.sizeDelta;
    }

    static Placement Read(RectTransform t)
    {
        return new Placement
        {
            anchorMin = t.anchorMin,
            anchorMax = t.anchorMax,
            pivot = t.pivot,
            anchoredPosition = t.anchoredPosition,
            sizeDelta = t.sizeDelta
        };
    }

    // Quantos elementos já têm posição salva (usado pelo painel do Inspector).
    public int CountSaved(bool landscape)
    {
        int n = 0;
        foreach (var x in saved)
        {
            if (landscape ? x.hasLandscape : x.hasPortrait) n++;
        }
        return n;
    }

    // ---------- edição na Scene ----------

    // Troca o leiaute em edição (usado pelo Inspector): Vertical = tela normal; Horizontal = tela com a proporção em pixels.
    public void SelectLayout(Layout l)
    {
        layout = l;
        if (l == Layout.Horizontal) BeginEdit();
        else
        {
            EndEdit();
            Show(false);
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    // Cada quadro vira um retângulo fixo, no centro, com o tamanho em pixels do leiaute escolhido, e mostra as posições salvas.
    public void BeginEdit()
    {
        var roots = Roots();
        if (roots.Count == 0) return;
#if UNITY_EDITOR
        UnityEditor.Undo.RegisterFullObjectHierarchyUndo(gameObject, "Editar leiaute");
#endif
        if (!editing)
        {
            normals.Clear();
            foreach (var r in roots) normals.Add(new Normal { path = r == transform ? "" : PathOf(r), placement = Read(r) });
            editing = true;
        }
        bool landscape = layout == Layout.Horizontal;
        foreach (var r in roots)
        {
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = Vector2.zero;
            r.sizeDelta = landscape ? horizontalSize : verticalSize;
        }
        Show(landscape);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    // Os quadros voltam a ocupar o Canvas inteiro (como estavam antes da edição).
    public void EndEdit()
    {
        if (!editing) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.Undo.RegisterFullObjectHierarchyUndo(gameObject, "Voltar ao normal");
#endif
        foreach (var n in normals)
        {
            var r = (string.IsNullOrEmpty(n.path) ? transform : transform.Find(n.path)) as RectTransform;
            if (r != null) Apply(r, n.placement);
        }
        normals.Clear();
        editing = false;
        shown = -1;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    static bool Same(Placement a, Placement b)
    {
        return (a.anchorMin - b.anchorMin).sqrMagnitude < 1e-6f && (a.anchorMax - b.anchorMax).sqrMagnitude < 1e-6f &&
               (a.pivot - b.pivot).sqrMagnitude < 1e-6f && (a.anchoredPosition - b.anchoredPosition).sqrMagnitude < 1e-4f &&
               (a.sizeDelta - b.sizeDelta).sqrMagnitude < 1e-4f;
    }

    // Algum elemento está diferente do que está salvo no leiaute escolhido? (sem posição salva, o vertical conta como não salvo)
    public bool HasUnsavedChanges()
    {
        bool landscape = layout == Layout.Horizontal;
        foreach (var t in Entries())
        {
            Saved s = saved.Find(x => x.path == PathOf(t));
            bool has = false;
            Placement baseline = default(Placement);
            if (s != null)
            {
                if (landscape && s.hasLandscape) { baseline = s.landscape; has = true; }
                else if (s.hasPortrait) { baseline = s.portrait; has = true; }
            }
            if (!has)
            {
                if (!landscape) return true;
                continue;
            }
            if (!Same(Read(t), baseline)) return true;
        }
        return false;
    }

    // Descarta o que não foi salvo: os elementos voltam às posições salvas do leiaute escolhido.
    public void RevertCurrent()
    {
#if UNITY_EDITOR
        foreach (var t in Entries()) UnityEditor.Undo.RecordObject(t, "Descartar posição");
#endif
        Show(layout == Layout.Horizontal);
    }

    // Guarda a posição atual de todos os elementos no leiaute escolhido.
    public void SaveCurrent()
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Salvar posição");
#endif
        bool landscape = layout == Layout.Horizontal;
        foreach (var t in Entries())
        {
            string path = PathOf(t);
            Saved s = saved.Find(x => x.path == path);
            if (s == null)
            {
                s = new Saved { path = path };
                saved.Add(s);
            }
            if (landscape)
            {
                s.landscape = Read(t);
                s.hasLandscape = true;
            }
            else
            {
                s.portrait = Read(t);
                s.hasPortrait = true;
            }
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}
