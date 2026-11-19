using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Cards de fim de partida: game over (tentar de novo / fases / escanear), vitória (mensagem, moedas,
// objeto 3D do desconto, botão do cupom) e a tela do cupom com o QR Code de exemplo.
public class ResultScreen : MonoBehaviour
{
    public UiScreens ui;
    public ProductDisplay productDisplay;

    [Header("Game over")]
    public GameObject gameOverScreen;
    public TMP_Text gameOverCoinsText;
    public Button retryButton;
    public Button gameOverPhasesButton;
    public Button gameOverScannerButton;

    [Header("Vitória")]
    public GameObject victoryScreen;
    public TMP_Text victoryMessageText;
    public TMP_Text victoryCoinsText;
    public GameObject productGroup;
    public Button couponButton;
    public Button victoryPhasesButton;
    public Button victoryScannerButton;

    [Header("Cupom")]
    public GameObject couponScreen;
    public TMP_Text couponMessageText;
    public Button couponBackButton;

    // "Tentar de novo": o RunnerGame recomeça a partida com a esteira no mesmo lugar.
    public event Action RetryRequested;
    // "Fases": o RunnerGame abre a lista de fases dentro do jogo.
    public event Action PhasesRequested;

    LevelConfig level;
    int coins;
    GameSettings.DiscountTier tier;

    void Start()
    {
        retryButton.onClick.AddListener(() => { if (RetryRequested != null) RetryRequested(); });
        gameOverPhasesButton.onClick.AddListener(() => { if (PhasesRequested != null) PhasesRequested(); });
        gameOverScannerButton.onClick.AddListener(Nav.LoadScanner);

        couponButton.onClick.AddListener(ShowCoupon);
        victoryPhasesButton.onClick.AddListener(() => { if (PhasesRequested != null) PhasesRequested(); });
        victoryScannerButton.onClick.AddListener(Nav.LoadScanner);

        couponBackButton.onClick.AddListener(() => ui.Show(victoryScreen));
    }

    public void ShowGameOver(LevelConfig levelConfig, int collectedCoins)
    {
        level = levelConfig;
        coins = collectedCoins;
        if (gameOverCoinsText) gameOverCoinsText.text = coins + " moedas";
        ui.Show(gameOverScreen);
    }

    public void ShowVictory(LevelConfig levelConfig, int collectedCoins, GameSettings.DiscountTier reachedTier)
    {
        level = levelConfig;
        coins = collectedCoins;
        tier = reachedTier;
        bool hasReward = tier != null;

        if (victoryCoinsText) victoryCoinsText.text = coins + " moedas";
        if (victoryMessageText)
        {
            victoryMessageText.text = hasReward
                ? level.Format(level.rewardMessage, coins, tier.percent)
                : level.Format(level.noRewardMessage, coins, 0);
        }
        if (productGroup) productGroup.SetActive(hasReward);
        couponButton.gameObject.SetActive(hasReward);
        if (hasReward && productDisplay != null) productDisplay.Show(level.productPrefab);

        ui.Show(victoryScreen);
    }

    void ShowCoupon()
    {
        if (level == null || tier == null) return;
        if (couponMessageText) couponMessageText.text = level.Format(level.couponMessage, coins, tier.percent);
        ui.Show(couponScreen);
    }
}
