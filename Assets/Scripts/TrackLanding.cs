using UnityEngine;

// Aterrissagem da esteira ao posicionar: ela cai na vertical sobre o marcador, toca um som de impacto e solta poeira pelos lados.
// Fica na raiz do prefab da esteira (junto do TrackRoot). Sem este componente no prefab, o RunnerGame o adiciona com os valores padrão abaixo.
// Medidas em metros do mundo (não dependem da escala da esteira). Tudo pode ser ajustado no Inspector;
// a poeira (Particle System), o material dela e o som padrão são criados por código.
public class TrackLanding : MonoBehaviour
{
    [Header("Aterrissagem")]
    [Tooltip("Desligado: a esteira aparece direto no lugar marcado, sem queda.")]
    public bool enableLanding = true;
    [Tooltip("Altura de onde a esteira começa a cair, em metros.")]
    [Min(0f)] public float startHeight = 0.5f;
    [Tooltip("Duração da queda, em segundos. Menor = queda mais rápida.")]
    [Min(0.05f)] public float fallSeconds = 0.7f;
    [Tooltip("Espera antes de a queda começar, em segundos.")]
    [Min(0f)] public float delaySeconds = 0.15f;
    [Tooltip("Eixo X: tempo da queda (0 a 1). Eixo Y: caminho percorrido (0 = ponto de partida, 1 = no chão).")]
    public AnimationCurve fallCurve = DefaultFallCurve();
    [Tooltip("Altura do quique depois do impacto, em metros. 0 = sem quique.")]
    [Min(0f)] public float bounceHeight = 0.02f;
    [Tooltip("Duração do quique, em segundos.")]
    [Min(0.05f)] public float bounceSeconds = 0.25f;

    [Header("Som de impacto")]
    [Tooltip("Vazio: usa um som gerado por código (senoides curtas, no estilo dos outros sons).")]
    public AudioClip impactSound;
    [Range(0f, 1f)] public float impactVolume = 1f;

    [Header("Poeira")]
    public bool dust = true;
    [Tooltip("Largura da área de onde a poeira sai (eixo X da esteira), em metros.")]
    [Min(0.01f)] public float dustAreaWidth = 0.50f;
    [Tooltip("Comprimento da área de onde a poeira sai (eixo Z da esteira), em metros.")]
    [Min(0.01f)] public float dustAreaLength = 0.54f;
    [Tooltip("Quantidade de partículas no impacto.")]
    [Min(0)] public int dustCount = 40;
    [Tooltip("Tamanho inicial das partículas, em metros: mínimo e máximo.")]
    [Min(0.001f)] public float dustSizeMin = 0.04f;
    [Min(0.001f)] public float dustSizeMax = 0.09f;
    [Tooltip("Quantas vezes a partícula cresce até sumir (1 = não cresce).")]
    [Min(0.1f)] public float dustGrowth = 2.2f;
    [Tooltip("Velocidade para fora, em metros por segundo: mínima e máxima.")]
    [Min(0f)] public float dustSpeedMin = 0.20f;
    [Min(0f)] public float dustSpeedMax = 0.50f;
    [Tooltip("Abertura do jato, em graus, para cada lado da direção que sai perpendicular à borda.")]
    [Range(0f, 90f)] public float dustSpreadDegrees = 25f;
    [Tooltip("Velocidade para cima, em metros por segundo.")]
    public float dustRise = 0.10f;
    [Tooltip("Tempo de vida das partículas, em segundos: mínimo e máximo.")]
    [Min(0.05f)] public float dustLifeMin = 0.5f;
    [Min(0.05f)] public float dustLifeMax = 0.9f;
    public Color dustColorStart = new Color(0.92f, 0.88f, 0.82f, 0.7f);
    [Tooltip("Cor ao fim da vida da partícula. Normalmente a mesma cor com Alpha 0.")]
    public Color dustColorEnd = new Color(0.92f, 0.88f, 0.82f, 0f);
    [Tooltip("Vazio: o material é criado por código (shader Sprites/Default). Para garantir o visual no build, crie um material com textura de bolinha suave e arraste aqui.")]
    public Material dustMaterial;

    Vector3 ground;
    bool active;
    bool impacted;
    ParticleSystem dustSystem;

    static Texture2D softTexture;
    static Material autoMaterial;
    static AudioClip defaultImpact;

    public bool IsActive { get { return active; } }

    // Duração total da queda: espera + queda + quique.
    public float TotalSeconds
    {
        get { return delaySeconds + Mathf.Max(0.05f, fallSeconds) + (bounceHeight > 0f ? Mathf.Max(0.05f, bounceSeconds) : 0f); }
    }

    // Tempo até a poeira sumir por completo (usado na pré-visualização do editor).
    public float TotalWithDustSeconds
    {
        get { return TotalSeconds + (dust ? Mathf.Max(dustLifeMin, dustLifeMax) * 1.1f : 0f); }
    }

    static AnimationCurve DefaultFallCurve()
    {
        // Queda acelerando, como na gravidade: começa devagar e chega rápido no chão.
        return new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 2f));
    }

    void OnValidate()
    {
        if (dustSizeMax < dustSizeMin) dustSizeMax = dustSizeMin;
        if (dustSpeedMax < dustSpeedMin) dustSpeedMax = dustSpeedMin;
        if (dustLifeMax < dustLifeMin) dustLifeMax = dustLifeMin;
    }

    void OnDestroy()
    {
        DestroyDust();
    }

    // ---------- animação (chamada pelo RunnerGame no Play e pelo botão de pré-visualização no editor) ----------

    // Começa a queda: "groundPosition" é o lugar final da esteira. A esteira vai para o ponto de partida, no alto.
    public void Begin(Vector3 groundPosition)
    {
        ground = groundPosition;
        impacted = false;
        active = true;
        DestroyDust();
        if (dust && dustCount > 0) dustSystem = BuildDust();
        ApplyHeight(startHeight);
    }

    // "t" = segundos desde o Begin. Pode ser chamado várias vezes por quadro sem problema.
    public void Step(float t)
    {
        if (!active) return;
        float fall = Mathf.Max(0.05f, fallSeconds);
        if (t < delaySeconds)
        {
            ApplyHeight(startHeight);
            return;
        }

        float u = (t - delaySeconds) / fall;
        if (u < 1f)
        {
            float f = (fallCurve != null && fallCurve.length > 0) ? fallCurve.Evaluate(u) : u * u;
            ApplyHeight(startHeight * (1f - f));
            return;
        }

        if (!impacted)
        {
            impacted = true;
            Impact();
        }

        float bounce = 0f;
        if (bounceHeight > 0f)
        {
            float v = (t - delaySeconds - fall) / Mathf.Max(0.05f, bounceSeconds);
            if (v < 1f) bounce = bounceHeight * 4f * v * (1f - v);
        }
        ApplyHeight(bounce);
    }

    public bool Finished(float t)
    {
        return t >= TotalSeconds;
    }

    // Termina: a esteira fica exatamente no lugar final. A poeira que ainda está no ar some sozinha.
    public void End()
    {
        if (!active) return;
        ApplyHeight(0f);
        active = false;
        if (Application.isPlaying && dustSystem != null) Destroy(dustSystem.gameObject, Mathf.Max(dustLifeMin, dustLifeMax) + 0.5f);
    }

    // Só para a pré-visualização no editor (fora do Play): avança a simulação da poeira.
    public void AdvanceDust(float dt)
    {
        if (dustSystem != null && !Application.isPlaying && dt > 0f) dustSystem.Simulate(dt, true, false);
    }

    public void DestroyDust()
    {
        if (dustSystem == null) return;
        if (Application.isPlaying) Destroy(dustSystem.gameObject);
        else DestroyImmediate(dustSystem.gameObject);
        dustSystem = null;
    }

    void ApplyHeight(float h)
    {
        transform.position = ground + Vector3.up * h;
    }

    void Impact()
    {
        if (Application.isPlaying) Sfx.Play(impactSound != null ? impactSound : DefaultImpact(), impactVolume);
        if (dustSystem != null) EmitDust();
    }

    // ---------- poeira ----------

    ParticleSystem BuildDust()
    {
        var go = new GameObject("Poeira_Aterrissagem");
        if (!Application.isPlaying) go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.position = ground;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(100, dustCount * 2);
        main.startLifetime = 1f;
        main.startSize = 0.05f;
        main.startSpeed = 0f;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 0f;

        var shape = ps.shape;
        shape.enabled = false;

        // Cor ao longo da vida: a cor inicial vai se aproximando da cor final.
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(
                    Mathf.Clamp01(dustColorEnd.r / Mathf.Max(0.001f, dustColorStart.r)),
                    Mathf.Clamp01(dustColorEnd.g / Mathf.Max(0.001f, dustColorStart.g)),
                    Mathf.Clamp01(dustColorEnd.b / Mathf.Max(0.001f, dustColorStart.b)), 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(Mathf.Clamp01(dustColorEnd.a / Mathf.Max(0.001f, dustColorStart.a)), 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // Tamanho ao longo da vida: cresce de 1x até dustGrowth x.
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        float g = Mathf.Max(0.1f, dustGrowth);
        sol.size = new ParticleSystem.MinMaxCurve(g, new AnimationCurve(new Keyframe(0f, 1f / g), new Keyframe(1f, 1f)));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sortMode = ParticleSystemSortMode.Distance;
        r.sharedMaterial = GetDustMaterial();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        ps.Play();
        return ps;
    }

    // Solta as partículas ao longo da borda da área, saindo para fora.
    void EmitDust()
    {
        Quaternion yaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        float w = Mathf.Max(0.01f, dustAreaWidth) * 0.5f;
        float l = Mathf.Max(0.01f, dustAreaLength) * 0.5f;
        float perimeter = 4f * (w + l);
        var ep = new ParticleSystem.EmitParams();

        for (int i = 0; i < dustCount; i++)
        {
            float p = Random.value * perimeter;
            Vector3 pos;
            Vector3 normal;
            if (p < 2f * w) { pos = new Vector3(-w + p, 0f, -l); normal = Vector3.back; }
            else if (p < 4f * w) { pos = new Vector3(-w + (p - 2f * w), 0f, l); normal = Vector3.forward; }
            else if (p < 4f * w + 2f * l) { pos = new Vector3(-w, 0f, -l + (p - 4f * w)); normal = Vector3.left; }
            else { pos = new Vector3(w, 0f, -l + (p - 4f * w - 2f * l)); normal = Vector3.right; }

            Vector3 dir = Quaternion.Euler(0f, Random.Range(-dustSpreadDegrees, dustSpreadDegrees), 0f) * normal;
            float speed = Random.Range(dustSpeedMin, dustSpeedMax);

            ep.position = ground + yaw * pos + Vector3.up * 0.01f;
            ep.velocity = yaw * (dir * speed) + Vector3.up * (dustRise * Random.Range(0.7f, 1.3f));
            ep.startLifetime = Random.Range(dustLifeMin, dustLifeMax);
            ep.startSize = Random.Range(dustSizeMin, dustSizeMax);
            ep.startColor = dustColorStart;
            dustSystem.Emit(ep, 1);
        }
    }

    Material GetDustMaterial()
    {
        if (dustMaterial != null) return dustMaterial;
        if (autoMaterial == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null)
            {
                Debug.LogWarning("TrackLanding: shader da poeira não encontrado. Crie um material e arraste no campo Dust Material.");
                return null;
            }
            autoMaterial = new Material(sh);
            autoMaterial.name = "MAT_PoeiraAuto";
            autoMaterial.hideFlags = HideFlags.HideAndDontSave;
            autoMaterial.mainTexture = SoftTexture();
        }
        return autoMaterial;
    }

    // Bolinha branca com borda suave, gerada por código.
    static Texture2D SoftTexture()
    {
        if (softTexture != null) return softTexture;
        const int size = 64;
        softTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        softTexture.name = "IMG_PoeiraAuto";
        softTexture.hideFlags = HideFlags.HideAndDontSave;
        softTexture.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        softTexture.SetPixels32(px);
        softTexture.Apply(false, true);
        return softTexture;
    }

    // ---------- som de impacto (gerado por código) ----------

    // Mesma linguagem dos outros sons: senoides puras, curtas, pico em 0,6. Um "tum" que cai de 320 para 90 Hz
    // e, logo depois, dois "blips" subindo (784 e 1047 Hz), de brinquedo.
    static AudioClip DefaultImpact()
    {
        if (defaultImpact != null) return defaultImpact;
        const int sr = 44100;
        const float dur = 0.34f;
        int n = Mathf.RoundToInt(sr * dur);
        var data = new float[n];

        double phase = 0.0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float v = 0f;

            // tum: varredura descendente com decaimento
            float f = 90f + 230f * Mathf.Exp(-t * 14f);
            phase += 2.0 * Mathf.PI * f / sr;
            float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 9f);
            v += 0.9f * Mathf.Sin((float)phase) * env;

            // blip 1 e 2
            v += Blip(t, 0.08f, 0.09f, 784f) * 0.35f;
            v += Blip(t, 0.15f, 0.12f, 1047f) * 0.35f;

            data[i] = v;
        }

        float peak = 0f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        float gain = peak > 0.0001f ? 0.6f / peak : 1f;
        for (int i = 0; i < n; i++)
        {
            float tail = Mathf.Clamp01((n - 1 - i) / (sr * 0.01f));
            data[i] *= gain * tail;
        }

        defaultImpact = AudioClip.Create("SFX_AterrissagemAuto", n, 1, sr, false);
        defaultImpact.SetData(data, 0);
        defaultImpact.hideFlags = HideFlags.HideAndDontSave;
        return defaultImpact;
    }

    static float Blip(float t, float start, float length, float freq)
    {
        float u = (t - start) / length;
        if (u < 0f || u > 1f) return 0f;
        return Mathf.Sin(2f * Mathf.PI * freq * (t - start)) * Mathf.Sin(Mathf.PI * u);
    }
}
