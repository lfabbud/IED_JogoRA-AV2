using System;
using UnityEngine;

// Altura em que um asset aparece na esteira: encostado no piso ou suspenso na altura do pulo normal / do pulo duplo.
public enum AssetHeight { Floor, Jump, DoubleJump, Random }

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
        [Tooltip("Marcado: só aparece nas trilhas 0 e 4 (cenário, sem colisão). Desmarcado: obstáculo nas trilhas 1 a 3.")]
        public bool onlyEdgeLanes = false;
        [Range(1, 3)] public int cellsAcross = 1;
        [Range(1, 3)] public int cellsAlong = 1;
        [Tooltip("Obstáculo baixo que dá para pular. Desmarcado: Lu precisa trocar de pista.")]
        public bool canBeJumped = true;
        public AssetHeight height = AssetHeight.Floor;
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
    [TextArea] public string noRewardMessage = "Você coletou {moedas} moedas. Tente de novo para ganhar um desconto!";

    [Header("Cores")]
    public Color themeColor = new Color(0.08f, 0.08f, 0.12f, 1f);
    public Color accentColor = new Color(0.76f, 0.55f, 0.86f, 1f);
    public Color trackColor = Color.white;
    [Tooltip("Cores sorteadas para pintar as peças de cada asset (obstáculos e cenário) e os confetes.")]
    public Color[] palette;

    [Header("Assets 3D")]
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
