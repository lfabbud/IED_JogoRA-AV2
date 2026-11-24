using UnityEngine;

// Mostra o objeto 3D do desconto numa tela: uma câmera própria enxerga só a camada do produto
// e desenha numa Render Texture, exibida por uma Raw Image nos cards.
// O objeto não é redimensionado: só é centralizado, e a câmera se afasta o necessário.
public class ProductDisplay : MonoBehaviour
{
    public Camera productCamera;
    public Transform stage;
    public string layerName = "ProdutoUI";
    [Tooltip("Segundos para o produto dar uma volta completa. 0 = não gira.")]
    [Min(0f)] public float secondsPerTurn = 9f;
    public float margin = 1.15f;

    GameObject current;

    void Start()
    {
        if (productCamera != null) productCamera.enabled = false;
    }

    public void Show(GameObject prefab, float viewScale = 1f)
    {
        Clear();
        Place(prefab, viewScale);
    }

    // Pré-visualização na Scene (sem Play): mostra o produto da fase na Render Texture. O objeto criado não é salvo na cena.
    public void ShowPreview(GameObject prefab, float viewScale = 1f)
    {
        if (Application.isPlaying) return;
        Clear();
        Place(prefab, viewScale);
        if (current != null)
        {
            PhasePreview.MarkTemporary(current);
            productCamera.Render();
        }
    }

    public void ClearPreview()
    {
        if (Application.isPlaying) return;
        Clear();
    }

    // viewScale: maior que 1 aproxima a câmera (produto maior no quadro); menor que 1 afasta. O prefab não é alterado.
    void Place(GameObject prefab, float viewScale)
    {
        if (prefab == null || stage == null || productCamera == null) return;

        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
        {
            Debug.LogError("A layer '" + layerName + "' não existe. Crie em Project Settings > Tags and Layers.");
            return;
        }

        stage.rotation = Quaternion.identity;
        current = Instantiate(prefab, stage);
        current.transform.localPosition = Vector3.zero;
        foreach (var t in current.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

        var renderers = current.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            current.transform.position -= b.center - stage.position;

            float radius = b.extents.magnitude;
            float halfFov = productCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float distance = radius / Mathf.Sin(halfFov) * margin / Mathf.Max(0.1f, viewScale);
            productCamera.transform.position = stage.position - Vector3.forward * distance;
            productCamera.transform.LookAt(stage.position);
        }
        productCamera.enabled = true;
    }

    public void Clear()
    {
        if (current != null)
        {
            if (Application.isPlaying) Destroy(current);
            else DestroyImmediate(current);
        }
        current = null;
        if (productCamera != null) productCamera.enabled = false;
    }

    void Update()
    {
        if (current != null && stage != null)
        {
            if (secondsPerTurn > 0f) stage.Rotate(0f, 360f / secondsPerTurn * Time.unscaledDeltaTime, 0f, Space.World);
        }
    }
}
