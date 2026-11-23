using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Cena 2: onboarding (só na primeira vez), depois câmera aberta lendo os posters (Reference Image Library).
// Cada poster reconhecido desbloqueia a fase e mostra os elementos 3D saindo do poster.
// Enquanto nenhum poster é reconhecido, aparece no centro da tela a imagem e o texto de "procurando".
// Um botão no HUD permite rever o onboarding a qualquer momento.
public class PosterScanner : MonoBehaviour, IPhasePreview
{
    public LevelCatalog catalog;
    public ARTrackedImageManager imageManager;
    public UiScreens ui;
    public OnboardingPanel onboarding;
    public LevelThemeApplier theme;
    public GameObject onboardingScreen;
    public GameObject scanScreen;
    public TMP_Text hintText;
    public Button backButton;

    [Header("Procurando (centro da tela) e rever onboarding")]
    public GameObject searchingGroup;
    public Button reviewOnboardingButton;

    [Header("Aviso de fase desbloqueada")]
    public GameObject unlockedBanner;
    public TMP_Text unlockedTitleText;
    public Image unlockedLogo;
    public Button playUnlockedButton;

    readonly Dictionary<TrackableId, GameObject> spawned = new Dictionary<TrackableId, GameObject>();
    readonly HashSet<TrackableId> tracking = new HashSet<TrackableId>();
    LevelConfig lastLevel;
    bool scanning;

    void Start()
    {
        OrientationLayout.FollowDevice = catalog.settings != null && catalog.settings.followDeviceOrientation;
        if (catalog.settings != null) Sfx.Volume = catalog.settings.sfxVolume;
        if (catalog.settings != null) Sfx.HookButtons(catalog.settings.sfxButton);
        if (catalog.settings != null) MusicPlayer.Play(catalog.settings.musicOpening, catalog.settings.musicVolume, catalog.settings.musicFadeInSeconds, catalog.settings.musicFadeOutSeconds);
        if (theme != null) theme.ApplyNeutral(catalog);
        imageManager.enabled = false;
        imageManager.trackablesChanged.AddListener(OnTrackablesChanged);

        backButton.onClick.AddListener(Nav.LoadMenu);
        playUnlockedButton.onClick.AddListener(() =>
        {
            if (lastLevel != null) Nav.LoadGame(lastLevel);
        });
        if (reviewOnboardingButton) reviewOnboardingButton.onClick.AddListener(() => ui.Show(onboardingScreen));
        onboarding.Finished += OnOnboardingFinished;

        if (SaveSystem.OnboardingSeen(SaveSystem.ScannerOnboarding)) StartScanning();
        else ui.Show(onboardingScreen);
    }

    void OnDestroy()
    {
        if (imageManager != null) imageManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
    }

    void OnOnboardingFinished()
    {
        SaveSystem.MarkOnboardingSeen(SaveSystem.ScannerOnboarding);
        if (scanning) ui.Show(scanScreen);
        else StartScanning();
    }

    void StartScanning()
    {
        scanning = true;
        ui.Show(scanScreen);
        unlockedBanner.SetActive(false);
        if (hintText) hintText.text = "Aponte a câmera para um poster";
        imageManager.enabled = true;
        UpdateSearching();
    }

    void UpdateSearching()
    {
        if (searchingGroup) searchingGroup.SetActive(scanning && tracking.Count == 0 && !unlockedBanner.activeSelf);
    }

    void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
    {
        foreach (var image in args.added) Handle(image);
        foreach (var image in args.updated) Handle(image);
        foreach (var removed in args.removed)
        {
            GameObject go;
            if (spawned.TryGetValue(removed.Key, out go))
            {
                if (go != null) Destroy(go);
                spawned.Remove(removed.Key);
            }
            tracking.Remove(removed.Key);
        }
        UpdateSearching();
    }

    void Handle(ARTrackedImage image)
    {
        var level = catalog.FindByPoster(image.referenceImage.name);
        if (level == null) return;

        bool isTracking = image.trackingState == TrackingState.Tracking;
        if (isTracking) tracking.Add(image.trackableId);
        else tracking.Remove(image.trackableId);

        GameObject go;
        if (isTracking && !spawned.TryGetValue(image.trackableId, out go))
        {
            OnPosterFound(level);
            go = level.posterUnlockPrefab != null ? Instantiate(level.posterUnlockPrefab, image.transform) : new GameObject("PosterVazio");
            if (level.posterUnlockPrefab == null) go.transform.SetParent(image.transform, false);
            spawned[image.trackableId] = go;
        }

        if (spawned.TryGetValue(image.trackableId, out go) && go != null)
            go.SetActive(isTracking);
    }

    // Pré-visualização na Scene: título e logo da fase desbloqueada. O banner continua como está na Hierarchy.
    public void ApplyPreview(LevelConfig level)
    {
        if (Application.isPlaying || level == null) return;
        if (unlockedTitleText) unlockedTitleText.text = level.Format(level.scannerUnlockedMessage, 0, 0);
        if (unlockedLogo)
        {
            unlockedLogo.sprite = level.brandLogo;
            unlockedLogo.preserveAspect = true;
            unlockedLogo.enabled = level.brandLogo != null;
        }
    }

    public void ClearPreview() { }

    void OnPosterFound(LevelConfig level)
    {
        bool firstTime = !SaveSystem.IsUnlocked(level);
        if (firstTime)
        {
            SaveSystem.Unlock(level);
            Sfx.Play(catalog.settings.sfxUnlock);
        }
        lastLevel = level;
        unlockedTitleText.text = level.Format(firstTime ? level.scannerUnlockedMessage : level.scannerRevisitMessage, 0, 0);
        if (unlockedLogo)
        {
            unlockedLogo.sprite = level.brandLogo;
            unlockedLogo.preserveAspect = true;
            unlockedLogo.enabled = level.brandLogo != null;
        }
        unlockedBanner.SetActive(true);
        UpdateSearching();
    }
}
