using System;
using UnityEngine;

// Altura em que um asset aparece na esteira: encostado no piso ou suspenso na altura do pulo normal / do pulo duplo.
public enum AssetHeight { Floor, Jump, DoubleJump }

// Faces do modelo 3D (eixos locais do prefab). Usadas nas travas de rotação.
public enum AssetFace { Down, Up, Left, Right, Back, Front }

// Uma fase = um asset deste tipo. Muda o tema (assets 3D, cores, textos, logo);
// a mecânica fica em GameSettings.
// Criar em: Project > clique direito > Create > Lu Runner > Level Config
[CreateAssetMenu(menuName = "Lu Runner/Level Config", fileName = "Fase_")]
public class LevelConfig : ScriptableObject
{
    // Um asset do "espaço de posicionamento" da fase.
    // Pivô do prefab: no centro do conjunto de casas que ele ocupa, com a base no piso.
    [Serializable]
    public class AssetEntry
    {
        public GameObject prefab;
        [Tooltip("Suspenso: no momento todas as 5 trilhas são jogáveis e este campo não tem efeito.")]
        public bool onlyEdgeLanes = false;
        [Range(1, 3)] public int cellsAcross = 1;
        [Range(1, 3)] public int cellsAlong = 1;
        [Tooltip("Altura do espaço reservado, em caixas (1 caixa = metade da largura de uma pista). Só serve para conferir o tamanho.")]
        [Range(1, 3)] public int cellsHigh = 1;
        [Tooltip("Obstáculo baixo que dá para pular. Desmarcado: Lu precisa trocar de pista.")]
        public bool canBeJumped = true;
        [Tooltip("Alturas possíveis do asset. O sistema sorteia entre as marcadas (marque de 1 a 3). Sem nenhuma marcada, vale o piso.")]
        public bool heightFloor = true;
        public bool heightJump = false;
        [Tooltip("Só é usada com Salto duplo ligado nas configurações.")]
        public bool heightDoubleJump = false;
        [Tooltip("Sorteia uma das orientações possíveis (giros de 90 graus nos 3 eixos). Orientações que não cabem no espaço reservado são descartadas.")]
        public bool randomRotation = false;
        [Tooltip("Trava: a face escolhida fica sempre virada para o piso.")]
        public bool lockFloor = false;
        public AssetFace floorFace = AssetFace.Down;
        [Tooltip("Trava: a face escolhida fica sempre virada para a Lu.")]
        public bool lockFront = false;
        public AssetFace frontFace = AssetFace.Front;
    }

    [Header("Identificação")]
    public string id = "goyta";
    public string displayName = "Goyta";
    public string posterImageName = "Goyta";
    public Sprite brandLogo;

    [Header("Textos (use {marca}, {desconto} e {moedas})")]
    [TextArea] public string introText = "";
    [TextArea] public string rewardMessage = "Parabéns! Você ganhou {desconto}% de desconto em {marca}.";
    [TextArea] public string couponMessage = "Apresente este QR Code e ganhe {desconto}% de desconto em {marca}.";
    [TextArea] public string scannerUnlockedMessage = "Fase {marca} desbloqueada!";
    [TextArea] public string scannerRevisitMessage = "Fase {marca}";

    [Header("Cores")]
    public Color themeColor = new Color(0.08f, 0.08f, 0.12f, 1f);
    public Color accentColor = new Color(0.76f, 0.55f, 0.86f, 1f);
    public Color trackColor = Color.white;
    [Tooltip("Cor do corpo da esteira 3D (todas as peças do modelo, menos a Fita e o PainelTopo).")]
    public Color bodyColor = Color.white;
    [Tooltip("Cor do PainelTopo da esteira 3D.")]
    public Color panelColor = Color.white;
    [Tooltip("Cor das letras (títulos e textos) sobre o fundo da fase.")]
    public Color textColor = Color.white;
    [Tooltip("Cores PRIMÁRIAS (você define). Cada obstáculo e cada objeto de cenário instanciado, em qualquer das 5 pistas, recebe cores sorteadas só desta lista. Vazia: mantém as cores originais do modelo.")]
    [UnityEngine.Serialization.FormerlySerializedAs("palette")]
    public Color[] primaryPalette;
    [Tooltip("Cores SECUNDÁRIAS (você define). Não são mais usadas nas peças das pistas (todas usam a paleta primária); só entram no sorteio das cores dos confetes.")]
    public Color[] secondaryPalette;

    // Paleta de uma peça: as 5 pistas (meio e laterais) usam a primária. O parâmetro é mantido só por compatibilidade.
    public Color[] PaletteFor(bool edgeLane)
    {
        return primaryPalette;
    }

    // Confetes: sorteiam entre as duas paletas.
    public Color[] AllColors()
    {
        var l = new System.Collections.Generic.List<Color>();
        if (primaryPalette != null) l.AddRange(primaryPalette);
        if (secondaryPalette != null) l.AddRange(secondaryPalette);
        return l.ToArray();
    }

    [Header("Roupas da Lu")]
    public Color capColor = Color.white;
    public Color shortsColor = Color.white;
    public Color socksColor = Color.white;

    [Header("Assets 3D")]
    [Tooltip("Modelo 3D da esteira desta fase. Precisa de dois pontos (objetos vazios) chamados PontoLu e PontoFundo e de uma peça chamada Fita. Vazio: usa o quadro simples do prefab.")]
    public GameObject beltPrefab;
    public GameObject productPrefab;
    public GameObject coinPrefab;
    public GameObject finishLinePrefab;
    public GameObject vortexPrefab;
    public GameObject posterUnlockPrefab;
    public AssetEntry[] assets;

    public string Format(string template, int coins, int percent)
    {
        if (string.IsNullOrEmpty(template)) return "";
        return template
            .Replace("{marca}", displayName)
            .Replace("{desconto}", percent.ToString())
            .Replace("{moedas}", coins.ToString());
    }
}
