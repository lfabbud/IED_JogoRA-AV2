using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Cards de fim de partida: game over (tentar de novo / fases / escanear), vitória (mensagem, moedas,
// objeto 3D do desconto, botão do cupom) e a tela do cupom com o QR Code de exemplo.
public class ResultScreen : MonoBehaviour, IPhasePreview
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
    public TMP_Text victoryCoinsText;
    public GameObject productGroup;
    public Button couponButton;
    public Button victoryPhasesButton;
    public Button victoryScannerButton;

    [Header("Cupom")]
    public GameObject couponScreen;
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

    // Pré-visualização na Scene: textos de vitória, cupom e game over com a fase escolhida e a maior faixa de desconto.
    // As telas continuam desativadas; ative a que quiser ver na Hierarchy.
    public void ApplyPreview(LevelConfig lvl)
    {
        if (Application.isPlaying || lvl == null) return;
        var st = PhasePreview.Settings();
        int pct = 0;
        int c = 0;
        if (st != null && st.tiers != null && st.tiers.Length > 0)
        {
            var t = st.tiers[st.tiers.Length - 1];
            pct = t.percent;
            c = t.minCoins;
        }
        if (gameOverCoinsText) gameOverCoinsText.text = c + " moedas";
        if (victoryCoinsText) victoryCoinsText.text = c + " moedas";
        LevelThemeApplier.SetText(gameObject.scene, "RewardMessage", lvl.Format(lvl.rewardMessage, c, pct));
        LevelThemeApplier.SetText(gameObject.scene, "CouponMessage", lvl.Format(lvl.couponMessage, c, pct));
    }

    public void ClearPreview() { }

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
        LevelThemeApplier.SetText(gameObject.scene, "RewardMessage", level.Format(level.rewardMessage, coins, hasReward ? tier.percent : 0));
        if (productGroup) productGroup.SetActive(hasReward);
        couponButton.gameObject.SetActive(hasReward);
        if (hasReward && productDisplay != null) productDisplay.Show(level.productPrefab);

        ui.Show(victoryScreen);
    }

    void ShowCoupon()
    {
        if (level == null || tier == null) return;
        LevelThemeApplier.SetText(gameObject.scene, "CouponMessage", level.Format(level.couponMessage, coins, tier.percent));
        ui.Show(couponScreen);
    }
}
