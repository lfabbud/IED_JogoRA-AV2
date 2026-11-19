using UnityEngine;

// Lista das fases + a configuração global. Todas as cenas apontam para o mesmo catálogo.
// Criar em: Project > clique direito > Create > Lu Runner > Level Catalog
[CreateAssetMenu(menuName = "Lu Runner/Level Catalog", fileName = "LevelCatalog")]
public class LevelCatalog : ScriptableObject
{
    public GameSettings settings;
    public LevelConfig[] levels;

    [Header("Tema neutro (telas sem fase definida)")]
    public Color neutralThemeColor = new Color(0.08f, 0.08f, 0.12f, 1f);
    public Color neutralAccentColor = new Color(0.76f, 0.55f, 0.86f, 1f);
    public Color neutralTextColor = Color.white;
    public Sprite neutralLogo;

    public LevelConfig FindByPoster(string imageName)
    {
        if (levels == null) return null;
        foreach (var l in levels)
        {
            if (l != null && string.Equals(l.posterImageName, imageName, System.StringComparison.OrdinalIgnoreCase))
                return l;
        }
        return null;
    }
}
