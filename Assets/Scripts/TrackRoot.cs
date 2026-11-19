using System.Collections.Generic;
using UnityEngine;

// Fica na raiz do prefab da esteira. Desliza a esteira sem parar, cria os módulos (fileiras transversais de 5 casas:
// 2 de cenário nas pontas e 3 de ação no meio), as moedas e a linha de chegada, e move todos os itens pelo percurso
// (curva distante > trecho reto > fade out depois da casa da Lu). Tudo em coordenadas locais.
// Eixos locais: +Z = lado de onde vêm os itens (horizonte); X = trilhas 0 a 4 (centro = 2); Lu fica em Z = 0.
public class TrackRoot : MonoBehaviour
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
    int jumpShiftCol = 2;

    float L;
    float flatEnd;
    float arc;
    float fadeLen;
    float travel;
    float beltOffset;
    int moduleIndex;
    int lastObstacleEnd;
    int lastEscape = -1;
    int jumpCol0, jumpWidth, jumpModule;

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

        float nearZ = -(0.5f * L + fadeLen);
        float length = flatEnd - nearZ;
        beltTile = Mathf.Max(0.01f, s.beltTextureTileLength);
        if (beltModel != null) Destroy(beltModel);
        beltModel = null;
        beltMat = null;
        if (level.beltPrefab != null) BuildBeltModel();
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
        for (int i = 0; i < 5; i++) laneShift[i] = Random.value * Mathf.Clamp01(s.lateralMisalignment) * L;
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
        beltRenderer.enabled = false;
        beltModel = Instantiate(level.beltPrefab, transform);
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
            beltMat = fr.material;
            beltMat.color = level.trackColor;
            beltTile = 1f;
            Bounds fb;
            if (BeltItem.LocalBounds(transform, fita, out fb))
            {
                // Cinta em volta dos dois rolos (pista de corrida): o perímetro é 2 trechos retos + 2 meias-voltas.
                // Fita só plana (altura quase zero): vale o comprimento.
                float h = fb.size.y;
                float perim = h < 0.05f ? fb.size.z : 2f * Mathf.Max(0f, fb.size.z - h) + Mathf.PI * h;
                beltTile = Mathf.Max(0.01f, perim / Mathf.Max(0.01f, beltMat.mainTextureScale.y));
                Debug.Log("[Lu] Fita: perímetro " + perim.ToString("0.00") + ", repetições da textura na volta " + beltMat.mainTextureScale.y.ToString("0.##") + ", cada repetição mede " + beltTile.ToString("0.000") + ", textura: " + (beltMat.mainTexture != null ? beltMat.mainTexture.name : "nenhuma") + ".");
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
    float AssetWidth(LevelConfig.AssetEntry e)
    {
        int across = Mathf.Clamp(e.cellsAcross, 1, 3);
        return across * s.laneWidth;
    }

    // Escolhe a orientação do asset: respeita as travas e só aceita as que cabem no espaço reservado
    // (largura x altura x comprimento, em caixas). Se passar da largura das pistas, o asset é reduzido por igual
    // (escala uniforme, só quando passa): scale devolve o fator (1 = tamanho original). Se nada couber, só avisa.
    Quaternion PickRotation(LevelConfig.AssetEntry e, Vector3 size, out float scale)
    {
        var dom = new Vector3(
            AssetWidth(e),
            Mathf.Clamp(e.cellsHigh, 1, 3) * s.CellHeight,
            Mathf.Max(1, e.cellsAlong) * L);
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
            float capH = s.maxHeightFactor * Mathf.Max(dom.x, Mathf.Max(dom.y, dom.z));
            if (s.maxHeightFactor > 0f && sz.y * f > capH) f = capH / sz.y;
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
            ". A largura é reduzida para caber nas pistas e a altura para no máximo " + s.maxHeightFactor.ToString("0.#") + "x a maior medida da caixa; ajuste as caixas ou o modelo.");
        float fb = size.x > dom.x ? dom.x / size.x : 1f;
        float capB = s.maxHeightFactor * Mathf.Max(dom.x, Mathf.Max(dom.y, dom.z));
        if (s.maxHeightFactor > 0f && size.y * fb > capB) fb = capB / size.y;
        scale = haveLockOnly ? lockOnlyScale : fb;
        return haveLockOnly ? lockOnly : Quaternion.identity;
    }

    // Cria o asset já na orientação escolhida e, se passar da largura das pistas, reduzido por igual.
    // Recentraliza para o conjunto ficar no centro das casas e com a base no piso.
    GameObject BuildOriented(LevelConfig.AssetEntry e)
    {
        var root = new GameObject(e.prefab.name);
        root.transform.SetParent(transform, false);
        var inst = Instantiate(e.prefab, root.transform);
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        Bounds b;
        if (!BeltItem.LocalBounds(root.transform, inst.transform, out b)) return root;
        float f;
        Quaternion q = PickRotation(e, b.size, out f);
        if (f < 0.9999f) inst.transform.localScale = inst.transform.localScale * f;
        if (q != Quaternion.identity)
        {
            Vector3 c = q * b.center;
            Vector3 sz = RotatedSize(q, b.size);
            inst.transform.localRotation = q;
            inst.transform.localPosition = new Vector3(-c.x, sz.y * 0.5f - c.y, -c.z) * f;
        }
        return root;
    }

    // Apaga tudo e volta ao estado inicial (usado ao tentar de novo, sem mexer na posição da esteira).
    public void ClearAll()
    {
        foreach (var it in items)
        {
            if (it != null) Destroy(it.gameObject);
        }
        items.Clear();
        foreach (var c in GetComponentsInChildren<ConfettiBurst>(true)) Destroy(c.gameObject);

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
        else go = Instantiate(prefab, transform);
        var it = go.AddComponent<BeltItem>();
        it.kind = kind;
        it.x = x;
        it.baseY = y;
        it.s = sPos;
        it.halfDepth = halfDepth;
        it.canJump = canJump;
        if (entry != null)
            it.gizmoBox = new Vector3(AssetWidth(entry), Mathf.Clamp(entry.cellsHigh, 1, 3) * s.CellHeight, Mathf.Max(1, entry.cellsAlong) * L);
        it.Setup();
        if (showBoxes) it.SetBoxVisible(true);
        if (kind == BeltKind.Obstacle || kind == BeltKind.Scenery) it.Paint(level.PaletteFor(kind == BeltKind.Scenery));
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
        holder.transform.SetParent(transform, false);
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

    void SpawnScenery(int m, float s0)
    {
        if (edgeEntries.Count == 0) return;
        for (int side = 0; side < 2; side++)
        {
            int col = side == 0 ? 0 : 4;
            if (blockedUntil[col] >= m) continue;
            if (Random.value > s.sceneryChance) continue;
            var e = edgeEntries[Random.Range(0, edgeEntries.Count)];
            int along = Mathf.Max(1, e.cellsAlong);
            float sCenter = s0 - (along - 1) * 0.5f * L;
            SpawnItem(e.prefab, BeltKind.Scenery, (col - 2) * s.laneWidth, HeightY(ResolveHeight(e)),
                sCenter, along * L * 0.5f, false, e);
            blockedUntil[col] = m + along - 1;
        }
    }

    // Sempre sobra uma casa livre (de escape) em cada módulo com obstáculo, e os módulos seguintes ficam livres
    // (casas de aterrissagem). Obstáculos baixos que ocupam as 3 casas obrigam a pular, e então a aterrissagem
    // é garantida pelo intervalo mínimo entre obstáculos.
    void SpawnObstacles(int m, float s0)
    {
        if (obstacleEntries.Count == 0) return;
        if (m < s.startEmptyModules) return;
        if (m - lastObstacleEnd <= s.minModulesBetweenObstacles) return;
        if (Random.value >= s.obstacleChance) return;

        int escape = ChooseEscape(m);
        if (escape < 0) return;

        var taken = new bool[5];
        int wanted = Random.value < s.threeObstacleChance ? 3 : (Random.value < s.twoObstacleChance ? 2 : 1);
        int placed = 0;
        for (int attempt = 0; attempt < 15 && placed < wanted; attempt++)
        {
            var e = obstacleEntries[Random.Range(0, obstacleEntries.Count)];
            int w = Mathf.Clamp(e.cellsAcross, 1, 3);
            int along = Mathf.Max(1, e.cellsAlong);
            AssetHeight h = ResolveHeight(e);
            bool ground = h == AssetHeight.Floor;
            if (ground && w == 3 && !e.canBeJumped) continue;

            var starts = new List<int>();
            for (int c0 = 0; c0 + w - 1 <= 4; c0++)
            {
                bool ok = true;
                for (int c = c0; c < c0 + w; c++)
                {
                    // Nada de moeda no piso na caixa logo antes do obstáculo (coinBusyUntil >= m - 1).
                    if (taken[c] || blockedUntil[c] >= m || coinBusyUntil[c] >= m - 1) ok = false;
                    if (ground && w < 3 && c == escape) ok = false;
                }
                if (ok) starts.Add(c0);
            }
            if (starts.Count == 0) continue;

            int start = starts[Random.Range(0, starts.Count)];
            float center = start + (w - 1) * 0.5f;
            float sCenter = s0 - (along - 1) * 0.5f * L;
            SpawnItem(e.prefab, BeltKind.Obstacle, (center - 2f) * s.laneWidth, HeightY(h), sCenter,
                along * L * 0.5f, e.canBeJumped, e, Mathf.Clamp(Mathf.RoundToInt(center), 0, 4));

            for (int c = start; c < start + w; c++)
            {
                taken[c] = true;
                blockedUntil[c] = m + along - 1;
            }
            lastObstacleEnd = Mathf.Max(lastObstacleEnd, m + along - 1);
            if (ground && e.canBeJumped)
            {
                jumpCol0 = start;
                jumpWidth = w;
                jumpModule = m;
                jumpShiftCol = Mathf.Clamp(Mathf.RoundToInt(center), 0, 4);
            }
            placed++;
        }
        if (placed > 0) lastEscape = escape;
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

    // Moedas: uma por vez, nos instantes sorteados em Init (frequência e posição aleatórias, sem grupos). Cada moeda
    // pode aparecer no piso ou, com Coin Hard Spot Chance, em lugar de difícil acesso (em cima de um obstáculo baixo ou
    // suspensa na altura do pulo / do pulo duplo). Reservas por trilha evitam obstáculos em cima de moedas no piso.
    void SpawnCoins(int m, float s0, float elapsed)
    {
        if (level.coinPrefab == null || coinIdx >= coinTimes.Count || elapsed < coinTimes[coinIdx]) return;
        int col;
        float y;
        bool ground;
        bool onJump;
        if (!PickCoinSpot(m, jumpModule == m, out col, out y, out ground, out onJump)) return;
        SpawnItem(level.coinPrefab, BeltKind.Coin, (col - 2) * s.laneWidth, y, s0, L * 0.5f, false, null, onJump ? jumpShiftCol : -1);
        if (ground) coinBusyUntil[col] = Mathf.Max(coinBusyUntil[col], m);
        coinIdx++;
        coinsSpawned++;
    }

    bool PickCoinSpot(int mi, bool overJumpObstacle, out int col, out float y, out bool ground, out bool onJump)
    {
        onJump = false;
        col = -1;
        y = s.coinHeight;
        ground = false;
        if (Random.value < s.coinHardSpotChance && s.allowSuspendedAssets)
        {
            if (overJumpObstacle)
            {
                col = jumpCol0 + Random.Range(0, jumpWidth);
                y = s.CoinHeightJump;
                onJump = true;
                return true;
            }
            int c = PickCoinLane(mi);
            if (c >= 0)
            {
                col = c;
                y = (s.doubleJumpEnabled && Random.value < 0.5f) ? s.CoinHeightDoubleJump : s.CoinHeightJump;
                return true;
            }
        }
        col = PickCoinLane(mi);
        if (col < 0) return false;
        ground = true;
        return true;
    }

    // Trilha livre (0 a 4) no módulo mi, sorteada. -1 = nenhuma.
    int PickCoinLane(int mi)
    {
        var free = new List<int>();
        for (int c = 0; c <= 4; c++)
        {
            if (blockedUntil[c] >= mi || coinBusyUntil[c] >= mi) continue;
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
        if (limitsObj != null) Destroy(limitsObj);
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
        if (level.finishLinePrefab != null) go = Instantiate(level.finishLinePrefab, transform);
        else
        {
            go = new GameObject("Chegada");
            go.transform.SetParent(transform, false);
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
        burst.Play(origin, colors, s.confettiCount, s.confettiSize, s.confettiSeconds);
    }
}
