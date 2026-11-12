using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Aplica o tema da fase (cores, logo e textos) nos elementos de interface listados.
// A cor da esteira e os assets 3D são aplicados pelo próprio jogo a partir do LevelConfig.
public class LevelThemeApplier : MonoBehaviour
{
    public Graphic[] themeGraphics;
    public Graphic[] accentGraphics;
    public Image[] logoImages;
    public TMP_Text[] titleTexts;
    public TMP_Text[] introTexts;

    public void Apply(LevelConfig level)
    {
        Tint(themeGraphics, level.themeColor);
        Tint(accentGraphics, level.accentColor);

        if (logoImages != null)
        {
            foreach (var img in logoImages)
            {
                if (!img) continue;
                img.sprite = level.brandLogo;
                img.preserveAspect = true;
                img.enabled = level.brandLogo != null;
            }
        }
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

    static void Tint(Graphic[] graphics, Color c)
    {
        if (graphics == null) return;
        foreach (var g in graphics)
        {
            if (g) g.color = new Color(c.r, c.g, c.b, g.color.a);
        }
    }
}
