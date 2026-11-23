using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using TMPro;

// Cena de jogo (única, parametrizada pela fase escolhida).
// Fluxo: introdução > onboarding (só na 1ª vez) > posicionar a esteira > esteira aterrissa (TrackLanding) > Lu aquecendo, esperando o toque na tela >
// HUD desliza para dentro > contagem regressiva > corrida > resultado. "Tentar de novo" recomeça no mesmo lugar.
public class RunnerGame : MonoBehaviour
{
    enum State { Intro, Placing, Landing, Ready, Countdown, Running, Celebrating, GameOver }

    [Header("Dados")]
    public LevelCatalog catalog;
    public LevelConfig testLevel;
    public GameObject trackPrefab;

    [Header("Componentes")]
    public TrackPlacer placer;
    public LevelThemeApplier theme;
    public ResultScreen results;
    public HudController hud;
    public OnboardingPanel onboarding;
    public UiScreens ui;

    [Header("Telas")]
    public GameObject introScreen;
    public GameObject placementScreen;
    public GameObject hudScreen;
    public GameObject pauseScreen;

    [Header("Botões")]
    public Button playButton;
    public Button placeButton;
    public Button resumeButton;
    public Button restartButton;
    public Button menuButton;
    [Tooltip("Opcional: botão de trocar de fase na tela de pause.")]
    public Button pausePhasesButton;
    public Button placementReviewButton;
    public TMP_Text placementHint;

    [Header("Tela de posicionar: área da base da esteira")]
    public Graphic footprintImage;
    public Color footprintIdle = new Color(1f, 1f, 1f, 0.55f);
    public Color footprintReady = new Color(0.4f, 1f, 0.5f, 0.9f);

    State state;
    LevelConfig level;
    GameSettings settings;
    TrackRoot track;
    TrackLanding landing;
    bool anchorAfterLanding;
    // Moedas já contadas nesta partida: cada moeda soma uma vez só, mesmo que o contato seja avisado mais de uma vez.
    readonly HashSet<BeltItem> countedCoins = new HashSet<BeltItem>();
    bool stepsOn;
    int lastHitIndex = -1;
    PhasePicker picker;
    ShowBoxesToggle boxesToggle;
    bool showBoxes;
    LaneController lu;
    Renderer[] luRenderers;
    GameObject vortex;

    int lives;
    int coins;
    float elapsed;
    float invincibleLeft;
    float readyTimer;
    float hitAnimLeft;
    int lastAlertSecond;
    bool jumpAnimActive;
    bool reviewingOnboarding;

    void Start()
    {
        if (catalog != null && catalog.settings != null) OrientationLayout.FollowDevice = catalog.settings.followDeviceOrientation;
        level = GameSession.SelectedLevel;
        if (level == null) level = testLevel;
        if (level == null && catalog.levels != null && catalog.levels.Length > 0) level = catalog.levels[0];
        if (level == null)
        {
            Debug.LogError("Nenhuma fase definida: escolha uma fase pelo menu ou preencha Test Level.");
            return;
        }
        GameSession.SelectedLevel = level;

        // Cópia dos dados: valores alterados em jogo valem só para esta cópia, nunca para o asset do projeto.
        settings = Instantiate(catalog.settings);
        Sfx.Volume = settings.sfxVolume;
        Sfx.HookButtons(settings.sfxButton, hud != null && hud.controlsRoot != null ? hud.controlsRoot.transform : null);
        MusicPlayer.Play(settings.musicGame, settings.musicVolume, settings.musicFadeInSeconds, settings.musicFadeOutSeconds);

        theme.Apply(level);

        playButton.onClick.AddListener(OnPlayPressed);
        placeButton.onClick.AddListener(OnPlacePressed);
        resumeButton.onClick.AddListener(Resume);
        restartButton.onClick.AddListener(RestartInPlace);
        menuButton.onClick.AddListener(ToMenu);
        if (placementReviewButton) placementReviewButton.onClick.AddListener(ReviewOnboarding);

        hud.LeftPressed += () => { if (lu != null) lu.MoveLeft(); };
        hud.RightPressed += () => { if (lu != null) lu.MoveRight(); };
        hud.JumpPressed += () => { if (lu != null) lu.Jump(); };
        hud.PausePressed += Pause;

        onboarding.Finished += OnOnboardingFinished;
        results.RetryRequested += RestartInPlace;

        boxesToggle = gameObject.AddComponent<ShowBoxesToggle>();
        boxesToggle.Changed += on =>
        {
            showBoxes = on;
            if (track != null) track.ShowBoxes = on;
        };
        picker = gameObject.AddComponent<PhasePicker>();
        picker.Chosen += SwitchLevel;
        results.PhasesRequested += ShowPhases;
        if (pausePhasesButton) pausePhasesButton.onClick.AddListener(ShowPhases);

        pauseScreen.SetActive(false);
        ui.Show(introScreen);
        state = State.Intro;
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    // ---------- onboarding e posicionamento ----------

    void OnPlayPressed()
    {
        if (SaveSystem.OnboardingSeen(SaveSystem.GameOnboarding)) BeginPlacement();
        else ui.Show(onboarding.gameObject);
    }

    // Botão no HUD da tela de posicionar: rever o onboarding.
    void ReviewOnboarding()
    {
        if (state != State.Placing) return;
        reviewingOnboarding = true;
        ui.Show(onboarding.gameObject);
    }

    void OnOnboardingFinished()
    {
        SaveSystem.MarkOnboardingSeen(SaveSystem.GameOnboarding);
        if (reviewingOnboarding)
        {
            reviewingOnboarding = false;
            ui.Show(placementScreen);
        }
        else BeginPlacement();
    }

    void BeginPlacement()
    {
        state = State.Placing;
        ui.Show(placementScreen);
        placeButton.interactable = false;
        placer.onScreenTest = settings.testOnScreen;
        placer.Begin();
    }

    void OnPlacePressed()
    {
        if (state != State.Placing || !placer.HasPose) return;

        Pose pose = placer.CurrentPose;
        Vector3 forward = Vector3.ProjectOnPlane(placer.arCamera.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

        var go = Instantiate(trackPrefab, pose.position, Quaternion.LookRotation(forward.normalized, Vector3.up));
        go.transform.localScale = Vector3.one * settings.arScale;
        if (settings.testOnScreen && settings.testTiltDegrees > 0f)
            go.transform.rotation = Quaternion.AngleAxis(-settings.testTiltDegrees, go.transform.right) * go.transform.rotation;
        // Aterrissagem: a esteira cai sobre o marcador. O ARAnchor só entra depois da queda, para o AR não brigar com a animação.
        landing = go.GetComponent<TrackLanding>();
        if (landing == null) landing = go.AddComponent<TrackLanding>();
        bool wantAnchor = settings.useAnchor && !settings.testOnScreen;
        if (wantAnchor && !landing.enableLanding) go.AddComponent<ARAnchor>();
        anchorAfterLanding = wantAnchor && landing.enableLanding;
        placer.End();

        track = go.GetComponent<TrackRoot>();
        lu = track.lu;
        lu.Jumped += OnJumped;
        lu.DoubleJumped += OnDoubleJumped;
        lu.LaneChanged += dir => Sfx.Play(dir < 0 ? settings.sfxLaneLeft : settings.sfxLaneRight);
        lu.ObstacleTouched += OnObstacleTouched;
        lu.CoinTouched += OnCoinTouched;
        luRenderers = lu.GetComponentsInChildren<Renderer>(true);

        BeginRun();
    }

    // ---------- início da corrida ----------

    // Entre o posicionamento e o aquecimento: pinta a meia, o short e o boné da Lu com as cores da fase.
    void BeginRun()
    {
        track.Init(settings, level);
        if (settings.testOnScreen) CenterTrackVertically();
        track.ShowBoxes = showBoxes;
        lu.Init(settings);
        SetLuVisible(true);

        var wardrobe = lu.GetComponentInChildren<LuWardrobe>(true);
        if (wardrobe != null) wardrobe.ApplyLevel(level);

        if (landing != null && landing.enableLanding) StartCoroutine(LandingRoutine());
        else PrepareRun();
    }

    // Esteira e Lu (já vestida) caem sobre o marcador; ao terminar, entra o ARAnchor e começa o aquecimento.
    IEnumerator LandingRoutine()
    {
        state = State.Landing;
        placeButton.interactable = false;
        if (placementHint) placementHint.text = "";

        Vector3 ground = track.transform.position;
        landing.Begin(ground);
        float t = 0f;
        while (!landing.Finished(t))
        {
            t += Time.deltaTime;
            landing.Step(t);
            yield return null;
        }
        landing.Step(landing.TotalSeconds);
        landing.End();

        if (anchorAfterLanding && track.GetComponent<ARAnchor>() == null) track.gameObject.AddComponent<ARAnchor>();
        PrepareRun();
    }

    // ---------- preparar e começar a partida ----------

    // Deixa tudo no começo, com a esteira no mesmo lugar: Lu aquecendo, esperando o toque na tela.
    void PrepareRun()
    {
        StopAllCoroutines();
        Sfx.StopTimed();
        StopSteps();
        countedCoins.Clear();
        DestroyVortex();

        lives = settings.startingLives;
        coins = 0;
        elapsed = 0f;
        invincibleLeft = 0f;
        hitAnimLeft = 0f;
        lastAlertSecond = -1;
        jumpAnimActive = false;

        track.Init(settings, level);
        track.ShowBoxes = showBoxes;
        lu.Init(settings);
        SetLuVisible(true);
        PlayState("Aquecimento");

        ui.Show(hudScreen);
        hud.ConfigureTimer(settings);
        hud.SetControlsVisible(false);
        hud.SetLives(lives);
        hud.SetCoins(0);
        hud.SetTimer(settings.runDurationSeconds);
        hud.SetCenter(settings.readyMessage);

        readyTimer = 0f;
        state = State.Ready;
    }

    IEnumerator CountdownRoutine()
    {
        state = State.Countdown;
        hud.SetCenter("");

        PlayState("Preparo");
        // Com um som único da contagem (Sfx Countdown), ele toca uma vez junto com o primeiro número e não há bips.
        bool wholeSound = settings.sfxCountdown != null;
        if (wholeSound) Sfx.Play(settings.sfxCountdown);
        for (int i = settings.countdownFrom; i > 0; i--)
        {
            hud.SetCenter(CountdownText(i));
            if (!wholeSound) Sfx.Play(settings.sfxBeep);
            yield return new WaitForSeconds(1f);
        }

        hud.SetCenter(settings.goMessage);
        Sfx.Play(settings.sfxStartSiren);
        PlayState("Correr");
        hud.SetControlsVisible(true);
        lu.CanControl = true;
        lu.SensorActive = true;
        state = State.Running;
        StartSteps();

        yield return new WaitForSeconds(0.7f);
        if (state == State.Running) hud.SetCenter("");
    }

    void Update()
    {
        // O checkbox "Show boxes" só aparece antes de a corrida começar.
        if (boxesToggle != null) boxesToggle.SetVisible(state == State.Intro || state == State.Placing || state == State.Ready);

        if (state == State.Placing)
        {
            placeButton.interactable = placer.HasPose;
            if (placementHint)
                placementHint.text = placer.HasPose
                    ? (settings.testOnScreen ? settings.placementTestMessage : settings.placementFoundMessage)
                    : settings.placementSearchingMessage;
            if (footprintImage) footprintImage.color = placer.HasPose ? footprintReady : footprintIdle;
        }
        else if (state == State.Ready)
        {
            readyTimer += Time.deltaTime;
            if (readyTimer > 0.4f && TapThisFrame()) StartCoroutine(CountdownRoutine());
        }
        else if (state == State.Running)
        {
            RunTick();
        }
        else if (state == State.Celebrating && track != null)
        {
            track.Tick(Time.deltaTime, elapsed, false);
        }
    }

    bool TapThisFrame()
    {
        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return false;
        if (boxesToggle != null && boxesToggle.BlocksTap(pointer.position.ReadValue())) return false;
        return true;
    }

    void RunTick()
    {
        float dt = Time.deltaTime;
        elapsed += dt;

        bool spawning = elapsed < settings.runDurationSeconds;
        if (!spawning && !track.FinishSpawned) track.SpawnFinish();
        track.Tick(dt, elapsed, spawning);

        // Passadas: tocam correndo e param enquanto a Lu está no ar.
        if (lu.IsAirborne == stepsOn)
        {
            if (stepsOn) StopSteps();
            else StartSteps();
        }

        float left = Mathf.Max(0f, settings.runDurationSeconds - elapsed);
        hud.SetTimer(left);
        UpdateEndAlert(left);

        if (invincibleLeft > 0f) invincibleLeft -= dt;

        if (jumpAnimActive && !lu.IsAirborne)
        {
            jumpAnimActive = false;
            PlayState("Correr");
        }
        if (hitAnimLeft > 0f)
        {
            hitAnimLeft -= dt;
            if (hitAnimLeft <= 0f && !lu.IsAirborne) PlayState("Correr");
        }

        if (state == State.Running && track.FinishCrossed) StartCoroutine(Win());
    }

    // Reta final: o cronômetro pisca e muda de cor, e a sirene toca uma vez por segundo que falta.
    void UpdateEndAlert(float left)
    {
        bool alert = settings.alertSeconds > 0f && left > 0f && left <= settings.alertSeconds;
        hud.SetTimerAlert(alert);
        int sec = Mathf.CeilToInt(left);
        if (alert && sec != lastAlertSecond)
        {
            lastAlertSecond = sec;
            Sfx.Play(settings.sfxEndAlert);
        }
    }

    // ---------- contatos ----------

    void OnObstacleTouched(BeltItem item)
    {
        if (state != State.Running || invincibleLeft > 0f) return;
        OnHit();
    }

    void OnCoinTouched(BeltItem item)
    {
        if (state != State.Running || item == null || !item.gameObject.activeInHierarchy) return;
        if (!countedCoins.Add(item)) return;
        CoinPickupEffect.Play(track, item, settings);
        coins++;
        hud.SetCoins(coins);
        hud.PulseCoins();
        Sfx.Play(settings.sfxCoin);
    }

    void OnHit()
    {
        lives--;
        hud.SetLives(lives);
        track.ClearObstaclesAhead(settings.clearAheadDistance);

        if (lives <= 0)
        {
            Sfx.Play(PickHitSound());
            StartCoroutine(Lose());
            return;
        }
        invincibleLeft = settings.invincibleSeconds;
        Sfx.Play(PickHitSound());
        Sfx.PlayFor(settings.sfxDizzy, settings.invincibleSeconds);
        PlayState("Esbarrar", true);
        hitAnimLeft = settings.hitAnimationSeconds;
        StartCoroutine(HitEffect());
    }

    // Um tempo só (invincibleSeconds): Lu invencível, pisca, o redemoinho gira sobre a cabeça e o som toca.
    IEnumerator HitEffect()
    {
        DestroyVortex();
        if (level.vortexPrefab != null && track.headAnchor != null)
            vortex = Instantiate(level.vortexPrefab, track.headAnchor);

        float t = 0f;
        float blink = 0f;
        bool visible = true;
        var fade = lu.GetComponent<LuFade>();
        if (fade == null) fade = lu.gameObject.AddComponent<LuFade>();
        bool smooth = settings.blinkSmoothness > 0.001f && fade.Prepare();
        while (t < settings.invincibleSeconds && state == State.Running)
        {
            float dt = Time.deltaTime;
            t += dt;
            if (smooth)
            {
                fade.SetAlpha(BlinkAlpha(t));
            }
            else
            {
                blink += dt;
                if (blink >= BlinkHalf())
                {
                    blink = 0f;
                    visible = !visible;
                    SetLuVisible(visible);
                }
            }
            if (vortex != null) vortex.transform.Rotate(0f, settings.vortexSpinDegreesPerSecond * dt, 0f, Space.Self);
            yield return null;
        }
        SetLuVisible(true);
        DestroyVortex();
    }

    // Metade de uma piscada: com N piscadas por segundo, some por 1/(2N) s e aparece por 1/(2N) s.
    float BlinkHalf()
    {
        return 0.5f / Mathf.Max(0.1f, settings.blinksPerSecond);
    }

    // Opacidade da Lu na piscada: mistura a onda seca (liga/desliga) com uma onda suave, conforme blinkSmoothness.
    float BlinkAlpha(float t)
    {
        float iv = BlinkHalf();
        float square = (Mathf.FloorToInt(t / iv) % 2 == 0) ? 1f : 0f;
        float smooth = 0.5f + 0.5f * Mathf.Cos(Mathf.PI * t / iv);
        return Mathf.Lerp(square, smooth, Mathf.Clamp01(settings.blinkSmoothness));
    }

    void DestroyVortex()
    {
        if (vortex != null) Destroy(vortex);
        vortex = null;
    }

    // ---------- fim da partida ----------

    IEnumerator Lose()
    {
        state = State.GameOver;
        lu.CanControl = false;
        lu.SensorActive = false;
        track.StopBelt();
        SetLuVisible(true);
        jumpAnimActive = false;
        hitAnimLeft = 0f;
        if (HasLuState("Derrota")) PlayState("Derrota");
        else PlayState("Esbarrar", true);
        DestroyVortex();
        Sfx.StopTimed();
        hud.SetTimerAlert(false);
        hud.SetControlsVisible(false);
        Sfx.Play(settings.sfxLose);
        StopSteps();
        yield return new WaitForSeconds(settings.gameOverDelaySeconds);

        SaveSystem.SaveResult(level, coins, false);
        results.ShowGameOver(level, coins);
    }

    // Ao cruzar a linha de chegada: a esteira desacelera até parar, Lu comemora, confetes e tema de vitória.
    IEnumerator Win()
    {
        state = State.Celebrating;
        lu.CanControl = false;
        lu.SensorActive = false;
        track.DecelerateAndStop(settings.stopDecelSeconds);
        SetLuVisible(true);
        DestroyVortex();
        Sfx.StopTimed();
        StopSteps();
        hud.SetTimerAlert(false);
        hud.SetControlsVisible(false);
        PlayState("Comemorar");
        Sfx.Play(settings.sfxVictoryTheme);
        track.PlayConfetti(level.AllColors());
        yield return new WaitForSeconds(settings.celebrationSeconds);

        SaveSystem.SaveResult(level, coins, true);
        results.ShowVictory(level, coins, settings.GetTier(coins));
    }

    // ---------- textos e passadas ----------

    // Texto de cada passo da contagem: o {n} vira o número da vez.
    string CountdownText(int n)
    {
        string template = string.IsNullOrEmpty(settings.countdownMessage) ? "{n}" : settings.countdownMessage;
        return template.Replace("{n}", n.ToString());
    }

    // Sorteia um dos sons de batida preenchidos, sem repetir o anterior (se houver mais de um).
    AudioClip PickHitSound()
    {
        var list = settings.sfxHits;
        if (list == null) return null;
        var options = new List<int>();
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] != null) options.Add(i);
        }
        if (options.Count == 0) return null;
        if (options.Count > 1) options.Remove(lastHitIndex);
        lastHitIndex = options[Random.Range(0, options.Count)];
        return list[lastHitIndex];
    }

    void StartSteps()
    {
        stepsOn = true;
        Sfx.StartSteps(settings.sfxFootsteps, settings.footstepsVolume);
    }

    void StopSteps()
    {
        stepsOn = false;
        Sfx.StopSteps();
    }

    // ---------- animação, pulo, pause ----------

    void OnJumped()
    {
        PlayState("Pular");
        jumpAnimActive = true;
        Sfx.Play(settings.sfxJump);
    }

    void OnDoubleJumped()
    {
        PlayState("PularDuplo", true);
        jumpAnimActive = true;
        Sfx.Play(settings.sfxJump);
    }

    // Estados opcionais (PularDuplo, Esbarrar): se não existirem no Animator, nada acontece.
    bool HasLuState(string stateName)
    {
        if (track == null || track.luAnimator == null) return false;
        var anim = track.luAnimator;
        return anim.HasState(0, Animator.StringToHash(stateName)) || anim.HasState(0, Animator.StringToHash("Base Layer." + stateName));
    }

    void PlayState(string stateName, bool optional = false)
    {
        if (track == null || track.luAnimator == null) return;
        var anim = track.luAnimator;
        int hash = Animator.StringToHash(stateName);
        int fullHash = Animator.StringToHash("Base Layer." + stateName);
        if (!anim.HasState(0, hash) && !anim.HasState(0, fullHash))
        {
            if (!optional) Debug.LogWarning("Estado do Animator não encontrado: " + stateName);
            return;
        }
        anim.CrossFadeInFixedTime(hash, 0.15f);
    }

    void SetLuVisible(bool visible)
    {
        if (luRenderers == null) return;
        foreach (var r in luRenderers)
        {
            if (r != null) r.enabled = visible;
        }
        if (visible && lu != null)
        {
            var fade = lu.GetComponent<LuFade>();
            if (fade != null) fade.SetAlpha(1f);
        }
    }

    void Pause()
    {
        if (state != State.Running) return;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        pauseScreen.SetActive(true);
    }

    void Resume()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        pauseScreen.SetActive(false);
    }

    // "Tentar de novo" e "Reiniciar" (pause): a esteira fica no mesmo lugar e a partida recomeça com a Lu aquecendo.
    void RestartInPlace()
    {
        Resume();
        if (track == null) return;
        PrepareRun();
    }

    void ToMenu()
    {
        Resume();
        Nav.LoadMenu();
    }

    // Teste na tela: só a altura muda. O centro da esteira sobe ou desce até ficar no centro vertical da tela;
    // a distância e o alinhamento lateral continuam os do posicionador.
    void CenterTrackVertically()
    {
        var cam = placer.arCamera;
        if (cam == null) return;
        for (int pass = 0; pass < 3; pass++)
        {
            Bounds b = new Bounds();
            bool has = false;
            foreach (var r in track.GetComponentsInChildren<Renderer>())
            {
                if (r is LineRenderer || r.transform.IsChildOf(lu.transform)) continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!has) return;
            Vector3 sp = cam.WorldToScreenPoint(b.center);
            if (sp.z <= 0.01f) return;
            float worldPerPixel = 2f * sp.z * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1f, cam.pixelHeight);
            track.transform.position += Vector3.up * ((cam.pixelHeight * 0.5f - sp.y) * worldPerPixel);
        }
    }

    // ---------- trocar de fase dentro do jogo ----------

    void ShowPhases()
    {
        if (picker != null) picker.Open(catalog, settings.testUnlockAllLevels, level);
    }

    // Troca a fase sem escanear de novo e sem reposicionar a esteira: tema, esteira, roupas da Lu e partida recomeçam.
    void SwitchLevel(LevelConfig newLevel)
    {
        if (newLevel == null) return;
        Resume();
        level = newLevel;
        GameSession.SelectedLevel = level;
        theme.Apply(level);
        if (track == null || lu == null) return;
        var wardrobe = lu.GetComponentInChildren<LuWardrobe>(true);
        if (wardrobe != null) wardrobe.ApplyLevel(level);
        PrepareRun();
    }
}
