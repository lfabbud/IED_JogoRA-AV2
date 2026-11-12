using UnityEngine;

// Progresso salvo no aparelho (PlayerPrefs): fases desbloqueadas, melhor pontuação (moedas), conclusão
// e se o onboarding de cada cena já foi visto (só aparece na primeira vez).
public static class SaveSystem
{
    public const string ScannerOnboarding = "scanner";
    public const string GameOnboarding = "game";

    static string Key(string kind, string id)
    {
        return "luRunner_" + kind + "_" + id;
    }

    public static bool OnboardingSeen(string scene)
    {
        return PlayerPrefs.GetInt(Key("onboarding", scene), 0) == 1;
    }

    public static void MarkOnboardingSeen(string scene)
    {
        PlayerPrefs.SetInt(Key("onboarding", scene), 1);
        PlayerPrefs.Save();
    }

    // Cor escolhida no vestiário para a peça de roupa "part" (índice em GameSettings.wardrobeColors); -1 = cor original.
    public static int GetWardrobe(int part)
    {
        return PlayerPrefs.GetInt(Key("wardrobe", part.ToString()), -1);
    }

    public static void SetWardrobe(int part, int colorIndex)
    {
        PlayerPrefs.SetInt(Key("wardrobe", part.ToString()), colorIndex);
        PlayerPrefs.Save();
    }

    public static bool IsUnlocked(LevelConfig level)
    {
        return PlayerPrefs.GetInt(Key("unlocked", level.id), 0) == 1;
    }

    public static void Unlock(LevelConfig level)
    {
        PlayerPrefs.SetInt(Key("unlocked", level.id), 1);
        PlayerPrefs.Save();
    }

    public static bool HasPlayed(LevelConfig level)
    {
        return PlayerPrefs.HasKey(Key("best", level.id));
    }

    public static int BestCoins(LevelConfig level)
    {
        return PlayerPrefs.GetInt(Key("best", level.id), 0);
    }

    public static bool IsCompleted(LevelConfig level)
    {
        return PlayerPrefs.GetInt(Key("done", level.id), 0) == 1;
    }

    public static void SaveResult(LevelConfig level, int coins, bool completed)
    {
        if (coins > BestCoins(level) || !HasPlayed(level))
            PlayerPrefs.SetInt(Key("best", level.id), coins);
        if (completed)
            PlayerPrefs.SetInt(Key("done", level.id), 1);
        PlayerPrefs.Save();
    }

    public static void ResetAll(LevelCatalog catalog)
    {
        PlayerPrefs.DeleteKey(Key("onboarding", ScannerOnboarding));
        PlayerPrefs.DeleteKey(Key("onboarding", GameOnboarding));
        for (int i = 0; i < 8; i++) PlayerPrefs.DeleteKey(Key("wardrobe", i.ToString()));
        if (catalog != null && catalog.levels != null)
        {
            foreach (var l in catalog.levels)
            {
                if (l == null) continue;
                PlayerPrefs.DeleteKey(Key("unlocked", l.id));
                PlayerPrefs.DeleteKey(Key("best", l.id));
                PlayerPrefs.DeleteKey(Key("done", l.id));
            }
        }
        PlayerPrefs.Save();
    }
}
