using UnityEngine;

// Mostra o objeto 3D do desconto numa tela: uma câmera própria enxerga só a camada do produto
// e desenha numa Render Texture, exibida por uma Raw Image nos cards.
// O objeto não é redimensionado: só é centralizado, e a câmera se afasta o necessário.
public class ProductDisplay : MonoBehaviour
{
    public Camera productCamera;
    public Transform stage;
    public string layerName = "ProdutoUI";
    public float spinDegreesPerSecond = 40f;
    public float margin = 1.15f;

    GameObject current;

    void Start()
    {
        if (productCamera != null) productCamera.enabled = false;
    }

    public void Show(GameObject prefab)
    {
        Clear();
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
        current.transform.localRotation = Quaternion.identity;
        foreach (var t in current.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

        var renderers = current.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            current.transform.position -= b.center - stage.position;

            float radius = b.extents.magnitude;
            float halfFov = productCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float distance = radius / Mathf.Sin(halfFov) * margin;
            productCamera.transform.position = stage.position - Vector3.forward * distance;
            productCamera.transform.LookAt(stage.position);
        }
        productCamera.enabled = true;
    }

    public void Clear()
    {
        if (current != null) Destroy(current);
        current = null;
        if (productCamera != null) productCamera.enabled = false;
    }

    void Update()
    {
        if (current != null && stage != null)
            stage.Rotate(0f, spinDegreesPerSecond * Time.unscaledDeltaTime, 0f, Space.World);
    }
}
