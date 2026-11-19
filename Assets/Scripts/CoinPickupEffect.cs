using UnityEngine;

// Efeito ao pegar uma moeda: a moeda gira rápido, cresce, sobe um pouco e some (opacidade), com um estouro curto de
// faíscas na cor da moeda. Tudo em GameSettings (seção "Efeito ao pegar moeda"). Criado por script no momento do contato.
public class CoinPickupEffect : MonoBehaviour
{
    BeltItem item;
    GameSettings s;
    float t;
    float baseScale;
    float startY;

    public static void Play(TrackRoot track, BeltItem item, GameSettings settings)
    {
        if (track == null || item == null) return;
        track.Detach(item);
        Color c = item.MainColor(settings.coinSparkFallbackColor);
        Vector3 origin = track.transform.InverseTransformPoint(item.transform.position);
        track.PlayBurst(origin, new[] { c }, Mathf.Max(0, settings.coinSparkCount), settings.coinSparkSize, 0.5f);
        foreach (var col in item.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        item.spinSpeed = 720f;
        var fx = item.gameObject.AddComponent<CoinPickupEffect>();
        fx.item = item;
        fx.s = settings;
        fx.baseScale = item.transform.localScale.x;
        fx.startY = item.transform.localPosition.y;
    }

    void Update()
    {
        t += Time.deltaTime;
        float dur = Mathf.Max(0.05f, s.coinPopSeconds);
        float k = Mathf.Clamp01(t / dur);
        item.Spin(Time.deltaTime);
        var p = item.transform.localPosition;
        p.y = startY + s.coinPopRise * k;
        item.transform.localPosition = p;
        item.transform.localRotation = item.SpinRotation;
        item.transform.localScale = Vector3.one * (baseScale * Mathf.Lerp(1f, s.coinPopScale, k));
        item.SetAlpha(1f - k);
        if (k >= 1f) Destroy(gameObject);
    }
}
