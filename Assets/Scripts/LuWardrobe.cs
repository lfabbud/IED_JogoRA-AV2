using UnityEngine;

// Fica no LuModelo. Pinta a meia, o short e o boné da Lu com as cores escolhidas no vestiário.
// Cada peça aponta para os Renderers (e, se quiser, um material específico) que ela pinta.
public class LuWardrobe : MonoBehaviour
{
    [System.Serializable]
    public class Part
    {
        public string label = "Meia";
        public Renderer[] renderers;
        [Tooltip("Índice do material dentro do Renderer. -1 pinta todos os materiais do Renderer.")]
        public int materialIndex = -1;
    }

    public Part[] parts;

    MaterialPropertyBlock block;
    Color[] applied;
    bool[] hasApplied;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public int PartCount
    {
        get { return parts == null ? 0 : parts.Length; }
    }

    public void Apply(int partIndex, Color color)
    {
        if (parts == null || partIndex < 0 || partIndex >= parts.Length) return;
        var part = parts[partIndex];
        if (part == null || part.renderers == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        if (applied == null || applied.Length != parts.Length)
        {
            applied = new Color[parts.Length];
            hasApplied = new bool[parts.Length];
        }
        applied[partIndex] = color;
        hasApplied[partIndex] = true;

        foreach (var r in part.renderers)
        {
            if (r == null) continue;
            if (part.materialIndex < 0)
            {
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                r.SetPropertyBlock(block);
            }
            else
            {
                r.GetPropertyBlock(block, part.materialIndex);
                block.SetColor(BaseColorId, color);
                r.SetPropertyBlock(block, part.materialIndex);
            }
        }
    }

    // Cor escolhida para um material de um Renderer (usado pelo LuFade para manter a cor ao mudar a opacidade).
    public bool TryGetColor(Renderer renderer, int materialIndex, out Color color)
    {
        color = Color.white;
        if (parts == null || applied == null) return false;
        for (int p = 0; p < parts.Length; p++)
        {
            if (parts[p] == null || parts[p].renderers == null || !hasApplied[p]) continue;
            if (parts[p].materialIndex >= 0 && parts[p].materialIndex != materialIndex) continue;
            foreach (var r in parts[p].renderers)
            {
                if (r == renderer)
                {
                    color = applied[p];
                    return true;
                }
            }
        }
        return false;
    }

    // Aplica as cores salvas no aparelho (peças sem escolha mantêm a cor original do modelo).
    public void ApplySaved(GameSettings settings)
    {
        if (settings == null || settings.wardrobeColors == null) return;
        for (int i = 0; i < PartCount; i++)
        {
            int index = SaveSystem.GetWardrobe(i);
            if (index >= 0 && index < settings.wardrobeColors.Length) Apply(i, settings.wardrobeColors[index]);
        }
    }
}
