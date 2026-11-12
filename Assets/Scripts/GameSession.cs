// Estado que atravessa as cenas enquanto o app está aberto (não é salvo em disco).
// O "onboarding já visto" é salvo no aparelho por SaveSystem.
public static class GameSession
{
    public static LevelConfig SelectedLevel;
    public static bool OpenPhasesOnMenu;
}
