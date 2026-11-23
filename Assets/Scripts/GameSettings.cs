using System;
using UnityEngine;

// Configuração global da mecânica (igual para todas as fases).
// Criar em: Project > clique direito > Create > Lu Runner > Game Settings
// Em jogo, o RunnerGame trabalha numa CÓPIA deste asset, feita ao abrir a cena: valores alterados no Inspector durante o Play
// só valem na próxima vez que a cena abrir.
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

    [Header("Textos de jogo")]
    [Tooltip("Tela de posicionar, no teste na tela (sem AR).")]
    [TextArea(1, 3)] public string placementTestMessage = "Teste na tela. Toque em Posicionar.";
    [Tooltip("Tela de posicionar, quando a superfície foi encontrada.")]
    [TextArea(1, 3)] public string placementFoundMessage = "Superfície encontrada. Toque em Posicionar.";
    [Tooltip("Tela de posicionar, enquanto procura uma superfície.")]
    [TextArea(1, 3)] public string placementSearchingMessage = "Aponte para uma superfície plana e mova o celular devagar.";
    [Tooltip("Aparece no centro do HUD, com a Lu aquecendo, antes de a contagem começar.")]
    [TextArea(1, 3)] public string readyMessage = "Toque na tela para começar";
    [Tooltip("Texto de cada passo da contagem regressiva. {n} vira o número da vez (3, 2, 1). O número inicial está em Countdown From, na seção Partida.")]
    public string countdownMessage = "{n}";
    [Tooltip("Aparece quando a contagem termina e a corrida começa.")]
    public string goMessage = "JÁ!";

    [Header("Teste")]
    [Tooltip("A lista de trocar de fase mostra todas as fases, mesmo as ainda bloqueadas. Desmarque antes do build final.")]
    public bool testUnlockAllLevels = true;
    [Tooltip("Testa o jogo na tela, sem AR: a esteira é colocada à frente da câmera, sem plano detectado e sem âncora. Desmarque antes do build final.")]
    public bool testOnScreen = false;
    [Tooltip("Só no teste na tela: inclina a esteira para a câmera enxergar o topo dela de cima (0 = esteira reta, de frente).")]
    [Range(0f, 60f)] public float testTiltDegrees = 25f;

    [Header("Interface")]
    [Tooltip("Desmarcado: a interface fica sempre no leiaute vertical. Marcado: o leiaute gira com o aparelho (Orientation Layout).")]
    public bool followDeviceOrientation = false;

    [Header("AR")]
    public float arScale = 0.0786f;
    public bool useAnchor = true;

    [Header("Esteira e módulos (unidades locais da esteira)")]
    public float beltSpeed = 6f;
    public float laneWidth = 1f;
    public float moduleDepth = 1f;
    public float spawnDistance = 16f;
    public float curveRadius = 0.6f;
    public float fadeCells = 1f;
    public float beltTextureTileLength = 2f;
    public float beltScrollDirection = 1f;
    [Tooltip("Marcado: a textura da Fita rola no eixo U. Desmarcado: no eixo V (padrão).")]
    public bool beltScrollOnU = false;
    [Tooltip("Altura de uma caixa = largura da pista x este valor (0,5 = metade, para a Lu poder pular).")]
    public float cellHeightRatio = 0.5f;
    public float CellHeight { get { return laneWidth * cellHeightRatio; } }

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
    public bool doubleJumpEnabled = true;
    public float doubleJumpHeight = 0.9f;

    [Header("Colisão (colliders-gatilho)")]
    public Vector3 luHitboxSize = new Vector3(0.6f, 1.4f, 0.5f);
    public float luHitboxCenterY = 0.8f;
    [Range(0f, 0.5f)] public float obstacleTolerance = 0.15f;
    [Range(0f, 1f)] public float coinTolerance = 0.3f;
    public float clearAheadDistance = 6f;

    [Header("Depois de esbarrar (um tempo só para tudo)")]
    public float invincibleSeconds = 3f;
    [Tooltip("Piscadas por segundo: cada piscada = metade do tempo some e metade aparece (3 = 1/6 s sumida, 1/6 s visível).")]
    public float blinksPerSecond = 3f;
    [Tooltip("0 = piscada seca (liga e desliga). 1 = transição gradual de opacidade. Acima de 0 exige os materiais da Lu com Surface Type = Transparent.")]
    [Range(0f, 1f)] public float blinkSmoothness = 0f;
    public float vortexSpinDegreesPerSecond = 360f;
    public float hitAnimationSeconds = 0.8f;

    [Header("Módulos e obstáculos")]
    public int startEmptyModules = 6;
    public int minModulesBetweenObstacles = 2;
    [Range(0f, 1f)] public float obstacleChance = 0.7f;
    [Range(0f, 1f)] public float twoObstacleChance = 0.5f;
    [Range(0f, 1f)] public float threeObstacleChance = 0.2f;
    [Tooltip("0 = as caixas das 5 pistas ficam totalmente alinhadas lado a lado. 1 = cada pista começa o jogo com um deslocamento aleatório (até uma caixa) em relação às outras.")]
    [Range(0f, 1f)] public float lateralMisalignment = 1f;
    [Tooltip("Altura máxima de um asset = este número x a maior medida da caixa do asset. Acima disso, ele é reduzido por igual. 0 = sem limite.")]
    public float maxHeightFactor = 3f;
    [Range(0f, 1f)] public float sceneryChance = 0.5f;
    public bool allowSuspendedAssets = true;
    [Tooltip("Quanto a base de um asset suspenso invade o topo da Lu no ponto mais alto do pulo.")]
    public float suspendedOverlap = 0.3f;
    // Alturas calculadas a partir do pulo da Lu: mudou o pulo, mudam as alturas.
    float LuTop { get { return luHitboxCenterY + luHitboxSize.y * 0.5f; } }
    public float JumpPeak { get { return jumpHeight; } }
    public float DoubleJumpPeak { get { return jumpHeight + doubleJumpHeight; } }
    public float suspendedHeightJump { get { return JumpPeak + LuTop - suspendedOverlap; } }
    public float suspendedHeightDoubleJump { get { return DoubleJumpPeak + LuTop - suspendedOverlap; } }
    public float CoinHeightJump { get { return JumpPeak + coinHeight - coinReachMargin; } }
    public float CoinHeightDoubleJump { get { return DoubleJumpPeak + coinHeight - coinReachMargin; } }

    [Header("Moedas")]
    public int maxCoins = 30;
    public float coinHeight = 0.5f;
    [Tooltip("Quanto abaixo do ponto exato as moedas do pulo ficam, para facilitar pegar.")]
    public float coinReachMargin = 0f;
    public float coinStartDelaySeconds = 3f;
    public float coinTailSeconds = 6f;
    [Range(0f, 1f)] public float coinHardSpotChance = 0.3f;
    [Tooltip("Tamanho das moedas (1 = tamanho do modelo).")]
    public float coinScale = 0.9f;

    [Header("Efeito ao pegar moeda")]
    public float coinPopSeconds = 0.25f;
    [Tooltip("A moeda cresce até este tamanho (1,3 = 30% maior) enquanto some.")]
    public float coinPopScale = 1.3f;
    public float coinPopRise = 0.4f;
    public int coinSparkCount = 8;
    public float coinSparkSize = 0.08f;
    [Tooltip("Cor da faísca quando o material da moeda é branco (a cor vem da textura).")]
    public Color coinSparkFallbackColor = new Color(1f, 0.82f, 0.2f, 1f);

    [Header("Descontos (iguais para todas as fases)")]
    public DiscountTier[] tiers = new DiscountTier[]
    {
        new DiscountTier { minCoins = 15, percent = 5 },
        new DiscountTier { minCoins = 20, percent = 10 },
        new DiscountTier { minCoins = 25, percent = 15 },
        new DiscountTier { minCoins = 30, percent = 20 }
    };

    [Header("Música")]
    [Tooltip("Trilha do menu e do scanner (abertura).")]
    public AudioClip musicOpening;
    [Tooltip("Trilha de toda a cena de jogo.")]
    public AudioClip musicGame;
    [Range(0f, 1f)] public float musicVolume = 0.6f;
    [Tooltip("Segundos para a trilha nova entrar ao mudar de cena.")]
    public float musicFadeInSeconds = 1.5f;
    [Tooltip("Segundos para a trilha anterior sumir ao mudar de cena.")]
    public float musicFadeOutSeconds = 1f;

    [Header("Sons")]
    [Tooltip("Volume de todos os efeitos sonoros (0 a 1). A música tem o seu próprio volume na seção Música.")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Tooltip("Toca em todo botão apertado (menu, telas do jogo, lista de fases). Os botões esquerda, direita e pular do jogo não usam este som.")]
    public AudioClip sfxButton;
    [Tooltip("Toca cada vez que a Lu troca de pista para a esquerda.")]
    public AudioClip sfxLaneLeft;
    [Tooltip("Toca cada vez que a Lu troca de pista para a direita.")]
    public AudioClip sfxLaneRight;
    public AudioClip sfxCoin;
    public AudioClip sfxJump;
    [Tooltip("Passadas da Lu: tocam em repetição enquanto ela corre e param enquanto ela está no ar (pulo). O arquivo precisa emendar sem pausa ao repetir.")]
    public AudioClip sfxFootsteps;
    [Range(0f, 1f)] public float footstepsVolume = 1f;
    [Tooltip("Sons de batida nos obstáculos. A cada batida toca um destes, sorteado, sem repetir o mesmo duas vezes seguidas. Sons vazios são ignorados.")]
    public AudioClip[] sfxHits = new AudioClip[3];
    [Tooltip("Toca em repetição durante o tempo de tontura e invencibilidade (som de passarinho).")]
    public AudioClip sfxDizzy;
    [Tooltip("Toca em cada número da contagem regressiva. Não toca quando o campo Sfx Countdown tem um som.")]
    public AudioClip sfxBeep;
    [Tooltip("Som único da contagem regressiva inteira: toca uma vez, junto com o primeiro número, no lugar dos bips. Deve durar o tempo da contagem (1 segundo por número). Vazio: toca o Sfx Beep em cada número.")]
    public AudioClip sfxCountdown;
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
