using UnityEngine;
using UnityEngine.UI;

// Cena 1: tela de apresentação com "Jogar" (vai ao scanner) e "Fases" (lista das fases).
public class MenuController : MonoBehaviour, IPhasePreview
{
    public LevelCatalog catalog;
    public LevelThemeApplier theme;
    public UiScreens ui;
    public GameObject mainScreen;
    public GameObject phasesScreen;
    public Button playButton;
    public Button phasesButton;
    public Button phasesBackButton;
    public Transform slotsContainer;
    public PhaseSlot slotPrefab;

    [Header("Testes (desligue antes do build final)")]
    public bool unlockAllForTesting;
    public bool resetProgressOnStart;

    void Start()
    {
        OrientationLayout.FollowDevice = catalog.settings != null && catalog.settings.followDeviceOrientation;
        if (catalog.settings != null) Sfx.Volume = catalog.settings.sfxVolume;
        if (catalog.settings != null) Sfx.HookButtons(catalog.settings.sfxButton);
        if (catalog.settings != null) MusicPlayer.Play(catalog.settings.musicOpening, catalog.settings.musicVolume, catalog.settings.musicFadeInSeconds, catalog.settings.musicFadeOutSeconds);
        if (theme != null) theme.ApplyNeutral(catalog);
        if (resetProgressOnStart) SaveSystem.ResetAll(catalog);
        if (unlockAllForTesting)
        {
            foreach (var l in catalog.levels)
            {
                if (l != null) SaveSystem.Unlock(l);
            }
        }

        playButton.onClick.AddListener(Nav.LoadScanner);
        phasesButton.onClick.AddListener(OpenPhases);
        phasesBackButton.onClick.AddListener(() => ui.Show(mainScreen));

        if (GameSession.OpenPhasesOnMenu)
        {
            GameSession.OpenPhasesOnMenu = false;
            OpenPhases();
        }
        else
        {
            ui.Show(mainScreen);
        }
    }

    // Pré-visualização na Scene: os quadros da lista de fases (com o progresso salvo). A tela de fases continua como está na Hierarchy.
    public void ApplyPreview(LevelConfig level)
    {
        if (Application.isPlaying) return;
        if (slotsContainer == null || slotPrefab == null || catalog == null || catalog.levels == null) return;
        ClearPreview();
        foreach (var l in catalog.levels)
        {
            if (l == null) continue;
            var slot = Instantiate(slotPrefab, slotsContainer);
            slot.gameObject.name = PreviewPrefix + l.displayName;
            PhasePreview.MarkTemporary(slot.gameObject);
            slot.Setup(l, null);
        }
    }

    public void ClearPreview()
    {
        if (Application.isPlaying || slotsContainer == null) return;
        for (int i = slotsContainer.childCount - 1; i >= 0; i--)
        {
            var c = slotsContainer.GetChild(i).gameObject;
            if (c.name.StartsWith(PreviewPrefix)) DestroyImmediate(c);
        }
    }

    const string PreviewPrefix = "[Pré-visualização] ";

    void OpenPhases()
    {
        foreach (Transform child in slotsContainer) Destroy(child.gameObject);
        foreach (var l in catalog.levels)
        {
            if (l == null) continue;
            var slot = Instantiate(slotPrefab, slotsContainer);
            slot.Setup(l, Nav.LoadGame);
        }
        ui.Show(phasesScreen);
    }
}
