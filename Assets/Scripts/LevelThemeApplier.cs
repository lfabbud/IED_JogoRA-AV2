using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Aplica o tema da fase (cores, logo e textos) nos elementos de interface listados.
// Sem fase definida (Cena 2), aplica o tema neutro do Level Catalog.
// A cor da esteira, as roupas da Lu e os assets 3D são aplicados pelo próprio jogo a partir do LevelConfig.
public class LevelThemeApplier : MonoBehaviour, IPhasePreview
{
    public Graphic[] themeGraphics;
    public Graphic[] accentGraphics;
    public TMP_Text[] themeTexts;
    public Image[] logoImages;
    public OnboardingPanel onboarding;
    public TMP_FontAsset buttonFont;
    public TMP_FontAsset infoFont;
    public TMP_FontAsset bodyFont;
    public TextStyle buttonStyle = new TextStyle();
    public TextStyle infoStyle = new TextStyle();
    public TextStyle bodyStyle = new TextStyle();

    [Header("Textos livres")]
    [Tooltip("Textos desta lista ficam fora da fonte e dos estilos (Button, Info e Body): tamanho, alinhamento, estilo e fonte são os que estiverem no próprio texto. A cor só muda se o texto também estiver em Theme Texts.")]
    public TMP_Text[] freeTexts;

    [System.Serializable]
    public class TextStyle
    {
        public bool apply;
        public float fontSize = 36;
        public HorizontalAlignmentOptions horizontal = HorizontalAlignmentOptions.Center;
        public VerticalAlignmentOptions vertical = VerticalAlignmentOptions.Middle;
        public FontStyles fontStyle = FontStyles.Normal;
        public float characterSpacing = 0;
        public float lineSpacing = 0;

        // Verdadeiro se algum valor do texto é diferente do estilo.
        public bool Differs(TMP_Text t)
        {
            if (!apply) return false;
            return t.enableAutoSizing
                || !Mathf.Approximately(t.fontSize, fontSize)
                || t.horizontalAlignment != horizontal
                || t.verticalAlignment != vertical
                || t.fontStyle != fontStyle
                || !Mathf.Approximately(t.characterSpacing, characterSpacing)
                || !Mathf.Approximately(t.lineSpacing, lineSpacing);
        }

        public void ApplyTo(TMP_Text t)
        {
            if (!apply) return;
            t.enableAutoSizing = false;
            t.fontSize = fontSize;
            t.horizontalAlignment = horizontal;
            t.verticalAlignment = vertical;
            t.fontStyle = fontStyle;
            t.characterSpacing = characterSpacing;
            t.lineSpacing = lineSpacing;
        }
    }

    // Fonte dos botões para os textos criados por script (lista de fases, caixa Show boxes).
    public static TMP_FontAsset ButtonFont;

    public void Apply(LevelConfig level)
    {
        ApplyFonts();
        if (level == null) return;
        ApplyColors(level.themeColor, level.accentColor, level.textColor);
        SetLogo(level.brandLogo);
        SetText(gameObject.scene, "IntroText", level.Format(level.introText, 0, 0));
    }

    // Tema neutro: cores e logo do Level Catalog; títulos e textos não mudam.
    public void ApplyNeutral(LevelCatalog catalog)
    {
        ApplyFonts();
        if (catalog == null) return;
        ApplyColors(catalog.neutralThemeColor, catalog.neutralAccentColor, catalog.neutralTextColor);
        SetLogo(catalog.neutralLogo);
    }

    // No editor, qualquer alteração nos campos deste script aplica a fonte e o estilo na hora, sem apertar Play.
#if UNITY_EDITOR
    bool pendingApply;

    void OnValidate()
    {
        if (pendingApply) return;
        pendingApply = true;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            pendingApply = false;
            if (this) ApplyFonts();
        };
    }
#endif

    // Pré-visualização na Scene: cores, logo, títulos e fontes da fase escolhida (null = tema neutro).
    public void ApplyPreview(LevelConfig level)
    {
        if (Application.isPlaying) return;
        if (level != null) Apply(level);
        else ApplyNeutral(PhasePreview.Catalog());
    }

    public void ClearPreview() { }

    // Escreve o texto em todos os objetos de texto da cena com este nome, inclusive nos desativados.
    public static void SetText(UnityEngine.SceneManagement.Scene scene, string objectName, string text)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.gameObject.name != objectName || t.text == text) continue;
                Rec(t);
                t.text = text;
            }
        }
    }

    static void Rec(Object o)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && o != null) UnityEditor.Undo.RecordObject(o, "Level Theme Applier");
#endif
    }

    [ContextMenu("Aplicar estilos agora")]
    void ApplyNow()
    {
        ApplyFonts();
    }

    // Aplica a fonte e o estilo (tamanho, alinhamento, estilo, espaçamento) em todos os textos da cena, inclusive os desativados:
    // texto dentro de Button ou Toggle = fonte dos botões;
    // texto com quebra de linha ligada (Wrapping) = texto corrido;
    // demais textos = informações em tela.
    void ApplyFonts()
    {
        ButtonFont = buttonFont;
        var free = new System.Collections.Generic.HashSet<TMP_Text>();
        if (freeTexts != null)
        {
            foreach (var f in freeTexts)
            {
                if (f) free.Add(f);
            }
        }
        var roots = gameObject.scene.GetRootGameObjects();
        foreach (var root in roots)
        {
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!t || free.Contains(t)) continue;
                TMP_FontAsset font;
                TextStyle style;
                if (t.GetComponentInParent<Button>(true) || t.GetComponentInParent<Toggle>(true)) { font = buttonFont; style = buttonStyle; }
                else if (t.textWrappingMode != TextWrappingModes.NoWrap) { font = bodyFont; style = bodyStyle; }
                else { font = infoFont; style = infoStyle; }
                bool changeFont = font && t.font != font;
                bool changeStyle = style != null && style.Differs(t);
                if (!changeFont && !changeStyle) continue;
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.Undo.RecordObject(t, "Level Theme Applier");
#endif
                if (changeFont) t.font = font;
                if (changeStyle) style.ApplyTo(t);
            }
        }
    }

    void ApplyColors(Color theme, Color accent, Color text)
    {
        Tint(themeGraphics, theme);
        Tint(accentGraphics, accent);
        if (themeTexts != null)
        {
            foreach (var t in themeTexts)
            {
                if (t) { var n = new Color(text.r, text.g, text.b, t.color.a); if (t.color != n) { Rec(t); t.color = n; } }
            }
        }
        if (onboarding) onboarding.SetDotColors(accent);
    }

    void SetLogo(Sprite logo)
    {
        if (logoImages == null) return;
        foreach (var img in logoImages)
        {
            if (!img) continue;
            Rec(img);
            img.sprite = logo;
            img.preserveAspect = true;
            img.enabled = logo != null;
        }
    }

    static void Tint(Graphic[] graphics, Color c)
    {
        if (graphics == null) return;
        foreach (var g in graphics)
        {
            if (g) { var n = new Color(c.r, c.g, c.b, g.color.a); if (g.color != n) { Rec(g); g.color = n; } }
        }
    }
}
