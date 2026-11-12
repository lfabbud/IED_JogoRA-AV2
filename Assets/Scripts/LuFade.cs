using UnityEngine;

// Faz a Lu desvanecer (opacidade) durante a piscada gradual. É adicionado por script ao objeto Lu: não precisa montar nada.
// Só funciona se os materiais da Lu estiverem com Surface Type = Transparent; senão o jogo usa a piscada seca.
public class LuFade : MonoBehaviour
{
    struct Entry
    {
        public Renderer renderer;
        public int material;
        public Color color;
    }

    Entry[] entries;
    MaterialPropertyBlock block;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int SurfaceId = Shader.PropertyToID("_Surface");
    bool warned;

    // Lê os materiais da Lu. Devolve false se algum não puder desvanecer.
    public bool Prepare()
    {
        if (block == null) block = new MaterialPropertyBlock();
        var wardrobe = GetComponentInChildren<LuWardrobe>(true);
        var list = new System.Collections.Generic.List<Entry>();
        bool ok = true;
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                if (!m.HasProperty(BaseColorId) || (m.HasProperty(SurfaceId) && m.GetFloat(SurfaceId) < 0.5f))
                {
                    ok = false;
                    continue;
                }
                Color c;
                if (wardrobe == null || !wardrobe.TryGetColor(r, i, out c)) c = m.GetColor(BaseColorId);
                list.Add(new Entry { renderer = r, material = i, color = c });
            }
        }
        entries = list.ToArray();
        if (!ok && !warned)
        {
            warned = true;
            Debug.LogWarning("LuFade: algum material da Lu não está com Surface Type = Transparent (ou não tem _BaseColor). A piscada seca será usada.");
        }
        return ok && entries.Length > 0;
    }

    public void SetAlpha(float alpha)
    {
        if (entries == null) return;
        alpha = Mathf.Clamp01(alpha);
        foreach (var e in entries)
        {
            if (e.renderer == null) continue;
            e.renderer.GetPropertyBlock(block, e.material);
            block.SetColor(BaseColorId, new Color(e.color.r, e.color.g, e.color.b, e.color.a * alpha));
            e.renderer.SetPropertyBlock(block, e.material);
        }
    }
}
