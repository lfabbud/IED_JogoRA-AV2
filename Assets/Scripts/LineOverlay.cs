using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Linhas finas de 2 pixels (como as arestas dos colliders no Scene): usadas nas caixas de teste e nas pistas.
// Desenha na câmera depois da cena; Incerto: o shader "Hidden/Internal-Colored" pode não entrar numa build.
public class LineOverlay : MonoBehaviour
{
    public Vector3[] points;
    public Color color = Color.white;

    static readonly List<LineOverlay> active = new List<LineOverlay>();
    static Material mat;
    static bool hooked;

    void OnEnable()
    {
        if (!active.Contains(this)) active.Add(this);
        if (!hooked)
        {
            hooked = true;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
            Camera.onPostRender += OnPostRenderCam;
        }
    }

    void OnDisable()
    {
        active.Remove(this);
    }

    static void OnEndCamera(ScriptableRenderContext ctx, Camera cam) { Draw(cam); }
    static void OnPostRenderCam(Camera cam) { Draw(cam); }

    static void Draw(Camera cam)
    {
        if (active.Count == 0 || cam == null) return;
        if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;
        if (mat == null)
        {
            var sh = Shader.Find("Hidden/Internal-Colored");
            if (sh == null) return;
            mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_Cull", (int)CullMode.Off);
            mat.SetInt("_ZWrite", 0);
        }
        mat.SetPass(0);
        float halfTan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        Vector3 camPos = cam.transform.position;
        Vector3 right = cam.transform.right, up = cam.transform.up;
        for (int i = 0; i < active.Count; i++)
        {
            var o = active[i];
            if (o == null || o.points == null || o.points.Length < 2) continue;
            var m = o.transform.localToWorldMatrix;
            GL.Begin(GL.LINES);
            GL.Color(o.color);
            // Espessura de 2 pixels: a linha é desenhada 4 vezes, deslocada 0 e 1 pixel na horizontal e na vertical.
            for (int ox = 0; ox < 2; ox++)
            {
                for (int oy = 0; oy < 2; oy++)
                {
                    for (int p = 0; p < o.points.Length - 1; p++)
                    {
                        GL.Vertex(Shift(m.MultiplyPoint3x4(o.points[p]), camPos, right, up, halfTan, cam.pixelHeight, ox, oy));
                        GL.Vertex(Shift(m.MultiplyPoint3x4(o.points[p + 1]), camPos, right, up, halfTan, cam.pixelHeight, ox, oy));
                    }
                }
            }
            GL.End();
        }
    }

    static Vector3 Shift(Vector3 w, Vector3 camPos, Vector3 right, Vector3 up, float halfTan, int pixelHeight, int ox, int oy)
    {
        float d = Vector3.Distance(w, camPos);
        float px = 2f * d * halfTan / Mathf.Max(1, pixelHeight);
        return w + right * (px * ox) + up * (px * oy);
    }
}
