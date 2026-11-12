using UnityEngine;
using UnityEngine.UI;

// Cena 1: tela de apresentação com "Jogar" (vai ao scanner) e "Fases" (lista das fases).
public class MenuController : MonoBehaviour
{
    public LevelCatalog catalog;
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
