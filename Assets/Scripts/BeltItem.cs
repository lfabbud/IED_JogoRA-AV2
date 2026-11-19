using UnityEngine;

public enum BeltKind { Obstacle, Coin, Scenery, Finish }

// Adicionado por script a cada item criado na esteira (obstáculo, moeda, cenário, linha de chegada).
// Guarda o tipo, a posição ao longo do percurso, as cores (paleta) e a opacidade (fade), e cria o collider-gatilho.
// Para o fade aparecer, os materiais do asset precisam estar com Surface Type = Transparent.
public class BeltItem : MonoBehaviour
{
    public BeltKind kind;
    public bool canJump;
    public float x;
    public float baseY;
    public float s;
    public float halfDepth;
    public float spinSpeed;

    Renderer[] renderers;
    Color[] baseColors;
    MaterialPropertyBlock block;
    bool painted;
    float alpha = -1f;
    float spin;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    // Caixa que envolve as peças de "sub", medida no espaço local de "root".
    public static bool LocalBounds(Transform root, Transform sub, out Bounds result)
    {
        bool has = false;
        result = new Bounds();
        Matrix4x4 w2l = root.worldToLocalMatrix;
        foreach (var r in sub.GetComponentsInChildren<Renderer>(true))
        {
            Bounds lb;
            if (r is MeshRenderer)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                lb = mf.sharedMesh.bounds;
            }
            else if (r is SkinnedMeshRenderer) lb = ((SkinnedMeshRenderer)r).localBounds;
            else continue;
            Matrix4x4 m = w2l * r.transform.localToWorldMatrix;
            for (int k = 0; k < 8; k++)
            {
                var corner = new Vector3((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f);
                Vector3 p = m.MultiplyPoint3x4(lb.center + Vector3.Scale(lb.extents, corner));
                if (!has) { result = new Bounds(p, Vector3.zero); has = true; }
                else result.Encapsulate(p);
            }
        }
        return has;
    }

    // Cada "fatia" = um material de um renderer (uma malha com 3 materiais tem 3 fatias).
    int[] slotRenderer;
    int[] slotMaterial;

    // Tamanho do espaço reservado do asset (largura x altura x comprimento). Zero = sem caixa.
    // Só serve para conferir no Scene (e no Game, com o botão Gizmos ligado): a caixa acompanha o item.
    public Vector3 gizmoBox;

    void OnDrawGizmos()
    {
        if (gizmoBox == Vector3.zero) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = kind == BeltKind.Obstacle ? new Color(1f, 0.3f, 0.2f, 1f) : new Color(0.2f, 0.9f, 1f, 1f);
        Gizmos.DrawWireCube(new Vector3(0f, gizmoBox.y * 0.5f, 0f), gizmoBox);
    }

    LineOverlay boxLine;

    // Caixa de teste visível também no Game (sem depender do botão Gizmos): liga e desliga.
    public void SetBoxVisible(bool on)
    {
        if (gizmoBox == Vector3.zero) return;
        if (boxLine == null)
        {
            if (!on) return;
            var go = new GameObject("CaixaTeste");
            go.transform.SetParent(transform, false);
            boxLine = go.AddComponent<LineOverlay>();
            boxLine.color = kind == BeltKind.Obstacle ? new Color(1f, 0.3f, 0.2f, 1f) : new Color(0.2f, 0.9f, 1f, 1f);
            float x = gizmoBox.x * 0.5f, z = gizmoBox.z * 0.5f, h = gizmoBox.y;
            var p = new Vector3[]
            {
                new Vector3(-x, 0f, -z), new Vector3(x, 0f, -z), new Vector3(x, 0f, z), new Vector3(-x, 0f, z), new Vector3(-x, 0f, -z),
                new Vector3(-x, h, -z), new Vector3(x, h, -z), new Vector3(x, 0f, -z), new Vector3(x, h, -z), new Vector3(x, h, z),
                new Vector3(x, 0f, z), new Vector3(x, h, z), new Vector3(-x, h, z), new Vector3(-x, 0f, z), new Vector3(-x, h, z),
                new Vector3(-x, h, -z)
            };
            boxLine.points = p;
        }
        boxLine.gameObject.SetActive(on);
    }

    public void Setup()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        block = new MaterialPropertyBlock();
        var sr = new System.Collections.Generic.List<int>();
        var sm = new System.Collections.Generic.List<int>();
        var cols = new System.Collections.Generic.List<Color>();
        for (int i = 0; i < renderers.Length; i++)
        {
            var mats = renderers[i].sharedMaterials;
            int n = Mathf.Max(1, mats.Length);
            for (int k = 0; k < n; k++)
            {
                var m = k < mats.Length ? mats[k] : null;
                sr.Add(i);
                sm.Add(k);
                cols.Add((m != null && m.HasProperty(BaseColorId)) ? m.GetColor(BaseColorId) : Color.white);
            }
        }
        slotRenderer = sr.ToArray();
        slotMaterial = sm.ToArray();
        baseColors = cols.ToArray();
    }

    // Cada fatia (cada material de cada peça do prefab) recebe uma cor da paleta, sorteada.
    // Peças e materiais diferentes do mesmo asset recebem cores diferentes. Nenhuma fica sem cor.
    // Com uma paleta menor que o número de fatias, as cores se repetem depois de usar todas.
    public void Paint(Color[] palette)
    {
        if (palette == null || palette.Length == 0 || baseColors == null) return;
        painted = true;
        var order = new int[palette.Length];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = order[i];
            order[i] = order[j];
            order[j] = tmp;
        }
        for (int i = 0; i < baseColors.Length; i++)
        {
            var c = palette[order[i % order.Length]];
            baseColors[i] = new Color(c.r, c.g, c.b, 1f);
        }
        alpha = -1f;
    }

    // Cor principal do asset (primeira cor que não é branca); sem nenhuma, devolve a cor de reserva.
    public Color MainColor(Color fallback)
    {
        if (baseColors != null)
        {
            foreach (var c in baseColors)
            {
                if (c.r > 0.92f && c.g > 0.92f && c.b > 0.92f) continue;
                return new Color(c.r, c.g, c.b, 1f);
            }
        }
        return fallback;
    }

    bool transparentOn;
    bool instanced;

    // Liga o modo transparente dos materiais enquanto o item está com opacidade menor que 1 (entrada e saída) e volta ao
    // opaco depois. Assim o fade aparece mesmo com materiais Opaque. INCERTO: feito para os shaders do URP (Lit, Simple Lit,
    // Unlit); em um build, a variante transparente do shader pode não estar incluída.
    void SetSurface(bool transparent)
    {
        if (transparentOn == transparent || renderers == null) return;
        transparentOn = transparent;
        instanced = true;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            var ms = r.materials;
            foreach (var m in ms)
            {
                if (m == null || !m.HasProperty("_Surface")) continue;
                if (transparent)
                {
                    m.SetFloat("_Surface", 1f);
                    m.SetFloat("_Blend", 0f);
                    m.SetOverrideTag("RenderType", "Transparent");
                    m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_ZWrite", 0f);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
                else
                {
                    m.SetFloat("_Surface", 0f);
                    m.SetOverrideTag("RenderType", "Opaque");
                    m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                    m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                    m.SetFloat("_ZWrite", 1f);
                    m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.renderQueue = -1;
                }
            }
        }
    }

    void OnDestroy()
    {
        if (!instanced || renderers == null) return;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            foreach (var m in r.materials)
            {
                if (m != null) Destroy(m);
            }
        }
    }

    public void SetAlpha(float a)
    {
        if (baseColors == null) return;
        a = Mathf.Clamp01(a);
        if (Mathf.Abs(a - alpha) < 0.004f && !(a >= 0.999f && alpha < 0.999f)) return;
        alpha = a;
        SetSurface(a < 0.999f);
        for (int i = 0; i < baseColors.Length; i++)
        {
            var r = renderers[slotRenderer[i]];
            if (r == null) continue;
            if (!painted && a >= 0.999f)
            {
                r.SetPropertyBlock(null, slotMaterial[i]);
                continue;
            }
            r.GetPropertyBlock(block, slotMaterial[i]);
            var c = baseColors[i];
            block.SetColor(BaseColorId, new Color(c.r, c.g, c.b, c.a * a));
            r.SetPropertyBlock(block, slotMaterial[i]);
        }
    }

    public void Spin(float dt)
    {
        spin += spinSpeed * dt;
    }

    public Quaternion SpinRotation
    {
        get { return Quaternion.Euler(0f, spin, 0f); }
    }

    // Transforma os colliders do prefab em gatilhos; se não houver nenhum, cria uma caixa que envolve as peças.
    // factor < 1 diminui a caixa (obstáculo mais "perdoador"); factor > 1 aumenta (moeda mais fácil de pegar).
    // Só caixas (Box Collider) têm o tamanho ajustado pela tolerância.
    public void BuildTrigger(float factor)
    {
        var cols = GetComponentsInChildren<Collider>(true);
        if (cols.Length > 0)
        {
            foreach (var c in cols)
            {
                c.isTrigger = true;
                var b = c as BoxCollider;
                if (b != null) b.size = b.size * factor;
            }
            return;
        }

        bool has = false;
        var bounds = new Bounds();
        foreach (var r in renderers)
        {
            Bounds lb;
            if (r is MeshRenderer)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                lb = mf.sharedMesh.bounds;
            }
            else if (r is SkinnedMeshRenderer)
            {
                lb = ((SkinnedMeshRenderer)r).localBounds;
            }
            else continue;

            Matrix4x4 m = transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
            for (int k = 0; k < 8; k++)
            {
                var corner = new Vector3(
                    (k & 1) == 0 ? -1f : 1f,
                    (k & 2) == 0 ? -1f : 1f,
                    (k & 4) == 0 ? -1f : 1f);
                Vector3 p = m.MultiplyPoint3x4(lb.center + Vector3.Scale(lb.extents, corner));
                if (!has)
                {
                    bounds = new Bounds(p, Vector3.zero);
                    has = true;
                }
                else bounds.Encapsulate(p);
            }
        }
        if (!has) return;
        var box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = bounds.center;
        box.size = bounds.size * factor;
    }

    public void DisableColliders()
    {
        foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
    }
}
