using System.Collections.Generic;
using UnityEngine;

// Trilha sonora com fade in e fade out. Fica viva entre as cenas: ao entrar numa cena, a trilha anterior some
// em "fadeOut" segundos e a nova entra em "fadeIn" segundos. Se a cena pedir a mesma trilha, ela continua sem cortar.
public class MusicPlayer : MonoBehaviour
{
    class Voice
    {
        public AudioSource src;
        public float target;
        public float rate;
    }

    static MusicPlayer inst;
    readonly List<Voice> voices = new List<Voice>();
    Voice current;

    public static void Play(AudioClip clip, float volume, float fadeIn, float fadeOut)
    {
        if (inst == null)
        {
            var go = new GameObject("MusicPlayer");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<MusicPlayer>();
        }
        inst.Switch(clip, volume, fadeIn, fadeOut);
    }

    void Switch(AudioClip clip, float volume, float fadeIn, float fadeOut)
    {
        if (current != null && clip != null && current.src.clip == clip)
        {
            SetTarget(current, volume, fadeIn);
            return;
        }
        if (current != null)
        {
            SetTarget(current, 0f, fadeOut);
            current = null;
        }
        if (clip == null) return;

        var v = new Voice();
        v.src = gameObject.AddComponent<AudioSource>();
        v.src.clip = clip;
        v.src.loop = true;
        v.src.spatialBlend = 0f;
        v.src.volume = 0f;
        v.src.Play();
        SetTarget(v, volume, fadeIn);
        voices.Add(v);
        current = v;
    }

    static void SetTarget(Voice v, float target, float seconds)
    {
        v.target = Mathf.Clamp01(target);
        v.rate = Mathf.Max(0.0001f, Mathf.Abs(v.target - v.src.volume)) / Mathf.Max(0.01f, seconds);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = voices.Count - 1; i >= 0; i--)
        {
            var v = voices[i];
            v.src.volume = Mathf.MoveTowards(v.src.volume, v.target, v.rate * dt);
            if (v.target <= 0f && v.src.volume <= 0f)
            {
                Destroy(v.src);
                voices.RemoveAt(i);
            }
        }
    }
}
