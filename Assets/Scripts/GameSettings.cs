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
    public float celebrationSeconds = 4f;
    public float gameOverDelaySeconds = 1f;

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
    public float speedIncreasePercent = 7f;
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
    [Tooltip("Altura do chão dos confetes, em metros, a partir do ponto onde a esteira foi posicionada. 0 = a superfície da esteira. Os confetes pousam aí e não passam para baixo.")]
    public float confettiFloorHeight = 0f;

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
    [Tooltip("Sem efeito: a distância entre obstáculos agora vem de Obstacle Min Gap Boxes.")]
    public int minModulesBetweenObstacles = 2;
    [Range(0f, 1f)] public float obstacleChance = 0.7f;
    [Tooltip("Sem efeito: cada obstáculo nasce sozinho numa linha transversal, sem alinhar com outro.")]
    [Range(0f, 1f)] public float twoObstacleChance = 0.5f;
    [Tooltip("Sem efeito: cada obstáculo nasce sozinho numa linha transversal, sem alinhar com outro.")]
    [Range(0f, 1f)] public float threeObstacleChance = 0.2f;
    [Tooltip("Quantidade de obstáculos na esteira. Maior = mais obstáculos ao mesmo tempo: multiplica Obstacle Chance e o número de tentativas de posição por módulo.")]
    [Range(0.5f, 3f)] public float obstacleDensity = 1.5f;
    [Tooltip("Caixas livres mínimas, ao longo da esteira, entre dois obstáculos quaisquer (em qualquer pista). Nenhum obstáculo fica na mesma linha transversal de outro. Menor = mais obstáculos ao mesmo tempo.")]
    [Min(0)] public float obstacleMinGapBoxes = 0.5f;
    [Tooltip("Uma pista só recebe outro obstáculo depois que este número de obstáculos foi criado em outras pistas. Só vale para obstáculos (não para moedas). Se nenhuma posição cumpre a regra (por exemplo, depois de objetos largos), usa a pista que ficou sem obstáculo há mais tempo.")]
    [Min(0)] public int obstacleLaneGapObjects = 3;
    [Tooltip("Caixas livres mínimas, ao longo da esteira, entre uma moeda e um obstáculo da mesma pista (antes e depois). Não vale para a moeda que fica em cima de um obstáculo baixo.")]
    [Min(0)] public float coinObstacleGapBoxes = 2f;
    [Tooltip("Sem efeito: cada obstáculo e cada moeda nasce numa posição sorteada, sem seguir pistas com deslocamento fixo.")]
    [Range(0f, 1f)] public float lateralMisalignment = 1f;
    [Tooltip("Altura máxima de qualquer asset, em unidades. Só vale quando a esteira da fase não tem o objeto PainelTopo: com ele, o limite é a distância medida entre a superfície da esteira e a parte mais baixa do PainelTopo.")]
    [Min(0.05f)] public float fallbackMaxAssetHeight = 2f;
    [Tooltip("Folga abaixo do limite de altura, em % do vão até o PainelTopo. Com 10, os assets mais altos ficam 10% abaixo do painel, sem encostar nele.")]
    [Range(0f, 50f)] public float maxHeightMarginPercent = 10f;
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
    [Tooltip("Caixas livres mínimas entre duas moedas, ao longo da esteira. Nenhuma moeda fica na mesma linha transversal de outra.")]
    [Min(0)] public int coinMinGapBoxes = 1;
    public float coinTailSeconds = 6f;
    [Header("Altura das moedas (proporção entre as três)")]
    [Tooltip("Peso das moedas no chão. Cada moeda sorteia a altura por estes três pesos: com 1, 1 e 1, um terço em cada.")]
    [Min(0f)] public float coinGroundWeight = 1f;
    [Tooltip("Peso das moedas na altura do pulo. Só vale com Allow Suspended Assets ligado.")]
    [Min(0f)] public float coinJumpWeight = 1f;
    [Tooltip("Peso das moedas na altura do pulo duplo. Só vale com Allow Suspended Assets e Double Jump Enabled ligados.")]
    [Min(0f)] public float coinDoubleJumpWeight = 1f;
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
    [Tooltip("Segundos para a trilha nova entrar ao mudar de cena.")]
    public float musicFadeInSeconds = 1.5f;
    [Tooltip("Segundos para a trilha anterior sumir ao mudar de cena.")]
    public float musicFadeOutSeconds = 1f;

    [Header("Sons")]
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
    [Tooltip("Toca quando a tela de posicionar encontra uma superfície.")]
    public AudioClip sfxSurfaceFound;

    // Volume de todos os sons (0 a 1). Volume final de cada som = Master Geral x a barra do som. 1 = volume do arquivo; só dá para abaixar.
    [System.Serializable]
    public class SoundVolumeSet
    {
        [Tooltip("Abaixa todos os sons juntos, na mesma proporção (música, efeitos, passadas e aterrissagem).")]
        [Range(0f, 1f)] public float masterGeral = 1f;
        [Range(0f, 1f)] public float music = 0.6f;
        [Range(0f, 1f)] public float footsteps = 1f;
        [Tooltip("Som de aterrissagem da esteira no início do jogo.")]
        [Range(0f, 1f)] public float impact = 1f;
        [Range(0f, 1f)] public float button = 1f;
        [Range(0f, 1f)] public float laneLeft = 1f;
        [Range(0f, 1f)] public float laneRight = 1f;
        [Range(0f, 1f)] public float coin = 1f;
        [Range(0f, 1f)] public float jump = 1f;
        [Tooltip("Vale para os três sons de batida.")]
        [Range(0f, 1f)] public float hits = 1f;
        [Range(0f, 1f)] public float dizzy = 1f;
        [Range(0f, 1f)] public float beep = 1f;
        [Range(0f, 1f)] public float countdown = 1f;
        [Range(0f, 1f)] public float startSiren = 1f;
        [Range(0f, 1f)] public float endAlert = 1f;
        [Range(0f, 1f)] public float victoryTheme = 1f;
        [Range(0f, 1f)] public float lose = 1f;
        [Range(0f, 1f)] public float unlock = 1f;
        [Range(0f, 1f)] public float surfaceFound = 1f;
    }

    [Header("Volume de cada som")]
    public SoundVolumeSet soundVolumes = new SoundVolumeSet();

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
