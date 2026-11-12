using System;
using UnityEngine;

// Configuração global da mecânica (igual para todas as fases).
// Criar em: Project > clique direito > Create > Lu Runner > Game Settings
// Em jogo, o RunnerGame trabalha numa CÓPIA deste asset: o que o painel de teste altera não é salvo.
[CreateAssetMenu(menuName = "Lu Runner/Game Settings", fileName = "GameSettings")]
public class GameSettings : ScriptableObject
{
    [Serializable]
    public class DiscountTier
    {
        public int minCoins = 15;
        public int percent = 5;
    }

    [Header("Partida")]
    public float runDurationSeconds = 60f;
    public int startingLives = 3;
    public int countdownFrom = 3;
    public float celebrationSeconds = 4f;
    public float gameOverDelaySeconds = 1f;
    public float hudSlideSeconds = 0.6f;

    [Header("Teste")]
    [Tooltip("Liga o botão de debug no topo da tela (também no build). Desligue para a versão final.")]
    public bool debugEnabled = true;

    [Header("AR")]
    public float arScale = 0.08f;
    public bool useAnchor = true;

    [Header("Esteira e módulos (unidades locais da esteira)")]
    public float beltSpeed = 6f;
    public float laneWidth = 1f;
    public float moduleDepth = 1f;
    public float spawnDistance = 16f;
    public float curveRadius = 2f;
    public float fadeCells = 1f;
    public float beltTextureTileLength = 2f;
    public float beltScrollDirection = 1f;

    [Header("Velocidade progressiva")]
    public float speedIncreasePercent = 5f;
    public float speedIncreaseEverySeconds = 15f;
    public float speedRampSeconds = 1f;

    [Header("Reta final")]
    public float alertSeconds = 5f;
    public Color timerNormalColor = Color.white;
    public Color timerAlertColor = new Color(1f, 0.25f, 0.2f, 1f);
    public float timerBlinkInterval = 0.25f;

    [Header("Chegada e comemoração")]
    public float stopDecelSeconds = 1.5f;
    public int confettiCount = 80;
    public float confettiSeconds = 3f;
    public float confettiSize = 0.15f;

    [Header("Lu")]
    public float laneChangeSeconds = 0.12f;
    public float jumpSeconds = 0.6f;
    public float jumpHeight = 0.9f;
    public bool doubleJumpEnabled = false;
    public float doubleJumpHeight = 0.9f;

    [Header("Colisão (colliders-gatilho)")]
    public Vector3 luHitboxSize = new Vector3(0.6f, 1.4f, 0.5f);
    public float luHitboxCenterY = 0.8f;
    [Range(0f, 0.5f)] public float obstacleTolerance = 0.15f;
    [Range(0f, 1f)] public float coinTolerance = 0.3f;
    public float clearAheadDistance = 6f;

    [Header("Depois de esbarrar (um tempo só para tudo)")]
    public float invincibleSeconds = 3f;
    public float blinkInterval = 0.1f;
    [Tooltip("0 = piscada seca (liga e desliga). 1 = transição gradual de opacidade. Acima de 0 exige os materiais da Lu com Surface Type = Transparent.")]
    [Range(0f, 1f)] public float blinkSmoothness = 0f;
    public float vortexSpinDegreesPerSecond = 360f;
    public float hitAnimationSeconds = 0.8f;

    [Header("Módulos e obstáculos")]
    public int startEmptyModules = 6;
    public int minModulesBetweenObstacles = 5;
    [Range(0f, 1f)] public float obstacleChance = 0.5f;
    [Range(0f, 1f)] public float twoObstacleChance = 0.3f;
    [Range(0f, 1f)] public float sceneryChance = 0.5f;
    public bool allowSuspendedAssets = true;
    public float suspendedHeightJump = 2.1f;
    public float suspendedHeightDoubleJump = 3f;

    [Header("Moedas")]
    public int maxCoins = 30;
    public int coinsPerGroup = 3;
    public int coinSpacingModules = 1;
    public float coinHeight = 0.5f;
    public float coinStartDelaySeconds = 3f;
    public float coinTailSeconds = 6f;
    [Range(0f, 1f)] public float coinHardSpotChance = 0.6f;

    [Header("Vestiário (cores disponíveis para meia, short e boné)")]
    public Color[] wardrobeColors = new Color[]
    {
        new Color(0.90f, 0.20f, 0.25f), new Color(1.00f, 0.55f, 0.15f), new Color(1.00f, 0.85f, 0.20f),
        new Color(0.25f, 0.75f, 0.40f), new Color(0.25f, 0.50f, 0.95f), new Color(0.65f, 0.40f, 0.90f),
        new Color(1.00f, 1.00f, 1.00f), new Color(0.10f, 0.10f, 0.10f)
    };

    [Header("Descontos (iguais para todas as fases)")]
    public DiscountTier[] tiers = new DiscountTier[]
    {
        new DiscountTier { minCoins = 15, percent = 5 },
        new DiscountTier { minCoins = 20, percent = 10 },
        new DiscountTier { minCoins = 25, percent = 15 },
        new DiscountTier { minCoins = 30, percent = 20 }
    };

    [Header("Sons")]
    public AudioClip sfxCoin;
    public AudioClip sfxJump;
    public AudioClip sfxHit;
    public AudioClip sfxBeep;
    public AudioClip sfxStartSiren;
    public AudioClip sfxEndAlert;
    public AudioClip sfxVictoryTheme;
    public AudioClip sfxLose;
    public AudioClip sfxUnlock;

    // Devolve a maior faixa alcançada, ou null se nenhuma foi alcançada.
    public DiscountTier GetTier(int coins)
    {
        DiscountTier best = null;
        if (tiers == null) return null;
        foreach (var t in tiers)
        {
            if (t == null) continue;
            if (coins >= t.minCoins && (best == null || t.minCoins > best.minCoins)) best = t;
        }
        return best;
    }
}
