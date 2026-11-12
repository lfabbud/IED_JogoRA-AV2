using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD da partida: vidas, moedas, cronômetro (com alerta na reta final), mensagem central, botões
// (esquerda, direita, pular, pause) e a entrada deslizante dos elementos laterais quando o jogador toca na tela.
public class HudController : MonoBehaviour
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

    [Header("Entrada deslizante (ao tocar na tela)")]
    public RectTransform[] slideFromLeft;
    public RectTransform[] slideFromRight;
    public float slideDistance = 1200f;

    public event Action LeftPressed;
    public event Action RightPressed;
    public event Action JumpPressed;
    public event Action PausePressed;

    Vector2[] leftFinal;
    Vector2[] rightFinal;
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
        leftFinal = Capture(slideFromLeft);
        rightFinal = Capture(slideFromRight);
    }

    static Vector2[] Capture(RectTransform[] items)
    {
        if (items == null) return new Vector2[0];
        var result = new Vector2[items.Length];
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i]) result[i] = items[i].anchoredPosition;
        }
        return result;
    }

    // Põe os elementos laterais fora da tela (esquerda e direita), prontos para deslizar.
    public void PrepareSlide()
    {
        Apply(slideFromLeft, leftFinal, -slideDistance);
        Apply(slideFromRight, rightFinal, slideDistance);
    }

    public IEnumerator SlideIn(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.01f, seconds)));
            Apply(slideFromLeft, leftFinal, -slideDistance * (1f - k));
            Apply(slideFromRight, rightFinal, slideDistance * (1f - k));
            yield return null;
        }
        Apply(slideFromLeft, leftFinal, 0f);
        Apply(slideFromRight, rightFinal, 0f);
    }

    static void Apply(RectTransform[] items, Vector2[] finals, float offsetX)
    {
        if (items == null || finals == null) return;
        for (int i = 0; i < items.Length && i < finals.Length; i++)
        {
            if (items[i]) items[i].anchoredPosition = finals[i] + new Vector2(offsetX, 0f);
        }
    }

    public void SetControlsVisible(bool visible)
    {
        if (controlsRoot) controlsRoot.SetActive(visible);
    }

    public void SetLives(int lives)
    {
        if (lifeIcons == null) return;
        for (int i = 0; i < lifeIcons.Length; i++)
        {
            if (lifeIcons[i]) lifeIcons[i].color = i < lives ? lifeOn : lifeOff;
        }
    }

    public void SetCoins(int coins)
    {
        if (coinsText) coinsText.text = coins.ToString();
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
