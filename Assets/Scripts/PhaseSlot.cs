using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Um quadro da lista de fases. Fase bloqueada = só a silhueta do logo (sem cadeado, sem texto).
public class PhaseSlot : MonoBehaviour
{
    public Button button;
    public Image logo;
    public TMP_Text titleText;
    public TMP_Text scoreText;
    public GameObject completedBadge;

    public void Setup(LevelConfig level, Action<LevelConfig> onSelected)
    {
        bool unlocked = SaveSystem.IsUnlocked(level);

        if (logo)
        {
            logo.sprite = level.brandLogo;
            logo.preserveAspect = true;
            logo.color = unlocked ? Color.white : Color.black;
        }
        if (titleText) titleText.gameObject.SetActive(unlocked);
        if (scoreText) scoreText.gameObject.SetActive(unlocked);
        if (completedBadge) completedBadge.SetActive(unlocked && SaveSystem.IsCompleted(level));

        button.interactable = unlocked;
        if (!unlocked) return;

        if (titleText) titleText.text = level.displayName;
        if (scoreText)
        {
            int best = SaveSystem.BestCoins(level);
            if (SaveSystem.IsCompleted(level)) scoreText.text = "Concluída · " + best + " moedas";
            else if (SaveSystem.HasPlayed(level)) scoreText.text = "Melhor: " + best + " moedas";
            else scoreText.text = "Pronta para jogar";
        }
        button.onClick.AddListener(() => onSelected(level));
    }
}
