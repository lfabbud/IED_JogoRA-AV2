using System.Collections.Generic;
using UnityEngine;

// Confetes da comemoração. Criado por script (TrackRoot.PlayConfetti): peças pequenas que sobem, giram e caem.
// Usa as cores da paleta da fase; sem paleta, usa cores vivas padrão.
public class ConfettiBurst : MonoBehaviour
{
    class Piece
    {
        public Transform t;
        public Vector3 velocity;
        public Vector3 spin;
        public float delay;
        public float life;
    }

    readonly List<Piece> pieces = new List<Piece>();
    float gravity = 7f;
    float duration;
    float time;

    static readonly Color[] Fallback =
    {
        new Color(1f, 0.35f, 0.4f), new Color(1f, 0.8f, 0.2f), new Color(0.3f, 0.8f, 0.5f),
        new Color(0.3f, 0.6f, 1f), new Color(0.8f, 0.5f, 1f)
    };

    public void Play(Vector3 localOrigin, Color[] colors, int count, float size, float seconds)
    {
        if (colors == null || colors.Length == 0) colors = Fallback;
        duration = seconds;
        var block = new MaterialPropertyBlock();
        var colorId = Shader.PropertyToID("_BaseColor");

        for (int i = 0; i < count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.name = "Confete";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localOrigin;
            go.transform.localScale = new Vector3(size, size * 0.12f, size * 1.6f);
            go.transform.localRotation = Random.rotation;

            var r = go.GetComponent<Renderer>();
            r.GetPropertyBlock(block);
            block.SetColor(colorId, colors[Random.Range(0, colors.Length)]);
            r.SetPropertyBlock(block);

            Vector3 dir = Random.insideUnitSphere;
            dir.y = Mathf.Abs(dir.y) + 0.6f;
            pieces.Add(new Piece
            {
                t = go.transform,
                velocity = dir.normalized * Random.Range(3f, 7f),
                spin = Random.insideUnitSphere * 540f,
                delay = Random.Range(0f, 0.6f),
                life = 0f
            });
            go.SetActive(false);
        }
    }

    void Update()
    {
        time += Time.deltaTime;
        float dt = Time.deltaTime;
        bool anyAlive = false;
        foreach (var p in pieces)
        {
            if (p.t == null) continue;
            if (time < p.delay) { anyAlive = true; continue; }
            if (!p.t.gameObject.activeSelf) p.t.gameObject.SetActive(true);
            p.life += dt;
            if (p.life > duration)
            {
                Destroy(p.t.gameObject);
                continue;
            }
            anyAlive = true;
            p.velocity += Vector3.down * gravity * dt;
            p.velocity *= (1f - 0.6f * dt);
            p.t.localPosition += p.velocity * dt;
            p.t.Rotate(p.spin * dt, Space.Self);
        }
        if (!anyAlive) Destroy(gameObject);
    }
}
