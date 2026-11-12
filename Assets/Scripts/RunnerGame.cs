using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using TMPro;

// Cena de jogo (única, parametrizada pela fase escolhida).
// Fluxo: introdução > onboarding (só na 1ª vez) > posicionar a esteira > vestiário > Lu aquecendo, esperando o toque na tela >
// HUD desliza para dentro > contagem regressiva > corrida > resultado. "Tentar de novo" recomeça no mesmo lugar.
public class RunnerGame : MonoBehaviour
{
    enum State { Intro, Placing, Wardrobe, Ready, Countdown, Running, Celebrating, GameOver }

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
    public DebugPanel debugPanel;
    public WardrobeScreen wardrobeScreen;

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
    bool godMode;
    bool reviewingOnboarding;

    void Start()
    {
        level = GameSession.SelectedLevel;
        if (level == null) level = testLevel;
        if (level == null && catalog.levels != null && catalog.levels.Length > 0) level = catalog.levels[0];
        if (level == null)
        {
            Debug.LogError("Nenhuma fase definida: escolha uma fase pelo menu ou preencha Test Level.");
            return;
        }
        GameSession.SelectedLevel = level;

        // Cópia dos dados: o painel de teste altera só esta cópia, nunca o asset do projeto.
        settings = Instantiate(catalog.settings);

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
        if (wardrobeScreen != null) wardrobeScreen.Done += OnWardrobeDone;

        if (debugPanel != null)
        {
            debugPanel.Setup(settings.debugEnabled);
            if (settings.debugEnabled) BuildDebug();
        }

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
        if (settings.useAnchor) go.AddComponent<ARAnchor>();
        placer.End();

        track = go.GetComponent<TrackRoot>();
        lu = track.lu;
        lu.Jumped += OnJumped;
        lu.DoubleJumped += OnDoubleJumped;
        lu.ObstacleTouched += OnObstacleTouched;
        lu.CoinTouched += OnCoinTouched;
        luRenderers = lu.GetComponentsInChildren<Renderer>(true);

        BeginWardrobe();
    }

    // ---------- vestiário ----------

    // Entre o posicionamento e o aquecimento: o jogador escolhe a cor da meia, do short e do boné da Lu.
    // Sem LuWardrobe no modelo, ou sem a tela, o jogo segue direto para o aquecimento.
    void BeginWardrobe()
    {
        track.Init(settings, level);
        lu.Init(settings);
        SetLuVisible(true);

        var wardrobe = lu.GetComponentInChildren<LuWardrobe>(true);
        if (wardrobe != null) wardrobe.ApplySaved(settings);
        if (wardrobeScreen == null || wardrobe == null || settings.wardrobeColors == null || settings.wardrobeColors.Length == 0)
        {
            PrepareRun();
            return;
        }

        PlayState("Preparo");
        state = State.Wardrobe;
        ui.Show(wardrobeScreen.gameObject);
        wardrobeScreen.Open(wardrobe, settings);
    }

    void OnWardrobeDone()
    {
        if (state == State.Wardrobe) PrepareRun();
    }

    // ---------- preparar e começar a partida ----------

    // Deixa tudo no começo, com a esteira no mesmo lugar: Lu aquecendo, esperando o toque na tela.
    void PrepareRun()
    {
        StopAllCoroutines();
        Sfx.StopTimed();
        DestroyVortex();

        lives = settings.startingLives;
        coins = 0;
        elapsed = 0f;
        invincibleLeft = 0f;
        hitAnimLeft = 0f;
        lastAlertSecond = -1;
        jumpAnimActive = false;

        track.Init(settings, level);
        lu.Init(settings);
        SetLuVisible(true);
        PlayState("Aquecimento");

        ui.Show(hudScreen);
        hud.ConfigureTimer(settings);
        hud.SetControlsVisible(false);
        hud.SetLives(lives);
        hud.SetCoins(0);
        hud.SetTimer(settings.runDurationSeconds);
        hud.PrepareSlide();
        hud.SetCenter("Toque na tela para começar");

        readyTimer = 0f;
        state = State.Ready;
    }

    IEnumerator CountdownRoutine()
    {
        state = State.Countdown;
        hud.SetCenter("");
        yield return StartCoroutine(hud.SlideIn(settings.hudSlideSeconds));

        PlayState("Preparo");
        for (int i = settings.countdownFrom; i > 0; i--)
        {
            hud.SetCenter(i.ToString());
            Sfx.Play(settings.sfxBeep);
            yield return new WaitForSeconds(1f);
        }

        hud.SetCenter("JÁ!");
        Sfx.Play(settings.sfxStartSiren);
        PlayState("Correr");
        hud.SetControlsVisible(true);
        lu.CanControl = true;
        lu.SensorActive = true;
        state = State.Running;

        yield return new WaitForSeconds(0.7f);
        if (state == State.Running) hud.SetCenter("");
    }

    void Update()
    {
        if (state == State.Placing)
        {
            placeButton.interactable = placer.HasPose;
            if (placementHint)
                placementHint.text = placer.HasPose
                    ? "Superfície encontrada. Toque em Posicionar."
                    : "Aponte para uma superfície plana e mova o celular devagar.";
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
        if (debugPanel != null && debugPanel.BlocksTap(pointer.position.ReadValue())) return false;
        return true;
    }

    void RunTick()
    {
        float dt = Time.deltaTime;
        elapsed += dt;

        bool spawning = elapsed < settings.runDurationSeconds;
        if (!spawning && !track.FinishSpawned) track.SpawnFinish();
        track.Tick(dt, elapsed, spawning);

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
        if (state != State.Running || invincibleLeft > 0f || godMode) return;
        OnHit();
    }

    void OnCoinTouched(BeltItem item)
    {
        if (state != State.Running || item == null || !item.gameObject.activeInHierarchy) return;
        track.Remove(item);
        coins++;
        hud.SetCoins(coins);
        Sfx.Play(settings.sfxCoin);
    }

    void OnHit()
    {
        lives--;
        hud.SetLives(lives);
        track.ClearObstaclesAhead(settings.clearAheadDistance);

        if (lives <= 0)
        {
            Sfx.Play(settings.sfxHit);
            StartCoroutine(Lose());
            return;
        }
        invincibleLeft = settings.invincibleSeconds;
        Sfx.PlayFor(settings.sfxHit, settings.invincibleSeconds);
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
                if (blink >= settings.blinkInterval)
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

    // Opacidade da Lu na piscada: mistura a onda seca (liga/desliga) com uma onda suave, conforme blinkSmoothness.
    float BlinkAlpha(float t)
    {
        float iv = Mathf.Max(0.02f, settings.blinkInterval);
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
        DestroyVortex();
        Sfx.StopTimed();
        hud.SetTimerAlert(false);
        hud.SetControlsVisible(false);
        Sfx.Play(settings.sfxLose);
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
        hud.SetTimerAlert(false);
        hud.SetControlsVisible(false);
        PlayState("Comemorar");
        Sfx.Play(settings.sfxVictoryTheme);
        track.PlayConfetti(level.palette);
        yield return new WaitForSeconds(settings.celebrationSeconds);

        SaveSystem.SaveResult(level, coins, true);
        results.ShowVictory(level, coins, settings.GetTier(coins));
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

    // ---------- painel de teste ----------

    void BuildDebug()
    {
        var d = debugPanel;
        d.AddInfo(delegate
        {
            return state + " | moedas " + coins + " | vidas " + lives + " | " + elapsed.ToString("0.0") + " s";
        });
        d.AddInfo(delegate
        {
            if (track == null) return "esteira ainda não posicionada";
            return "vel " + track.CurrentSpeed.ToString("0.0") + " | módulo " + track.ModuleIndex + " | itens " + track.ItemCount;
        });

        d.AddFloat("Velocidade da esteira", () => settings.beltSpeed, v => settings.beltSpeed = v, 0.5f, 1f, 20f);
        d.AddFloat("Aumento de velocidade (%)", () => settings.speedIncreasePercent, v => settings.speedIncreasePercent = v, 1f, 0f, 50f);
        d.AddFloat("A cada (s)", () => settings.speedIncreaseEverySeconds, v => settings.speedIncreaseEverySeconds = v, 1f, 1f, 60f);
        d.AddFloat("Chance de obstáculo", () => settings.obstacleChance, v => settings.obstacleChance = v, 0.05f, 0f, 1f);
        d.AddFloat("Módulos entre obstáculos", () => settings.minModulesBetweenObstacles, v => settings.minModulesBetweenObstacles = Mathf.RoundToInt(v), 1f, 1f, 12f, "0");
        d.AddFloat("Moedas em lugar difícil", () => settings.coinHardSpotChance, v => settings.coinHardSpotChance = v, 0.1f, 0f, 1f);
        d.AddFloat("Altura do pulo", () => settings.jumpHeight, v => settings.jumpHeight = v, 0.1f, 0.3f, 2f);
        d.AddFloat("Tolerância obstáculo", () => settings.obstacleTolerance, v => settings.obstacleTolerance = v, 0.05f, 0f, 0.5f);
        d.AddFloat("Tolerância moeda", () => settings.coinTolerance, v => settings.coinTolerance = v, 0.1f, 0f, 1f);
        d.AddFloat("Invencível/pisca/redemoinho (s)", () => settings.invincibleSeconds, v => settings.invincibleSeconds = v, 0.5f, 0.5f, 10f);
        d.AddFloat("Piscada suave (0 seca, 1 gradual)", () => settings.blinkSmoothness, v => settings.blinkSmoothness = v, 0.1f, 0f, 1f);
        d.AddFloat("Alerta final (s)", () => settings.alertSeconds, v => settings.alertSeconds = v, 1f, 0f, 15f, "0");
        d.AddBool("Salto duplo", () => settings.doubleJumpEnabled, v => settings.doubleJumpEnabled = v);
        d.AddBool("Assets suspensos", () => settings.allowSuspendedAssets, v => settings.allowSuspendedAssets = v);
        d.AddBool("Sem perder vida", () => godMode, v => godMode = v);

        d.AddButtons(
            new[] { "+1 vida", "+5 moedas", "Vitória" },
            new System.Action[]
            {
                () => { if (state == State.Running) { lives = Mathf.Min(lives + 1, 9); hud.SetLives(lives); } },
                () => { if (state == State.Running) { coins += 5; hud.SetCoins(coins); } },
                () => { if (state == State.Running) elapsed = settings.runDurationSeconds; }
            });
        d.AddButtons(
            new[] { "Derrota", "Recomeçar" },
            new System.Action[]
            {
                () => { if (state == State.Running) StartCoroutine(Lose()); },
                RestartInPlace
            });
    }
}
