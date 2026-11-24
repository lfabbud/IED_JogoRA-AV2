using System.Collections.Generic;
using UnityEngine;

// Fica na raiz do prefab da esteira. Desliza a esteira sem parar, cria os módulos (fileiras transversais de 5 casas:
// 2 de cenário nas pontas e 3 de ação no meio), as moedas e a linha de chegada, e move todos os itens pelo percurso
// (curva distante > trecho reto > fade out depois da casa da Lu). Tudo em coordenadas locais.
// Eixos locais: +Z = lado de onde vêm os itens (horizonte); X = trilhas 0 a 4 (centro = 2); Lu fica em Z = 0.
public class TrackRoot : MonoBehaviour, IPhasePreview
{
    [Header("Referências (dentro do prefab)")]
    public Renderer beltRenderer;
    public LaneController lu;
    public Animator luAnimator;
    public Transform headAnchor;

    public bool FinishSpawned { get; private set; }
    public bool FinishCrossed { get; private set; }
    public float CurrentSpeed { get { return s == null ? 0f : s.beltSpeed * speedMul * stopFactor; } }
    public int ModuleIndex { get { return moduleIndex; } }
    public int ItemCount { get { return items.Count; } }

    GameSettings s;
    LevelConfig level;
    Material beltMat;
    GameObject beltModel;
    float beltTile = 2f;
    static readonly HashSet<string> warned = new HashSet<string>();
    static readonly List<Quaternion> validRots = new List<Quaternion>();
    static readonly List<float> validScales = new List<float>();
    static Quaternion[] rots24;
    readonly List<BeltItem> items = new List<BeltItem>();
    readonly List<LevelConfig.AssetEntry> obstacleEntries = new List<LevelConfig.AssetEntry>();
    readonly List<LevelConfig.AssetEntry> edgeEntries = new List<LevelConfig.AssetEntry>();

    // Reservas por trilha (0 a 4): último módulo ocupado.
    readonly int[] blockedUntil = new int[5];
    readonly int[] coinBusyUntil = new int[5];
    // Deslocamento de cada pista (0 a 4) ao longo do percurso, sorteado no início de cada jogo (GameSettings.lateralMisalignment).
    readonly float[] laneShift = new float[5];
    // Registros do que já foi criado, na medida do percurso (módulo + posição sorteada). Servem para nenhum obstáculo
    // ficar alinhado com outro, nenhuma moeda ficar alinhada com outra e moeda e obstáculo não se sobreporem na mesma pista.
    struct ObstacleRec { public float key; public int c0, c1, along; }
    struct CoinRec { public float key; public int col; }
    readonly List<ObstacleRec> obstacleRecs = new List<ObstacleRec>();
    readonly List<CoinRec> coinRecs = new List<CoinRec>();
    // Por pista (0 a 4): quantos obstáculos foram criados em outras pistas desde o último obstáculo desta pista.
    readonly int[] laneSince = new int[5];
    int jumpShiftCol = 2;

    float L;
    float maxAssetHeight = 2f;
    int coinHeightPick = -1;
    // Altura da base do asset que está sendo criado (0 = piso). O espaço em altura do asset é o limite menos esta base.
    float planBaseY = 0f;
    // Asset no ar só nasce se couber reduzido a no mínimo esta fração do tamanho que teria no piso; senão fica no piso.
    const float minAirScale = 0.6f;
    // Asset no ar ocupa no máximo esta quantidade de pistas na largura, para sempre sobrar passagem pelas laterais.
    const int maxAirAcross = 2;
    // Altura máxima usada de fato: o vão até o PainelTopo menos a folga (Max Height Margin Percent).
    float UsableHeight { get { return maxAssetHeight * (1f - Mathf.Clamp(s.maxHeightMarginPercent, 0f, 50f) * 0.01f); } }
    float flatEnd;
    float arc;
    float fadeLen;
    float travel;
    float beltOffset;
    int moduleIndex;
    int lastObstacleEnd;
    int lastEscape = -1;
    int jumpCol0, jumpWidth, jumpModule;
    float jumpRel;

    int coinsSpawned;
    int coinIdx;
    readonly List<float> coinTimes = new List<float>();
    float curveR;
    bool showBoxes;
    GameObject limitsObj;

    float speedMul = 1f;
    float stopFactor = 1f;
    float decelRate;
    bool decelerating;

    const string PreviewName = "[Pré-visualização]";

    // Onde nascem a esteira e os itens. No jogo é o próprio TrackRoot; na pré-visualização é um objeto temporário que não é salvo.
    Transform Holder
    {
        get
        {
            if (Application.isPlaying) return transform;
            var h = transform.Find(PreviewName);
            return h != null ? h : transform;
        }
    }

    static void Kill(UnityEngine.Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    // Cor sem criar cópia do material (vale fora do Play): vai num MaterialPropertyBlock, só na tela.
    static void PaintRenderer(Renderer r, Color c)
    {
        var pb = new MaterialPropertyBlock();
        int n = Mathf.Max(1, r.sharedMaterials.Length);
        for (int k = 0; k < n; k++)
        {
            r.GetPropertyBlock(pb, k);
            pb.SetColor("_BaseColor", c);
            pb.SetColor("_Color", c);
            r.SetPropertyBlock(pb, k);
        }
    }

    // Pré-visualização na Scene: esteira 3D da fase com as cores, e uma amostra de cada obstáculo (nas pistas 1 a 3),
    // da moeda e da linha de chegada, em fileiras diante da Lu. Nada disso é salvo na cena nem no prefab.
    public void ApplyPreview(LevelConfig lvl)
    {
        if (Application.isPlaying || lvl == null) return;
        var settings = PhasePreview.Settings();
        if (settings == null)
        {
            Debug.LogWarning("[Lu] Pré-visualização: não encontrei um Level Catalog com GameSettings no projeto.");
            return;
        }
        ClearPreview();
        var holder = new GameObject(PreviewName);
        holder.transform.SetParent(transform, false);
        Init(settings, lvl);
        for (int i = 0; i < 5; i++) laneShift[i] = 0f;
        BuildPreviewSamples();
        PhasePreview.MarkTemporary(holder);
    }

    void BuildPreviewSamples()
    {
        float w = s.laneWidth;
        float z = 2f * L;
        float rowDepth = 0f;
        int n = 0;
        if (level.assets != null)
        {
            foreach (var e in level.assets)
            {
                if (e == null || e.prefab == null) continue;
                hasPlan = false;
                planBaseY = 0f;
                if (z > flatEnd - L) break;
                int col = 1 + (n % 3);
                int along = Mathf.Max(1, Along(e));
                SpawnItem(e.prefab, BeltKind.Obstacle, (col - 2) * w, 0f, arc + flatEnd - z, along * L * 0.5f, false, e);
                rowDepth = Mathf.Max(rowDepth, along);
                if (n % 3 == 2)
                {
                    z += (rowDepth + 0.5f) * L;
                    rowDepth = 0f;
                }
                n++;
            }
            if (n % 3 != 0) z += (rowDepth + 0.5f) * L;
        }
        if (level.coinPrefab != null)
            SpawnItem(level.coinPrefab, BeltKind.Coin, 0f, s.coinHeight, arc + flatEnd - L, L * 0.5f, false);
        if (level.finishLinePrefab != null && z <= flatEnd - L)
        {
            SpawnFinish();
            var f = items[items.Count - 1];
            f.s = arc + flatEnd - z;
            UpdateItem(f);
        }
    }

    public void ClearPreview()
    {
        if (Application.isPlaying) return;
        var h = transform.Find(PreviewName);
        if (h != null) DestroyImmediate(h.gameObject);
        items.Clear();
        beltModel = null;
        beltMat = null;
        if (limitsObj != null) DestroyImmediate(limitsObj);
        limitsObj = null;
        if (beltRenderer != null)
        {
            beltRenderer.forceRenderingOff = false;
            beltRenderer.SetPropertyBlock(null);
        }
    }

    public void Init(GameSettings settings, LevelConfig levelConfig)
    {
        s = settings;
        level = levelConfig;
        ClearAll();

        L = Mathf.Max(0.1f, s.moduleDepth);
        flatEnd = s.spawnDistance;
        curveR = Mathf.Max(0.1f, s.curveRadius);
        arc = Mathf.PI * 0.5f * curveR;
        fadeLen = Mathf.Max(0.01f, s.fadeCells * L);
        travel = L;
        maxAssetHeight = Mathf.Max(0.05f, s.fallbackMaxAssetHeight);
        autoSizeCache.Clear();
        autoLogged.Clear();
        hasPlan = false;

        float nearZ = -(0.5f * L + fadeLen);
        float length = flatEnd - nearZ;
        beltTile = Mathf.Max(0.01f, s.beltTextureTileLength);
        if (beltModel != null) Kill(beltModel);
        beltModel = null;
        beltMat = null;
        if (level.beltPrefab != null) BuildBeltModel();
        else if (!Application.isPlaying)
        {
            beltRenderer.forceRenderingOff = false;
            PaintRenderer(beltRenderer, level.trackColor);
        }
        else
        {
            beltRenderer.enabled = true;
            beltRenderer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            beltRenderer.transform.localScale = new Vector3(s.laneWidth * 5f, length, 1f);
            beltRenderer.transform.localPosition = new Vector3(0f, 0f, (flatEnd + nearZ) * 0.5f);
            beltMat = beltRenderer.material;
            beltMat.color = level.trackColor;
            beltMat.mainTextureScale = new Vector2(1f, length / beltTile);
        }

        obstacleEntries.Clear();
        edgeEntries.Clear();
        if (level.assets != null)
        {
            foreach (var e in level.assets)
            {
                if (e == null || e.prefab == null) continue;
                obstacleEntries.Add(e);
            }
        }

        // Moedas: cada uma nasce num instante sorteado entre o início e o fim da corrida (frequência e posição aleatórias).
        for (int i = 0; i < 5; i++) laneShift[i] = 0f;
        coinRecs.Clear();
        obstacleRecs.Clear();
        ResetLaneSince();
        coinIdx = 0;
        coinTimes.Clear();
        float t0 = s.coinStartDelaySeconds;
        float t1 = Mathf.Max(t0 + 1f, s.runDurationSeconds - s.coinTailSeconds);
        for (int i = 0; i < Mathf.Max(0, s.maxCoins); i++) coinTimes.Add(Random.Range(t0, t1));
        coinTimes.Sort();
        RefreshLimits();
    }

    // Esteira 3D da fase: entra sem mudar de tamanho. PontoLu fica na posição da Lu (em cima da fita) e
    // PontoFundo indica o lado do horizonte (+Z). A peça "Fita" recebe a cor da pista e a textura rolando.
    void BuildBeltModel()
    {
        if (Application.isPlaying) beltRenderer.enabled = false;
        else beltRenderer.forceRenderingOff = true;
        beltModel = Instantiate(level.beltPrefab, Holder);
        var bt = beltModel.transform;
        // A rotação do raiz do prefab (por exemplo, -90 em X vinda do FBX do Blender) é mantida; só a posição volta a zero.
        Quaternion baseRot = bt.localRotation;
        bt.localPosition = Vector3.zero;
        Transform pl = FindDeep(bt, "PontoLu");
        Transform pf = FindDeep(bt, "PontoFundo");
        if (pl != null && pf != null)
        {
            Vector3 d = transform.InverseTransformPoint(pf.position) - transform.InverseTransformPoint(pl.position);
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) bt.localRotation = Quaternion.Euler(0f, Vector3.SignedAngle(d, Vector3.forward, Vector3.up), 0f) * baseRot;
            bt.localPosition = -transform.InverseTransformPoint(pl.position);
        }
        else Warn("belt-points", "Esteira " + level.displayName + ": faltam os objetos PontoLu e/ou PontoFundo. A esteira ficou como veio do modelo.");

        // Os itens nascem no PontoFundo (ponta da Fita, lado do horizonte). O raio da curva é metade da altura da Fita.
        Transform fita0 = FindDeep(bt, "Fita");
        Bounds fb0;
        if (fita0 != null && BeltItem.LocalBounds(transform, fita0, out fb0) && fb0.size.y >= 0.05f) curveR = fb0.size.y * 0.5f;
        arc = Mathf.PI * 0.5f * curveR;
        if (pl != null && pf != null)
        {
            flatEnd = transform.InverseTransformPoint(pf.position).z - curveR;
            if (flatEnd < 1f) Warn("belt-end", "Esteira " + level.displayName + ": o PontoFundo está muito perto da Lu; confira a posição dele no modelo.");
        }
        PaintBeltParts(bt);

        // Altura máxima dos assets = vão entre a superfície da esteira (PontoLu, altura 0) e a parte mais baixa do PainelTopo.
        Transform painel = FindDeep(bt, "PainelTopo");
        Bounds pb;
        if (painel != null && BeltItem.LocalBounds(transform, painel, out pb) && pb.min.y > 0.05f)
        {
            maxAssetHeight = pb.min.y;
            if (Application.isPlaying) Debug.Log("[Lu] Altura máxima dos assets: vão até o PainelTopo " + maxAssetHeight.ToString("0.00") + ", com a folga " + UsableHeight.ToString("0.00") + ".");
        }
        else Warn("belt-panel", "Esteira " + level.displayName + ": não consegui medir o PainelTopo; a altura máxima dos assets é Fallback Max Asset Height (" + maxAssetHeight.ToString("0.00") + ").");

        Transform fita = FindDeep(bt, "Fita");
        if (fita == null)
        {
            Warn("belt-fita", "Esteira " + level.displayName + ": não achei a peça chamada Fita (sem cor nem movimento).");
            return;
        }
        var fr = fita.GetComponent<Renderer>();
        if (fr == null) fr = fita.GetComponentInChildren<Renderer>();
        if (fr != null)
        {
            if (Application.isPlaying)
            {
                beltMat = fr.material;
                beltMat.color = level.trackColor;
            }
            else
            {
                beltMat = fr.sharedMaterial;
                PaintRenderer(fr, level.trackColor);
            }
            beltTile = 1f;
            Bounds fb;
            if (BeltItem.LocalBounds(transform, fita, out fb))
            {
                // Cinta em volta dos dois rolos (pista de corrida): o perímetro é 2 trechos retos + 2 meias-voltas.
                // Fita só plana (altura quase zero): vale o comprimento.
                float h = fb.size.y;
                float perim = h < 0.05f ? fb.size.z : 2f * Mathf.Max(0f, fb.size.z - h) + Mathf.PI * h;
                beltTile = Mathf.Max(0.01f, perim / Mathf.Max(0.01f, beltMat.mainTextureScale.y));
                if (Application.isPlaying) Debug.Log("[Lu] Fita: perímetro " + perim.ToString("0.00") + ", repetições da textura na volta " + beltMat.mainTextureScale.y.ToString("0.##") + ", cada repetição mede " + beltTile.ToString("0.000") + ", textura: " + (beltMat.mainTexture != null ? beltMat.mainTexture.name : "nenhuma") + ".");
                if (Mathf.Abs(fb.size.x - s.laneWidth * 5f) > 0.05f * s.laneWidth * 5f)
                    Warn("belt-width", "Esteira " + level.displayName + ": a Fita tem " + fb.size.x.ToString("0.00") + " de largura; as 5 pistas pedem " + (s.laneWidth * 5f).ToString("0.00") + ".");
            }
        }
    }

    // Corpo e painel da esteira 3D: cada peça recebe a cor da fase (a Fita tem a sua, tratada abaixo).
    void PaintBeltParts(Transform root)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (IsUnder(r.transform, "Fita", root)) continue;
            Color c = IsUnder(r.transform, "PainelTopo", root) ? level.panelColor : level.bodyColor;
            if (!Application.isPlaying)
            {
                PaintRenderer(r, c);
                continue;
            }
            var ms = r.materials;
            foreach (var m in ms)
            {
                if (m == null) continue;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                else if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            }
        }
    }

    static bool IsUnder(Transform t, string name, Transform root)
    {
        for (var x = t; x != null && x != root.parent; x = x.parent)
            if (x.name == name) return true;
        return false;
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }
        return null;
    }

    static void Warn(string key, string msg)
    {
        if (warned.Add(key)) Debug.LogWarning("[Lu] " + msg);
    }

    // As 24 orientações possíveis (giros de 90 graus nos 3 eixos). A primeira é a original.
    static Quaternion[] Rotations24()
    {
        if (rots24 != null) return rots24;
        var list = new List<Quaternion>();
        var seen = new HashSet<string>();
        for (int x = 0; x < 4; x++)
            for (int y = 0; y < 4; y++)
                for (int z = 0; z < 4; z++)
                {
                    var q = Quaternion.Euler(x * 90f, y * 90f, z * 90f);
                    Vector3 a = q * Vector3.right;
                    Vector3 b = q * Vector3.up;
                    string key = Mathf.RoundToInt(a.x) + "," + Mathf.RoundToInt(a.y) + "," + Mathf.RoundToInt(a.z) + "|" +
                                 Mathf.RoundToInt(b.x) + "," + Mathf.RoundToInt(b.y) + "," + Mathf.RoundToInt(b.z);
                    if (seen.Add(key)) list.Add(q);
                }
        rots24 = list.ToArray();
        return rots24;
    }

    static Vector3 FaceVector(AssetFace f)
    {
        switch (f)
        {
            case AssetFace.Up: return Vector3.up;
            case AssetFace.Left: return Vector3.left;
            case AssetFace.Right: return Vector3.right;
            case AssetFace.Back: return Vector3.back;
            case AssetFace.Front: return Vector3.forward;
            default: return Vector3.down;
        }
    }

    static Vector3 RotatedSize(Quaternion q, Vector3 size)
    {
        Matrix4x4 m = Matrix4x4.Rotate(q);
        return new Vector3(
            Mathf.Abs(m.m00) * size.x + Mathf.Abs(m.m01) * size.y + Mathf.Abs(m.m02) * size.z,
            Mathf.Abs(m.m10) * size.x + Mathf.Abs(m.m11) * size.y + Mathf.Abs(m.m12) * size.z,
            Mathf.Abs(m.m20) * size.x + Mathf.Abs(m.m21) * size.y + Mathf.Abs(m.m22) * size.z);
    }

    // Largura reservada para o asset, em unidades locais: de 1 a 3 pistas (cenário das pontas: 1 pista).
    // ---------- espaço reservado (Cells): manual ou automático (Auto Cells) ----------

    // Auto Cells: o código escala o modelo por igual (para cima ou para baixo) até caber justo na caixa máxima e conta
    // as casas que ficaram preenchidas. O plano (orientação, escala e casas) é sorteado uma vez por asset criado.
    struct AutoPlan { public LevelConfig.AssetEntry entry; public Quaternion q; public float scale; public int across, along; }
    AutoPlan plan;
    bool hasPlan;
    int lastAcross = 1, lastAlong = 1;
    readonly Dictionary<LevelConfig.AssetEntry, Vector3> autoSizeCache = new Dictionary<LevelConfig.AssetEntry, Vector3>();
    readonly HashSet<LevelConfig.AssetEntry> autoLogged = new HashSet<LevelConfig.AssetEntry>();

    // Tamanho do modelo (largura, altura, comprimento) na orientação do prefab (com a rotação e a escala da raiz).
    bool MeasureModel(LevelConfig.AssetEntry e, out Vector3 size)
    {
        if (!autoSizeCache.TryGetValue(e, out size))
        {
            size = Vector3.zero;
            if (e.prefab != null)
            {
                var root = new GameObject("MedirAsset");
                root.SetActive(false);
                root.transform.SetParent(transform, false);
                // A rotação e a escala da raiz do prefab são mantidas: o tamanho medido já é o do asset na posição em que você o deixou no prefab.
                var inst = Instantiate(e.prefab, root.transform);
                inst.transform.localPosition = Vector3.zero;
                Bounds b;
                if (BeltItem.LocalBounds(root.transform, inst.transform, out b)) size = b.size;
                if (Application.isPlaying) Destroy(root); else DestroyImmediate(root);
            }
            autoSizeCache[e] = size;
        }
        return size.x > 0.0001f && size.y > 0.0001f && size.z > 0.0001f;
    }

    void EnsurePlan(LevelConfig.AssetEntry e)
    {
        if (!hasPlan || plan.entry != e) PlanAuto(e);
    }

    void PlanAuto(LevelConfig.AssetEntry e)
    {
        hasPlan = true;
        int maxA = Mathf.Clamp(e.cellsAcross, 1, 3);
        if (planBaseY > 0.001f) maxA = Mathf.Min(maxA, maxAirAcross);
        int maxB = Mathf.Clamp(e.cellsAlong, 1, 3);
        plan = new AutoPlan { entry = e, q = Quaternion.identity, scale = 1f, across = maxA, along = maxB };
        Vector3 size;
        if (!MeasureModel(e, out size)) return;

        Vector3 box = new Vector3(maxA * s.laneWidth, Mathf.Max(0.05f, UsableHeight - planBaseY), maxB * L);
        const float tol = 0.05f;
        bool consider = e.randomRotation || e.lockFloor || e.lockFront;
        var all = Rotations24();
        int n = consider ? all.Length : 1;
        var cands = new List<AutoPlan>();
        for (int i = 0; i < n; i++)
        {
            var q = all[i];
            if (e.lockFloor && Vector3.Dot(q * FaceVector(e.floorFace), Vector3.down) < 0.99f) continue;
            if (e.lockFront && Vector3.Dot(q * FaceVector(e.frontFace), Vector3.back) < 0.99f) continue;
            Vector3 sz = RotatedSize(q, size);
            // Maior escala uniforme que ainda cabe na caixa máxima: a medida que bate no limite encosta nele.
            float f = Mathf.Min(box.x / sz.x, Mathf.Min(box.y / sz.y, box.z / sz.z));
            int a = Mathf.Clamp(Mathf.CeilToInt(sz.x * f / s.laneWidth - tol), 1, maxA);
            int b = Mathf.Clamp(Mathf.CeilToInt(sz.z * f / L - tol), 1, maxB);
            b = Mathf.Min(maxB, Mathf.Max(b, a));
            cands.Add(new AutoPlan { entry = e, q = q, scale = f, across = a, along = b });
        }
        if (cands.Count == 0) return;
        plan = cands[e.randomRotation ? Random.Range(0, cands.Count) : 0];
        if (autoLogged.Add(e) && Application.isPlaying)
            Debug.Log("[Lu] Auto Cells " + (e.prefab != null ? e.prefab.name : "?") + ": modelo " + size.x.ToString("0.00") + " x " + size.y.ToString("0.00") + " x " + size.z.ToString("0.00") +
                ", caixa máxima " + box.x.ToString("0.00") + " x " + box.y.ToString("0.00") + " x " + box.z.ToString("0.00") + " => escala " + plan.scale.ToString("0.00") +
                ", Across " + plan.across + ", Along " + plan.along + ".");
    }

    int Across(LevelConfig.AssetEntry e)
    {
        if (!e.autoCells)
        {
            int a = Mathf.Clamp(e.cellsAcross, 1, 3);
            return planBaseY > 0.001f ? Mathf.Min(a, maxAirAcross) : a;
        }
        EnsurePlan(e);
        return plan.across;
    }

    int Along(LevelConfig.AssetEntry e)
    {
        if (!e.autoCells) return Mathf.Max(1, e.cellsAlong);
        EnsurePlan(e);
        return plan.along;
    }

    float AssetWidth(LevelConfig.AssetEntry e)
    {
        int across = Mathf.Clamp(Across(e), 1, 3);
        return across * s.laneWidth;
    }

    // Escolhe a orientação do asset: respeita as travas e só aceita as que cabem no espaço reservado
    // (largura x altura x comprimento, em caixas). Se passar da largura das pistas, o asset é reduzido por igual
    // (escala uniforme, só quando passa): scale devolve o fator (1 = tamanho original). Se nada couber, só avisa.
    Quaternion PickRotation(LevelConfig.AssetEntry e, Vector3 size, out float scale)
    {
        var dom = new Vector3(
            AssetWidth(e),
            Mathf.Max(0.05f, UsableHeight - planBaseY),
            Mathf.Max(1, Along(e)) * L);
        const float eps = 0.02f;
        bool consider = e.randomRotation || e.lockFloor || e.lockFront;
        var all = Rotations24();
        int n = consider ? all.Length : 1;
        validRots.Clear();
        validScales.Clear();
        Quaternion lockOnly = Quaternion.identity;
        float lockOnlyScale = 1f;
        bool haveLockOnly = false;
        for (int i = 0; i < n; i++)
        {
            var q = all[i];
            if (e.lockFloor && Vector3.Dot(q * FaceVector(e.floorFace), Vector3.down) < 0.99f) continue;
            if (e.lockFront && Vector3.Dot(q * FaceVector(e.frontFace), Vector3.back) < 0.99f) continue;
            Vector3 sz = RotatedSize(q, size);
            float f = sz.x > dom.x ? dom.x / sz.x : 1f;
            if (sz.y * f > dom.y) f = dom.y / sz.y;
            if (!haveLockOnly) { lockOnly = q; lockOnlyScale = f; haveLockOnly = true; }
            if (sz.y * f <= dom.y + eps && sz.z * f <= dom.z + eps)
            {
                validRots.Add(q);
                validScales.Add(f);
            }
        }
        if (validRots.Count > 0)
        {
            int k = e.randomRotation ? Random.Range(0, validRots.Count) : 0;
            scale = validScales[k];
            return validRots[k];
        }

        string nm = e.prefab != null ? e.prefab.name : "?";
        Warn("fit-" + nm, "Asset " + nm + ": não cabe no espaço reservado (" + dom.x.ToString("0.00") + " x " + dom.y.ToString("0.00") + " x " + dom.z.ToString("0.00") +
            ") com as travas escolhidas; o modelo mede " + size.x.ToString("0.00") + " x " + size.y.ToString("0.00") + " x " + size.z.ToString("0.00") +
            ". A largura é reduzida para caber nas pistas e a altura para no máximo " + UsableHeight.ToString("0.00") + " (vão até o PainelTopo menos a folga); ajuste as caixas ou o modelo.");
        float fb = size.x > dom.x ? dom.x / size.x : 1f;
        if (size.y * fb > dom.y) fb = dom.y / size.y;
        scale = haveLockOnly ? lockOnlyScale : fb;
        return haveLockOnly ? lockOnly : Quaternion.identity;
    }

    // Cria o asset já na orientação escolhida e, se passar da largura das pistas, reduzido por igual.
    // Recentraliza para o conjunto ficar no centro das casas e com a base no piso.
    GameObject BuildOriented(LevelConfig.AssetEntry e)
    {
        var root = new GameObject(e.prefab.name);
        root.transform.SetParent(Holder, false);
        var inst = Instantiate(e.prefab, root.transform);
        // O pivô (gizmo de posição) do prefab é o ponto de referência: ele fica no centro das casas, no piso, e o asset gira e escala em torno dele.
        // A rotação e a escala da raiz do prefab são mantidas (baseRot). As travas, o sorteio de orientação e o Auto Cells giram o asset por cima delas.
        inst.transform.localPosition = Vector3.zero;
        Quaternion baseRot = inst.transform.localRotation;
        Bounds b;
        if (!BeltItem.LocalBounds(root.transform, inst.transform, out b)) { hasPlan = false; return root; }
        Quaternion q;
        float f;
        if (e.autoCells)
        {
            // Auto Cells: orientação, escala (para cima ou para baixo) e casas vêm do plano sorteado para este asset.
            EnsurePlan(e);
            q = plan.q;
            f = plan.scale;
            lastAcross = plan.across;
            lastAlong = plan.along;
            hasPlan = false;
        }
        else
        {
            lastAcross = Mathf.Clamp(Across(e), 1, 3);
            lastAlong = Mathf.Max(1, Along(e));
            q = PickRotation(e, b.size, out f);
        }
        if (f < 0.9999f || e.autoCells) inst.transform.localScale = inst.transform.localScale * f;
        inst.transform.localRotation = q * baseRot;
        // Posição: o pivô fica na origem (centro das casas). Só a altura é corrigida quando a orientação muda: o ponto mais baixo do asset
        // fica onde estava no prefab (se o pivô está na base, o asset gira e continua apoiado no piso).
        float minY0 = b.center.y - b.size.y * 0.5f;
        Vector3 cq = q * b.center;
        Vector3 sq = RotatedSize(q, b.size);
        float minYq = cq.y - sq.y * 0.5f;
        inst.transform.localPosition = new Vector3(0f, (minY0 - minYq) * f, 0f);

        // Limite: o pivô manda, mas se o asset ficou (mesmo em parte) fora da caixa reservada, ele é deslocado para dentro (sem escalar).
        // Largura e comprimento: dentro das casas (origem = centro das casas). Altura: ponto mais baixo no piso ou acima, topo até o limite.
        Bounds nb;
        if (BeltItem.LocalBounds(root.transform, inst.transform, out nb))
        {
            float halfW = Mathf.Clamp(lastAcross, 1, 3) * s.laneWidth * 0.5f;
            float halfD = Mathf.Max(1, lastAlong) * L * 0.5f;
            float avail = Mathf.Max(0.05f, UsableHeight - planBaseY);
            Vector3 shift = Vector3.zero;
            shift.x = ShiftInto(nb.min.x, nb.max.x, -halfW, halfW);
            shift.z = ShiftInto(nb.min.z, nb.max.z, -halfD, halfD);
            if (nb.min.y < 0f) shift.y = -nb.min.y;
            else if (nb.max.y > avail) shift.y = -Mathf.Min(nb.max.y - avail, nb.min.y);
            if (shift != Vector3.zero) inst.transform.localPosition += shift;
        }
        return root;
    }

    // Deslocamento mínimo para o trecho [lo, hi] caber em [boxLo, boxHi]. Se o trecho for maior que a caixa, centraliza.
    static float ShiftInto(float lo, float hi, float boxLo, float boxHi)
    {
        const float tol = 0.001f;
        if (hi - lo > boxHi - boxLo) return (boxLo + boxHi) * 0.5f - (lo + hi) * 0.5f;
        if (lo < boxLo - tol) return boxLo - lo;
        if (hi > boxHi + tol) return boxHi - hi;
        return 0f;
    }

    // Apaga tudo e volta ao estado inicial (usado ao tentar de novo, sem mexer na posição da esteira).
    public void ClearAll()
    {
        foreach (var it in items)
        {
            if (it != null) Kill(it.gameObject);
        }
        items.Clear();
        foreach (var c in GetComponentsInChildren<ConfettiBurst>(true)) Kill(c.gameObject);

        for (int i = 0; i < 5; i++)
        {
            blockedUntil[i] = -1;
            coinBusyUntil[i] = -1;
        }
        FinishSpawned = false;
        FinishCrossed = false;
        moduleIndex = 0;
        lastObstacleEnd = -1000;
        lastEscape = -1;
        jumpModule = -1000;
        coinsSpawned = 0;
        coinHeightPick = -1;
        planBaseY = 0f;
        coinRecs.Clear();
        obstacleRecs.Clear();
        ResetLaneSince();
        speedMul = 1f;
        stopFactor = 1f;
        decelerating = false;
        beltOffset = 0f;
        travel = L > 0f ? L : 1f;
    }

    public void StopBelt()
    {
        stopFactor = 0f;
        decelerating = false;
    }

    // A esteira desacelera até parar em "seconds" segundos.
    public void DecelerateAndStop(float seconds)
    {
        decelerating = true;
        decelRate = stopFactor / Mathf.Max(0.05f, seconds);
    }

    public void Tick(float dt, float elapsed, bool spawning)
    {
        UpdateSpeed(dt, elapsed);
        float dz = s.beltSpeed * speedMul * stopFactor * dt;
        travel += dz;

        for (int i = items.Count - 1; i >= 0; i--)
        {
            var it = items[i];
            if (it == null)
            {
                items.RemoveAt(i);
                continue;
            }
            it.s += dz;
            if (it.spinSpeed != 0f) it.Spin(dt);
            if (!UpdateItem(it))
            {
                Destroy(it.gameObject);
                items.RemoveAt(i);
                continue;
            }
            if (it.kind == BeltKind.Finish && it.transform.localPosition.z <= 0f) FinishCrossed = true;
        }

        if (beltMat != null)
        {
            beltOffset += dz / beltTile * s.beltScrollDirection;
            float o = Mathf.Repeat(beltOffset, 1f);
            beltMat.mainTextureOffset = s.beltScrollOnU ? new Vector2(o, 0f) : new Vector2(0f, o);
        }

        if (!spawning) return;
        while (travel >= L)
        {
            travel -= L;
            SpawnModule(elapsed);
        }
    }

    // +X% a cada X segundos, em rampa curta (valores em GameSettings).
    void UpdateSpeed(float dt, float elapsed)
    {
        float target = 1f;
        if (s.speedIncreaseEverySeconds > 0.01f && s.speedIncreasePercent > 0f)
        {
            int steps = Mathf.FloorToInt(elapsed / s.speedIncreaseEverySeconds);
            target = Mathf.Pow(1f + s.speedIncreasePercent / 100f, steps);
        }
        if (speedMul < target)
        {
            float rate = (s.speedIncreasePercent / 100f) * target / Mathf.Max(0.05f, s.speedRampSeconds);
            speedMul = Mathf.Min(target, speedMul + rate * dt);
        }
        else
        {
            speedMul = target;
        }
        if (decelerating) stopFactor = Mathf.Max(0f, stopFactor - decelRate * dt);
    }

    // Posiciona o item pelo percurso. Devolve false quando ele já sumiu (fade out terminado).
    bool UpdateItem(BeltItem it)
    {
        float R = curveR;
        float z;
        float y;
        Quaternion rot;
        if (it.s < arc)
        {
            // Curva distante: o item sobe pelo rolo, girando em torno do eixo X (transversal).
            float a = Mathf.PI * 0.5f - it.s / R;
            float sin = Mathf.Sin(a);
            float cos = Mathf.Cos(a);
            z = flatEnd + (R + it.baseY) * sin;
            y = (R + it.baseY) * cos - R;
            rot = Quaternion.Euler(a * Mathf.Rad2Deg, 0f, 0f);
        }
        else
        {
            z = flatEnd - (it.s - arc);
            y = it.baseY;
            rot = Quaternion.identity;
        }

        float alpha = Mathf.Clamp01(it.s / fadeLen);
        float fadeOutStart = -0.5f * L - it.halfDepth;
        if (z < fadeOutStart)
        {
            float outAlpha = 1f - (fadeOutStart - z) / fadeLen;
            if (outAlpha <= 0f) return false;
            alpha = Mathf.Min(alpha, outAlpha);
        }

        if (it.spinSpeed != 0f) rot = rot * it.SpinRotation;
        it.transform.localPosition = new Vector3(it.x, y, z);
        it.transform.localRotation = rot;
        it.SetAlpha(alpha);
        return true;
    }

    BeltItem SpawnItem(GameObject prefab, BeltKind kind, float x, float y, float sPos, float halfDepth, bool canJump, LevelConfig.AssetEntry entry = null, int shiftCol = -1)
    {
        if (shiftCol < 0) shiftCol = ColOf(x);
        sPos -= laneShift[Mathf.Clamp(shiftCol, 0, 4)];
        GameObject go;
        if (entry != null) go = BuildOriented(entry);
        else if (kind == BeltKind.Coin) go = BuildCoin(prefab);
        else go = Instantiate(prefab, Holder);
        var it = go.AddComponent<BeltItem>();
        it.kind = kind;
        it.x = x;
        it.baseY = y;
        it.s = sPos;
        it.halfDepth = halfDepth;
        it.canJump = canJump;
        if (entry != null)
            it.gizmoBox = new Vector3(lastAcross * s.laneWidth, Mathf.Max(0.05f, UsableHeight - planBaseY), lastAlong * L);
        it.Setup();
        if (showBoxes) it.SetBoxVisible(true);
        if (kind == BeltKind.Obstacle || kind == BeltKind.Scenery) it.Paint(level.ObjectColors());
        if (kind == BeltKind.Obstacle) it.BuildTrigger(1f - s.obstacleTolerance);
        else if (kind == BeltKind.Coin)
        {
            it.BuildTrigger(1f + s.coinTolerance);
            it.spinSpeed = 180f;
        }
        else it.DisableColliders();
        items.Add(it);
        UpdateItem(it);
        return it;
    }

    int ColOf(float x)
    {
        return Mathf.Clamp(Mathf.RoundToInt(x / Mathf.Max(0.0001f, s.laneWidth)) + 2, 0, 4);
    }

    // Moeda em pé e girando: a face do disco (o eixo mais fino do modelo) fica virada para a frente da esteira (eixo Z),
    // com o centro do disco na origem do item; o giro é em torno do eixo vertical.
    GameObject BuildCoin(GameObject prefab)
    {
        var holder = new GameObject("Moeda");
        holder.transform.SetParent(Holder, false);
        holder.transform.localScale = Vector3.one * Mathf.Max(0.01f, s.coinScale);
        var inst = Instantiate(prefab, holder.transform);
        inst.transform.localPosition = Vector3.zero;
        Bounds b;
        if (BeltItem.LocalBounds(holder.transform, inst.transform, out b))
        {
            Vector3 sz = b.size;
            Quaternion q = Quaternion.identity;
            if (sz.y < sz.x && sz.y <= sz.z) q = Quaternion.Euler(90f, 0f, 0f);
            else if (sz.x < sz.y && sz.x <= sz.z) q = Quaternion.Euler(0f, 90f, 0f);
            inst.transform.localRotation = q * inst.transform.localRotation;
            if (BeltItem.LocalBounds(holder.transform, inst.transform, out b)) inst.transform.localPosition = -b.center;
        }
        return holder;
    }

    // Conferência visual (só no Editor): limites da esteira em planta, 3 pistas jogáveis (verde) e 2 de cenário (cinza),
    // o caminho dos itens (amarelo, do ponto onde nascem, em S = 0, até a Lu) e a caixa de cada asset.
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || s == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        float w = s.laneWidth;
        float nearZ = -(0.5f * L + fadeLen);
        float len = flatEnd - nearZ;
        float zc = (flatEnd + nearZ) * 0.5f;
        Gizmos.color = new Color(0.3f, 1f, 0.4f, 1f);
        Gizmos.DrawWireCube(new Vector3(0f, 0f, zc), new Vector3(5f * w, 0.01f, len));

        float R = curveR;
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 1f);
        Vector3 last = PathPoint(0f, R);
        Gizmos.DrawWireSphere(last, 0.08f * w);
        float total = arc + flatEnd - nearZ;
        const int steps = 40;
        for (int i = 1; i <= steps; i++)
        {
            Vector3 p = PathPoint(total * i / steps, R);
            Gizmos.DrawLine(last, p);
            last = p;
        }
    }

    // Posição local no caminho dos itens (eixo central, no piso) para a distância S percorrida: curva distante e depois reta.
    Vector3 PathPoint(float sPos, float R)
    {
        if (sPos < arc)
        {
            float a = Mathf.PI * 0.5f - sPos / R;
            return new Vector3(0f, R * Mathf.Cos(a) - R, flatEnd + R * Mathf.Sin(a));
        }
        return new Vector3(0f, 0f, flatEnd - (sPos - arc));
    }

    // ---------- módulos ----------

    void ResetLaneSince()
    {
        for (int i = 0; i < 5; i++) laneSince[i] = 1000;
    }

    // Escolhe a primeira pista de um obstáculo de largura w. Uma pista só recebe outro obstáculo depois que
    // "Obstacle Lane Gap Objects" obstáculos foram criados em outras pistas. Se nenhuma posição cumpre isso
    // (por exemplo, depois de objetos largos), usa a posição cujas pistas ficaram sem obstáculo há mais tempo.
    int PickObstacleStart(int w)
    {
        int need = Mathf.Max(0, s.obstacleLaneGapObjects);
        var best = new List<int>();
        int bestScore = -1;
        for (int c0 = 0; c0 + w - 1 <= 4; c0++)
        {
            int score = int.MaxValue;
            for (int c = c0; c < c0 + w; c++) score = Mathf.Min(score, laneSince[c]);
            if (score >= need) score = 100000 + 0;
            if (score > bestScore)
            {
                bestScore = score;
                best.Clear();
            }
            if (score == bestScore) best.Add(c0);
        }
        return best[Random.Range(0, best.Count)];
    }

    void MarkLanes(int c0, int c1)
    {
        for (int c = 0; c < 5; c++) laneSince[c] = (c >= c0 && c <= c1) ? 0 : Mathf.Min(laneSince[c] + 1, 1000);
    }

    // Posição de um módulo ao longo do percurso. Cada obstáculo e cada moeda nasce numa posição sorteada
    // dentro do módulo (e atrás dele, se o objeto for longo); itens com o mesmo valor ficariam alinhados.
    float ModuleKey(int m)
    {
        return (m + 1) * L;
    }

    // Moeda: verdadeiro se a posição fica a pelo menos "Coin Min Gap Boxes" caixas livres de qualquer outra moeda.
    bool CoinKeyOk(float key)
    {
        float need = (1f + Mathf.Max(0, s.coinMinGapBoxes)) * L - 0.0001f;
        foreach (var c in coinRecs)
        {
            if (Mathf.Abs(key - c.key) < need) return false;
        }
        return true;
    }

    // Moeda: entre a moeda e qualquer obstáculo da mesma pista sobram pelo menos "Coin Obstacle Gap Boxes" caixas livres, antes e depois.
    bool CoinLaneClear(float key, int col)
    {
        float gap = Mathf.Max(0f, s.coinObstacleGapBoxes) * L;
        foreach (var o in obstacleRecs)
        {
            if (col < o.c0 || col > o.c1) continue;
            if (Mathf.Abs(key - o.key) < (o.along + 1) * 0.5f * L + gap) return false;
        }
        return true;
    }

    // Obstáculo: nenhum outro obstáculo na mesma linha transversal nem mais perto que "Obstacle Min Gap Boxes" caixas livres
    // (em qualquer pista), e nenhuma moeda a menos de "Coin Obstacle Gap Boxes" caixas livres nas pistas que ele ocupa.
    bool ObstacleClear(float key, int c0, int c1, int along)
    {
        float gap = Mathf.Max(0f, s.obstacleMinGapBoxes) * L;
        float coinGap = Mathf.Max(0f, s.coinObstacleGapBoxes) * L;
        foreach (var o in obstacleRecs)
        {
            if (Mathf.Abs(key - o.key) < (along + o.along) * 0.5f * L + gap) return false;
        }
        foreach (var c in coinRecs)
        {
            if (c.col < c0 || c.col > c1) continue;
            if (Mathf.Abs(key - c.key) < (along + 1) * 0.5f * L + coinGap) return false;
        }
        return true;
    }

    void SpawnModule(float elapsed)
    {
        int m = moduleIndex++;
        float s0 = travel;
        SpawnScenery(m, s0);
        SpawnObstacles(m, s0);
        SpawnCoins(m, s0, elapsed);
    }

    AssetHeight ResolveHeight(LevelConfig.AssetEntry e)
    {
        if (!s.allowSuspendedAssets) return AssetHeight.Floor;
        bool f = e.heightFloor;
        bool j = e.heightJump;
        bool d = e.heightDoubleJump && s.doubleJumpEnabled;
        int n = (f ? 1 : 0) + (j ? 1 : 0) + (d ? 1 : 0);
        if (n == 0) return AssetHeight.Floor;
        int r = Random.Range(0, n);
        if (f)
        {
            if (r == 0) return AssetHeight.Floor;
            r--;
        }
        if (j)
        {
            if (r == 0) return AssetHeight.Jump;
            r--;
        }
        return AssetHeight.DoubleJump;
    }

    float HeightY(AssetHeight h)
    {
        if (h == AssetHeight.Jump) return s.suspendedHeightJump;
        if (h == AssetHeight.DoubleJump) return s.suspendedHeightDoubleJump;
        return 0f;
    }

    // Asset no ar: o topo nunca passa da altura limite (o espaço em altura é o limite menos a base). Se, para caber,
    // ele precisasse ficar menor que minAirScale do tamanho que teria no piso, ele não sai do chão.
    AssetHeight FitAirHeight(LevelConfig.AssetEntry e, AssetHeight h)
    {
        if (h == AssetHeight.Floor) return h;
        float avail = UsableHeight - HeightY(h);
        if (avail < 0.05f) return AssetHeight.Floor;
        Vector3 size;
        if (!MeasureModel(e, out size)) return h;
        float fw;
        float airWidth = Mathf.Min(Mathf.Clamp(e.cellsAcross, 1, 3), maxAirAcross) * s.laneWidth;
        if (e.autoCells)
            fw = Mathf.Min(airWidth / size.x, Mathf.Clamp(e.cellsAlong, 1, 3) * L / size.z);
        else
            fw = Mathf.Min(1f, airWidth / size.x);
        float fFloor = Mathf.Min(fw, UsableHeight / size.y);
        float fAir = Mathf.Min(fw, avail / size.y);
        return fAir < minAirScale * fFloor ? AssetHeight.Floor : h;
    }

    void SpawnScenery(int m, float s0)
    {
        if (edgeEntries.Count == 0) return;
        for (int side = 0; side < 2; side++)
        {
            int col = side == 0 ? 0 : 4;
            if (blockedUntil[col] >= m) continue;
            if (Random.value > s.sceneryChance) continue;
            hasPlan = false;
            planBaseY = 0f;
            var e = edgeEntries[Random.Range(0, edgeEntries.Count)];
            AssetHeight sh = FitAirHeight(e, ResolveHeight(e));
            planBaseY = HeightY(sh);
            int along = Mathf.Max(1, Along(e));
            float sCenter = s0 - (along - 1) * 0.5f * L;
            SpawnItem(e.prefab, BeltKind.Scenery, (col - 2) * s.laneWidth, HeightY(sh),
                sCenter, along * L * 0.5f, false, e);
            planBaseY = 0f;
            blockedUntil[col] = m + along - 1;
        }
    }

    // Cada obstáculo (ou objeto largo de 2 ou 3 pistas) nasce numa pista e numa posição sorteadas dentro do módulo.
    // Nunca fica alinhado com outro obstáculo: a distância mínima entre dois é "Obstacle Min Gap Boxes" (GameSettings).
    // Como há no máximo um obstáculo por linha transversal, sempre sobram pistas livres para a Lu.
    void SpawnObstacles(int m, float s0)
    {
        if (obstacleEntries.Count == 0) return;
        if (m < s.startEmptyModules) return;
        float density = Mathf.Max(0.1f, s.obstacleDensity);
        if (Random.value >= Mathf.Clamp01(s.obstacleChance * density)) return;

        int tries = Mathf.Clamp(Mathf.RoundToInt(4f * density), 1, 12);
        for (int attempt = 0; attempt < tries; attempt++)
        {
            hasPlan = false;
            planBaseY = 0f;
            var e = obstacleEntries[Random.Range(0, obstacleEntries.Count)];
            AssetHeight h = FitAirHeight(e, ResolveHeight(e));
            planBaseY = HeightY(h);
            int w = Mathf.Clamp(Across(e), 1, 3);
            int along = Mathf.Max(1, Along(e));
            bool ground = h == AssetHeight.Floor;

            float rel = (along - 1) * 0.5f * L + Random.value * L;
            float key = ModuleKey(m) + rel;
            int start = PickObstacleStart(w);
            int c1 = start + w - 1;
            if (!ObstacleClear(key, start, c1, along)) continue;

            float center = start + (w - 1) * 0.5f;
            var spawned = SpawnItem(e.prefab, BeltKind.Obstacle, (center - 2f) * s.laneWidth, HeightY(h), s0 - rel,
                along * L * 0.5f, false, e, Mathf.Clamp(Mathf.RoundToInt(center), 0, 4));
            // Moeda em cima do obstáculo só se o modelo (já reduzido) não passa do pico do pulo da Lu.
            bool lowEnough = false;
            Bounds hb;
            if (spawned != null && BeltItem.LocalBounds(spawned.transform, spawned.transform, out hb)) lowEnough = hb.max.y <= s.JumpPeak;
            planBaseY = 0f;
            if (spawned != null) spawned.canJump = lowEnough;
            obstacleRecs.Add(new ObstacleRec { key = key, c0 = start, c1 = c1, along = along });
            MarkLanes(start, c1);
            if (obstacleRecs.Count > 24) obstacleRecs.RemoveAt(0);
            lastObstacleEnd = m + along - 1;
            if (ground && lowEnough)
            {
                jumpCol0 = start;
                jumpWidth = w;
                jumpModule = m;
                jumpRel = rel;
            }
            return;
        }
    }

    int ChooseEscape(int m)
    {
        var near = new List<int>();
        var any = new List<int>();
        for (int c = 0; c <= 4; c++)
        {
            if (blockedUntil[c] >= m) continue;
            any.Add(c);
            if (lastEscape < 0 || Mathf.Abs(c - lastEscape) <= 1) near.Add(c);
        }
        var pool = near.Count > 0 ? near : any;
        if (pool.Count == 0) return -1;
        return pool[Random.Range(0, pool.Count)];
    }

    // Moedas: uma por vez, nos instantes sorteados em Init, em posição sorteada dentro do módulo. Cada moeda
    // pode aparecer no piso, na altura do pulo ou na do pulo duplo (proporção: Coin Ground/Jump/Double Jump Weight; a do pulo
    // pode ficar em cima de um obstáculo baixo). Nenhuma moeda fica alinhada com outra nem a menos de
    // "Coin Min Gap Boxes" caixas livres; se não houver lugar neste módulo, a moeda espera o seguinte.
    void SpawnCoins(int m, float s0, float elapsed)
    {
        if (level.coinPrefab == null || coinIdx >= coinTimes.Count || elapsed < coinTimes[coinIdx]) return;
        int col;
        float y;
        bool ground;
        bool onJump;
        float rel;
        if (!PickCoinSpot(m, jumpModule == m, out col, out y, out ground, out onJump, out rel)) return;
        SpawnItem(level.coinPrefab, BeltKind.Coin, (col - 2) * s.laneWidth, y, s0 - rel, L * 0.5f, false, null, col);
        coinRecs.Add(new CoinRec { key = ModuleKey(m) + rel, col = col });
        if (coinRecs.Count > 10) coinRecs.RemoveAt(0);
        coinIdx++;
        coinsSpawned++;
    }

    // A altura da moeda é sorteada primeiro, pelos pesos do GameSettings (chão, pulo, pulo duplo), e fica a mesma até a moeda
    // nascer: se não houver pista livre neste módulo, a moeda espera o seguinte, sem cair para o chão.
    bool PickCoinSpot(int mi, bool overJumpObstacle, out int col, out float y, out bool ground, out bool onJump, out float rel)
    {
        onJump = false;
        col = -1;
        y = s.coinHeight;
        ground = false;
        rel = Random.value * L;

        if (coinHeightPick < 0)
        {
            float wg = Mathf.Max(0f, s.coinGroundWeight);
            float wj = s.allowSuspendedAssets ? Mathf.Max(0f, s.coinJumpWeight) : 0f;
            float wd = s.allowSuspendedAssets && s.doubleJumpEnabled ? Mathf.Max(0f, s.coinDoubleJumpWeight) : 0f;
            if (wg + wj + wd <= 0f) wg = 1f;
            float r = Random.value * (wg + wj + wd);
            coinHeightPick = r < wg ? 0 : (r < wg + wj ? 1 : 2);
        }

        if (coinHeightPick == 1 && overJumpObstacle && CoinKeyOk(ModuleKey(mi) + jumpRel))
        {
            col = jumpCol0 + Random.Range(0, jumpWidth);
            y = s.CoinHeightJump;
            onJump = true;
            rel = jumpRel;
            coinHeightPick = -1;
            return true;
        }

        col = PickCoinLane(mi, rel);
        if (col < 0) return false;
        if (coinHeightPick == 1) y = s.CoinHeightJump;
        else if (coinHeightPick == 2) y = s.CoinHeightDoubleJump;
        else ground = true;
        coinHeightPick = -1;
        return true;
    }

    // Trilha livre (0 a 4) para uma moeda na posição sorteada do módulo mi. -1 = nenhuma.
    int PickCoinLane(int mi, float rel)
    {
        float key = ModuleKey(mi) + rel;
        if (!CoinKeyOk(key)) return -1;
        var free = new List<int>();
        for (int c = 0; c <= 4; c++)
        {
            if (!CoinLaneClear(key, c)) continue;
            free.Add(c);
        }
        return free.Count == 0 ? -1 : free[Random.Range(0, free.Count)];
    }

    // ---------- conferência visual no Game (chave do painel de teste) ----------

    public bool ShowBoxes
    {
        get { return showBoxes; }
        set
        {
            showBoxes = value;
            foreach (var it in items)
            {
                if (it != null) it.SetBoxVisible(value);
            }
            RefreshLimits();
        }
    }

    // Limites da esteira em planta (5 pistas, verde) e caminho dos itens (amarelo, do ponto onde nascem até a Lu).
    void RefreshLimits()
    {
        if (limitsObj != null) Kill(limitsObj);
        limitsObj = null;
        if (!showBoxes || s == null) return;
        limitsObj = new GameObject("LimitesTeste");
        limitsObj.transform.SetParent(transform, false);
        float w = s.laneWidth;
        float nearZ = -(0.5f * L + fadeLen);
        for (int i = 0; i <= 5; i++)
        {
            float x = (i - 2.5f) * w;
            MakeLine("Pista" + i, new Color(0.3f, 1f, 0.4f, 1f), new[] { new Vector3(x, 0.02f, nearZ), new Vector3(x, 0.02f, flatEnd + curveR) });
        }
        MakeLine("Fim", new Color(0.3f, 1f, 0.4f, 1f), new[] { new Vector3(-2.5f * w, 0.02f, flatEnd + curveR), new Vector3(2.5f * w, 0.02f, flatEnd + curveR) });
        float total = arc + flatEnd - nearZ;
        const int steps = 40;
        var path = new Vector3[steps + 1];
        for (int i = 0; i <= steps; i++) path[i] = PathPoint(total * i / steps, curveR);
        MakeLine("Caminho", new Color(1f, 0.9f, 0.2f, 1f), path);
    }

    void MakeLine(string name, Color c, Vector3[] pts)
    {
        var go = new GameObject(name);
        go.transform.SetParent(limitsObj.transform, false);
        var lr = go.AddComponent<LineOverlay>();
        lr.color = c;
        lr.points = pts;
    }

    public void SpawnFinish()
    {
        if (FinishSpawned) return;
        FinishSpawned = true;

        GameObject go;
        if (level.finishLinePrefab != null) go = Instantiate(level.finishLinePrefab, Holder);
        else
        {
            go = new GameObject("Chegada");
            go.transform.SetParent(Holder, false);
        }
        var it = go.AddComponent<BeltItem>();
        it.kind = BeltKind.Finish;
        it.x = 0f;
        it.baseY = 0f;
        it.s = travel;
        it.halfDepth = 0.5f * L;
        it.Setup();
        it.DisableColliders();
        items.Add(it);
        UpdateItem(it);
    }

    // ---------- ações do jogo ----------

    public void Remove(BeltItem it)
    {
        if (it == null) return;
        items.Remove(it);
        it.gameObject.SetActive(false);
        Destroy(it.gameObject);
    }

    // Depois de esbarrar, some com os obstáculos mais próximos para Lu recomeçar sem bater de novo.
    public void ClearObstaclesAhead(float distance)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var it = items[i];
            if (it == null || it.kind != BeltKind.Obstacle) continue;
            if (it.transform.localPosition.z < distance)
            {
                Destroy(it.gameObject);
                items.RemoveAt(i);
            }
        }
    }

    // Tira o item da lista sem destruí-lo (o efeito de pegar moeda cuida dele).
    public void Detach(BeltItem it)
    {
        if (it != null) items.Remove(it);
    }

    // Faíscas: um estouro curto de peças pequenas, na posição local dada.
    public void PlayBurst(Vector3 localOrigin, Color[] colors, int count, float size, float seconds)
    {
        var go = new GameObject("Faiscas");
        go.transform.SetParent(transform, false);
        var burst = go.AddComponent<ConfettiBurst>();
        burst.Play(localOrigin, colors, count, size, seconds, 0f);
        Destroy(go, seconds + 1f);
    }

    public void PlayConfetti(Color[] colors)
    {
        var go = new GameObject("Confetes");
        go.transform.SetParent(transform, false);
        var burst = go.AddComponent<ConfettiBurst>();
        Vector3 origin = (lu != null ? lu.transform.localPosition : Vector3.zero) + new Vector3(0f, 2.6f, 0f);
        burst.Play(origin, colors, s.confettiCount, s.confettiSize, s.confettiSeconds, 0.6f, true, s.confettiFloorHeight);
    }
}
