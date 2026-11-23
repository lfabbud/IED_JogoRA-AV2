using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD da partida: vidas, moedas, cronômetro (com alerta na reta final), mensagem central, botões
// (esquerda, direita, pular, pause).
public class HudController : MonoBehaviour, IPhasePreview
{
    public GameObject controlsRoot;
    public Button leftButton;
    public Button rightButton;
    public Button jumpButton;
    public Button pauseButton;
    public TMP_Text coinsText;
    public TMP_Text timerText;
    public TMP_Text centerText;
    public Image[] lifeIcons;
    public Color lifeOn = Color.white;
    public Color lifeOff = new Color(1f, 1f, 1f, 0.25f);
    public Sprite lifeOnSprite;
    public Sprite lifeOffSprite;

    public event Action LeftPressed;
    public event Action RightPressed;
    public event Action JumpPressed;
    public event Action PausePressed;

    Color timerNormal = Color.white;
    Color timerAlert = Color.red;
    float timerBlink = 0.25f;
    bool alertOn;

    void Awake()
    {
        if (leftButton) leftButton.onClick.AddListener(() => { if (LeftPressed != null) LeftPressed(); });
        if (rightButton) rightButton.onClick.AddListener(() => { if (RightPressed != null) RightPressed(); });
        if (jumpButton) jumpButton.onClick.AddListener(() => { if (JumpPressed != null) JumpPressed(); });
        if (pauseButton) pauseButton.onClick.AddListener(() => { if (PausePressed != null) PausePressed(); });
    }

    // Pré-visualização na Scene: vidas cheias, 12 moedas e o tempo total da partida.
    public void ApplyPreview(LevelConfig level)
    {
        if (Application.isPlaying) return;
        var st = PhasePreview.Settings();
        if (st == null) return;
        ConfigureTimer(st);
        SetLives(st.startingLives);
        SetCoins(12);
        SetTimer(st.runDurationSeconds);
    }

    public void ClearPreview() { }

    public void SetControlsVisible(bool visible)
    {
        if (controlsRoot) controlsRoot.SetActive(visible);
    }

    public void SetLives(int lives)
    {
        if (lifeIcons == null) return;
        for (int i = 0; i < lifeIcons.Length; i++)
        {
            var icon = lifeIcons[i];
            if (!icon) continue;
            bool on = i < lives;
            icon.color = on ? lifeOn : lifeOff;
            Sprite sp = on ? lifeOnSprite : lifeOffSprite;
            if (sp != null) icon.sprite = sp;
        }
    }

    public void SetCoins(int coins)
    {
        if (coinsText) coinsText.text = coins.ToString();
    }

    // Pulsada do contador de moedas (1 > 1,3 > 1) ao pegar uma moeda.
    public void PulseCoins()
    {
        if (!coinsText || !isActiveAndEnabled) return;
        StopCoroutine("PulseRoutine");
        StartCoroutine("PulseRoutine");
    }

    IEnumerator PulseRoutine()
    {
        var tr = coinsText.transform;
        float t = 0f;
        const float dur = 0.2f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            tr.localScale = Vector3.one * (1f + 0.3f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        tr.localScale = Vector3.one;
    }

    public void SetTimer(float secondsLeft)
    {
        if (!timerText) return;
        int total = Mathf.CeilToInt(Mathf.Max(0f, secondsLeft));
        timerText.text = (total / 60) + ":" + (total % 60).ToString("00");
    }

    public void ConfigureTimer(GameSettings s)
    {
        timerNormal = s.timerNormalColor;
        timerAlert = s.timerAlertColor;
        timerBlink = Mathf.Max(0.05f, s.timerBlinkInterval);
        SetTimerAlert(false);
    }

    // Reta final: o cronômetro troca de cor e pisca.
    public void SetTimerAlert(bool on)
    {
        alertOn = on;
        if (!on && timerText) timerText.color = timerNormal;
    }

    void Update()
    {
        if (!alertOn || !timerText) return;
        bool visible = Mathf.FloorToInt(Time.unscaledTime / timerBlink) % 2 == 0;
        timerText.color = new Color(timerAlert.r, timerAlert.g, timerAlert.b, visible ? 1f : 0.2f);
    }

    public void SetCenter(string text)
    {
        if (centerText) centerText.text = text;
    }
}
