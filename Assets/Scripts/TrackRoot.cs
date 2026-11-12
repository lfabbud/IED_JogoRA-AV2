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
    readonly List<BeltItem> items = new List<BeltItem>();
    readonly List<LevelConfig.AssetEntry> obstacleEntries = new List<LevelConfig.AssetEntry>();
    readonly List<LevelConfig.AssetEntry> edgeEntries = new List<LevelConfig.AssetEntry>();

    // Reservas por trilha (0 a 4): último módulo ocupado.
    readonly int[] blockedUntil = new int[5];
    readonly int[] coinBusyUntil = new int[5];

    float L;
    float flatEnd;
    float arc;
    float fadeLen;
    float travel;
    float beltOffset;
    int moduleIndex;
    int lastObstacleEnd;
    int lastEscape;
    int jumpCol0, jumpWidth, jumpModule;

    int coinGroupsSpawned;
    int coinGroupsTotal;
    float coinInterval;
    float nextCoinTime;

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
        arc = Mathf.PI * 0.5f * Mathf.Max(0.1f, s.curveRadius);
        fadeLen = Mathf.Max(0.01f, s.fadeCells * L);
        travel = L;

        float nearZ = -(0.5f * L + fadeLen);
        float length = flatEnd - nearZ;
        beltRenderer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        beltRenderer.transform.localScale = new Vector3(s.laneWidth * 5f, length, 1f);
        beltRenderer.transform.localPosition = new Vector3(0f, 0f, (flatEnd + nearZ) * 0.5f);
        if (beltMat == null) beltMat = beltRenderer.material;
        beltMat.color = level.trackColor;
        beltMat.mainTextureScale = new Vector2(1f, length / Mathf.Max(0.01f, s.beltTextureTileLength));

        obstacleEntries.Clear();
        edgeEntries.Clear();
        if (level.assets != null)
        {
            foreach (var e in level.assets)
            {
                if (e == null || e.prefab == null) continue;
                if (e.onlyEdgeLanes) edgeEntries.Add(e);
                else obstacleEntries.Add(e);
            }
        }

        coinGroupsTotal = Mathf.Max(0, s.maxCoins / Mathf.Max(1, s.coinsPerGroup));
        float span = Mathf.Max(1f, s.runDurationSeconds - s.coinTailSeconds - s.coinStartDelaySeconds);
        coinInterval = coinGroupsTotal > 0 ? span / coinGroupsTotal : span;
        nextCoinTime = s.coinStartDelaySeconds;
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
        lastEscape = 0;
        jumpModule = -1000;
        coinGroupsSpawned = 0;
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

        beltOffset += dz / Mathf.Max(0.01f, s.beltTextureTileLength) * s.beltScrollDirection;
        beltMat.mainTextureOffset = new Vector2(0f, Mathf.Repeat(beltOffset, 1f));

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
        float R = Mathf.Max(0.1f, s.curveRadius);
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

    BeltItem SpawnItem(GameObject prefab, BeltKind kind, float x, float y, float sPos, float halfDepth, bool canJump)
    {
        var go = Instantiate(prefab, transform);
        var it = go.AddComponent<BeltItem>();
        it.kind = kind;
        it.x = x;
        it.baseY = y;
        it.s = sPos;
        it.halfDepth = halfDepth;
        it.canJump = canJump;
        it.Setup();
        if (kind == BeltKind.Obstacle || kind == BeltKind.Scenery) it.Paint(level.palette);
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
        if (e.height != AssetHeight.Random) return e.height;
        int n = s.doubleJumpEnabled ? 3 : 2;
        int r = Random.Range(0, n);
        return r == 0 ? AssetHeight.Floor : (r == 1 ? AssetHeight.Jump : AssetHeight.DoubleJump);
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
                sCenter, along * L * 0.5f, false);
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
        if (escape == 0) return;

        var taken = new bool[5];
        int wanted = Random.value < s.twoObstacleChance ? 2 : 1;
        int placed = 0;
        for (int attempt = 0; attempt < 10 && placed < wanted; attempt++)
        {
            var e = obstacleEntries[Random.Range(0, obstacleEntries.Count)];
            int w = Mathf.Clamp(e.cellsAcross, 1, 3);
            int along = Mathf.Max(1, e.cellsAlong);
            AssetHeight h = ResolveHeight(e);
            bool ground = h == AssetHeight.Floor;
            if (ground && w == 3 && !e.canBeJumped) continue;

            var starts = new List<int>();
            for (int c0 = 1; c0 + w - 1 <= 3; c0++)
            {
                bool ok = true;
                for (int c = c0; c < c0 + w; c++)
                {
                    if (taken[c] || blockedUntil[c] >= m || coinBusyUntil[c] >= m) ok = false;
                    if (ground && w < 3 && c == escape) ok = false;
                }
                if (ok) starts.Add(c0);
            }
            if (starts.Count == 0) continue;

            int start = starts[Random.Range(0, starts.Count)];
            float center = start + (w - 1) * 0.5f;
            float sCenter = s0 - (along - 1) * 0.5f * L;
            SpawnItem(e.prefab, BeltKind.Obstacle, (center - 2f) * s.laneWidth, HeightY(h), sCenter,
                along * L * 0.5f, e.canBeJumped);

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
            }
            placed++;
        }
        if (placed > 0) lastEscape = escape;
    }

    int ChooseEscape(int m)
    {
        var near = new List<int>();
        var any = new List<int>();
        for (int c = 1; c <= 3; c++)
        {
            if (blockedUntil[c] >= m) continue;
            any.Add(c);
            if (lastEscape == 0 || Mathf.Abs(c - lastEscape) <= 1) near.Add(c);
        }
        var pool = near.Count > 0 ? near : any;
        if (pool.Count == 0) return 0;
        return pool[Random.Range(0, pool.Count)];
    }

    // Moedas preferencialmente em lugares de difícil acesso (coinHardSpotChance): em cima de um obstáculo baixo
    // ou suspensas na altura do pulo (e do pulo duplo, se ativo). Senão, no piso, numa trilha livre.
    void SpawnCoins(int m, float s0, float elapsed)
    {
        if (level.coinPrefab == null || coinGroupsSpawned >= coinGroupsTotal || elapsed < nextCoinTime) return;

        int n = Mathf.Max(1, s.coinsPerGroup);
        int step = Mathf.Max(1, s.coinSpacingModules);
        int span = (n - 1) * step;
        int col = 0;
        float y = s.coinHeight;
        bool ground = false;

        if (Random.value < s.coinHardSpotChance && s.allowSuspendedAssets)
        {
            if (jumpModule == m)
            {
                col = jumpCol0 + Random.Range(0, jumpWidth);
                y = s.suspendedHeightJump;
            }
            else
            {
                var free = new List<int>();
                for (int c = 1; c <= 3; c++)
                {
                    if (blockedUntil[c] < m && coinBusyUntil[c] < m) free.Add(c);
                }
                if (free.Count > 0)
                {
                    col = free[Random.Range(0, free.Count)];
                    y = (s.doubleJumpEnabled && Random.value < 0.5f) ? s.suspendedHeightDoubleJump : s.suspendedHeightJump;
                }
            }
        }

        if (col == 0)
        {
            var free = new List<int>();
            for (int c = 1; c <= 3; c++)
            {
                if (blockedUntil[c] < m && coinBusyUntil[c] < m) free.Add(c);
            }
            if (free.Count == 0) return;
            col = free[Random.Range(0, free.Count)];
            y = s.coinHeight;
            ground = true;
        }

        for (int i = 0; i < n; i++)
        {
            SpawnItem(level.coinPrefab, BeltKind.Coin, (col - 2) * s.laneWidth, y, s0 - i * step * L, L * 0.5f, false);
        }
        if (ground) coinBusyUntil[col] = m + span;
        coinGroupsSpawned++;
        nextCoinTime += coinInterval;
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

    public void PlayConfetti(Color[] colors)
    {
        var go = new GameObject("Confetes");
        go.transform.SetParent(transform, false);
        var burst = go.AddComponent<ConfettiBurst>();
        Vector3 origin = (lu != null ? lu.transform.localPosition : Vector3.zero) + new Vector3(0f, 2.6f, 0f);
        burst.Play(origin, colors, s.confettiCount, s.confettiSize, s.confettiSeconds);
    }
}
