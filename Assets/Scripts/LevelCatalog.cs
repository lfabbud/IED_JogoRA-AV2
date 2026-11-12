using UnityEngine;

// Lista das fases + a configuração global. Todas as cenas apontam para o mesmo catálogo.
// Criar em: Project > clique direito > Create > Lu Runner > Level Catalog
[CreateAssetMenu(menuName = "Lu Runner/Level Catalog", fileName = "LevelCatalog")]
public class LevelCatalog : ScriptableObject
{
    public GameSettings settings;
    public LevelConfig[] levels;

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
