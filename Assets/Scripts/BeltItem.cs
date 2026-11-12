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

    public void Setup()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        baseColors = new Color[renderers.Length];
        block = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i].sharedMaterial;
            baseColors[i] = (m != null && m.HasProperty(BaseColorId)) ? m.GetColor(BaseColorId) : Color.white;
        }
    }

    // Cada peça (renderer) do prefab recebe uma cor da paleta, sorteada. Nenhuma peça fica sem cor.
    // Com uma paleta menor que o número de peças, as cores se repetem depois de usar todas.
    public void Paint(Color[] palette)
    {
        if (palette == null || palette.Length == 0 || renderers == null) return;
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
        for (int i = 0; i < renderers.Length; i++)
        {
            var c = palette[order[i % order.Length]];
            baseColors[i] = new Color(c.r, c.g, c.b, 1f);
        }
        alpha = -1f;
    }

    public void SetAlpha(float a)
    {
        if (renderers == null) return;
        a = Mathf.Clamp01(a);
        if (Mathf.Abs(a - alpha) < 0.004f && !(a >= 0.999f && alpha < 0.999f)) return;
        alpha = a;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            if (!painted && a >= 0.999f)
            {
                r.SetPropertyBlock(null);
                continue;
            }
            r.GetPropertyBlock(block);
            var c = baseColors[i];
            block.SetColor(BaseColorId, new Color(c.r, c.g, c.b, c.a * a));
            r.SetPropertyBlock(block);
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
