using System;
using UnityEngine;
using UnityEngine.UI;

// Cards de onboarding com passadores (anterior/próximo e pontos), ícone de fechar e botão de iniciar.
public class OnboardingPanel : MonoBehaviour
{
    public GameObject[] pages;
    public Image[] dots;
    public Color dotOn = Color.white;
    public Color dotOff = new Color(1f, 1f, 1f, 0.3f);
    public Button previousButton;
    public Button nextButton;
    public Button startButton;
    public Button closeButton;

    // Disparado ao tocar em iniciar ou em fechar.
    public event Action Finished;

    int index;
    bool wired;

    void Awake()
    {
        Wire();
    }

    void OnEnable()
    {
        Wire();
        Show(0);
    }

    void Wire()
    {
        if (wired) return;
        wired = true;
        if (previousButton) previousButton.onClick.AddListener(() => Show(index - 1));
        if (nextButton) nextButton.onClick.AddListener(() => Show(index + 1));
        if (startButton) startButton.onClick.AddListener(Finish);
        if (closeButton) closeButton.onClick.AddListener(Finish);
    }

    // Cor dos pontos: o ponto da página atual usa "on"; os demais, a mesma cor mais transparente.
    public void SetDotColors(Color on)
    {
        dotOn = new Color(on.r, on.g, on.b, 1f);
        dotOff = new Color(on.r, on.g, on.b, 0.3f);
        if (isActiveAndEnabled) Show(index);
    }

    void Show(int i)
    {
        if (pages == null || pages.Length == 0) return;
        int last = pages.Length - 1;
        index = Mathf.Clamp(i, 0, last);
        for (int k = 0; k < pages.Length; k++)
        {
            if (pages[k]) pages[k].SetActive(k == index);
        }
        if (dots != null)
        {
            for (int k = 0; k < dots.Length; k++)
            {
                if (dots[k]) dots[k].color = (k == index) ? dotOn : dotOff;
            }
        }
        if (previousButton) previousButton.gameObject.SetActive(index > 0);
        if (nextButton) nextButton.gameObject.SetActive(index < last);
        if (startButton) startButton.gameObject.SetActive(index == last);
    }

    void Finish()
    {
        if (Finished != null) Finished();
    }
}
