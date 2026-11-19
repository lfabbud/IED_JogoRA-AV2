using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Aplica o tema da fase (cores, logo e textos) nos elementos de interface listados.
// Sem fase definida (Cena 2), aplica o tema neutro do Level Catalog.
// A cor da esteira, as roupas da Lu e os assets 3D são aplicados pelo próprio jogo a partir do LevelConfig.
public class LevelThemeApplier : MonoBehaviour
{
    public Graphic[] themeGraphics;
    public Graphic[] accentGraphics;
    public TMP_Text[] themeTexts;
    public Image[] logoImages;
    public TMP_Text[] titleTexts;
    public TMP_Text[] introTexts;
    public OnboardingPanel onboarding;

    public void Apply(LevelConfig level)
    {
        if (level == null) return;
        ApplyColors(level.themeColor, level.accentColor, level.textColor);
        SetLogo(level.brandLogo);
        if (titleTexts != null)
        {
            foreach (var t in titleTexts)
            {
                if (t) t.text = level.displayName;
            }
        }
        if (introTexts != null)
        {
            foreach (var t in introTexts)
            {
                if (t) t.text = level.introText;
            }
        }
    }

    // Tema neutro: cores e logo do Level Catalog; títulos e textos não mudam.
    public void ApplyNeutral(LevelCatalog catalog)
    {
        if (catalog == null) return;
        ApplyColors(catalog.neutralThemeColor, catalog.neutralAccentColor, catalog.neutralTextColor);
        SetLogo(catalog.neutralLogo);
    }

    void ApplyColors(Color theme, Color accent, Color text)
    {
        Tint(themeGraphics, theme);
        Tint(accentGraphics, accent);
        if (themeTexts != null)
        {
            foreach (var t in themeTexts)
            {
                if (t) t.color = new Color(text.r, text.g, text.b, t.color.a);
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
            if (g) g.color = new Color(c.r, c.g, c.b, g.color.a);
        }
    }
}
