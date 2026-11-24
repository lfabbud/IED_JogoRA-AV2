using System;
using UnityEngine;

// Altura em que um asset aparece na esteira: encostado no piso ou suspenso na altura do pulo normal / do pulo duplo.
public enum AssetHeight { Floor, Jump, DoubleJump }

// Paleta usada pelos objetos instanciados na esteira (escolhida em cada fase).
// Primary: só a primária. Secondary: só a secundária.
// BothSeparate: as duas, cada objeto usa uma delas inteira (sorteada). BothMixed: as duas misturadas dentro de cada objeto.
public enum ObjectPalette { Primary, Secondary, BothSeparate, BothMixed }

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
        [Tooltip("Marcado: o código escala o modelo por igual, para cima ou para baixo, até caber justo na caixa máxima (Cells Across x Cells Along x altura até o PainelTopo menos a folga) e ocupa só as casas que sobrarem preenchidas. Cells Across e Cells Along viram o máximo de casas. Desmarcado: o modelo mantém o tamanho (só é reduzido se passar) e Cells Across e Along são o espaço reservado.")]
        public bool autoCells = false;
        [Tooltip("Largura: quantas pistas, de 1 a 3. Com Auto Cells, é o máximo que o asset pode preencher.")]
        [Range(1, 3)] public int cellsAcross = 1;
        [Tooltip("Comprimento: quantos módulos, de 1 a 3. Com Auto Cells, é o máximo que o asset pode preencher.")]
        [Range(1, 3)] public int cellsAlong = 1;
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
    [Tooltip("Qual paleta os objetos instanciados na esteira (obstáculos e cenário, nas 5 pistas) usam nesta fase. Primary / Secondary: só uma lista. Both Separate: as duas listas, cada objeto usa uma delas inteira (sorteada). Both Mixed: as duas listas misturadas dentro de cada objeto.")]
    public ObjectPalette paletteForObjects = ObjectPalette.Primary;
    [Tooltip("Cores PRIMÁRIAS (você define). Usadas pelos objetos instanciados quando 'Palette For Objects' = Primary, Both Separate ou Both Mixed. Lista vazia: os objetos mantêm as cores originais do modelo.")]
    [UnityEngine.Serialization.FormerlySerializedAs("palette")]
    public Color[] primaryPalette;
    [Tooltip("Cores SECUNDÁRIAS (você define). Usadas nos confetes e, quando 'Palette For Objects' = Secondary, Both Separate ou Both Mixed, também pelos objetos instanciados. Lista vazia nos confetes: usam a paleta primária.")]
    public Color[] secondaryPalette;

    // Cores de UM objeto instanciado na esteira (chamada uma vez por objeto), conforme 'Palette For Objects', em qualquer das 5 pistas.
    // Both Separate: sorteia a primária ou a secundária para o objeto inteiro (se uma estiver vazia, usa a outra).
    // Both Mixed: junta as duas listas, e as peças do objeto sorteiam entre todas as cores.
    public Color[] ObjectColors()
    {
        bool hasP = primaryPalette != null && primaryPalette.Length > 0;
        bool hasS = secondaryPalette != null && secondaryPalette.Length > 0;
        switch (paletteForObjects)
        {
            case ObjectPalette.Secondary:
                return secondaryPalette;
            case ObjectPalette.BothSeparate:
                if (hasP && hasS) return UnityEngine.Random.value < 0.5f ? primaryPalette : secondaryPalette;
                return hasS ? secondaryPalette : primaryPalette;
            case ObjectPalette.BothMixed:
                if (hasP && hasS)
                {
                    var l = new Color[primaryPalette.Length + secondaryPalette.Length];
                    primaryPalette.CopyTo(l, 0);
                    secondaryPalette.CopyTo(l, primaryPalette.Length);
                    return l;
                }
                return hasS ? secondaryPalette : primaryPalette;
            default:
                return primaryPalette;
        }
    }

    // Cores dos confetes: sempre a paleta SECUNDÁRIA (se estiver vazia, a primária).
    public Color[] ConfettiColors()
    {
        if (secondaryPalette != null && secondaryPalette.Length > 0) return secondaryPalette;
        return primaryPalette;
    }

    [Header("Roupas da Lu")]
    public Color capColor = Color.white;
    public Color shortsColor = Color.white;
    public Color socksColor = Color.white;

    [Header("Assets 3D")]
    [Tooltip("Modelo 3D da esteira desta fase. Precisa de dois pontos (objetos vazios) chamados PontoLu e PontoFundo e de uma peça chamada Fita. Vazio: usa o quadro simples do prefab.")]
    public GameObject beltPrefab;
    public GameObject productPrefab;
    [Tooltip("Tamanho do produto no quadro da tela de vitória. 1 = enquadramento padrão; maior que 1 deixa o produto maior; menor que 1 deixa menor. Não altera o prefab.")]
    [Min(0.1f)] public float productViewScale = 1f;
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
