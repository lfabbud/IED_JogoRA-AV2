using UnityEngine.SceneManagement;

// Navegação entre as três cenas. Os nomes das cenas precisam ser exatamente estes.
public static class Nav
{
    public const string MenuScene = "SCN_Menu";
    public const string ScannerScene = "SCN_Scanner";
    public const string GameScene = "SCN_Jogo";

    public static void LoadMenu()
    {
        SceneManager.LoadScene(MenuScene);
    }

    public static void LoadMenuPhases()
    {
        GameSession.OpenPhasesOnMenu = true;
        SceneManager.LoadScene(MenuScene);
    }

    public static void LoadScanner()
    {
        SceneManager.LoadScene(ScannerScene);
    }

    public static void LoadGame(LevelConfig level)
    {
        GameSession.SelectedLevel = level;
        SceneManager.LoadScene(GameScene);
    }
}
