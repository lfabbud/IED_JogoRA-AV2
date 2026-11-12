using System.Collections;
using UnityEngine;

// Toca efeitos sonoros. Um objeto "Audio" com este script em cada cena.
public class Sfx : MonoBehaviour
{
    static Sfx instance;
    AudioSource source;
    AudioSource loopSource;
    Coroutine timed;

    void Awake()
    {
        instance = this;
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        loopSource = gameObject.AddComponent<AudioSource>();
        loopSource.playOnAwake = false;
        loopSource.spatialBlend = 0f;
        loopSource.loop = true;
    }

    public static void Play(AudioClip clip, float volume = 1f)
    {
        if (instance == null || instance.source == null || clip == null) return;
        instance.source.PlayOneShot(clip, volume);
    }

    // Toca o clipe em repetição por exatamente "seconds" segundos (usado no som da tontura).
    public static void PlayFor(AudioClip clip, float seconds)
    {
        if (instance == null || clip == null) return;
        instance.StopTimedInternal();
        instance.timed = instance.StartCoroutine(instance.TimedRoutine(clip, seconds));
    }

    public static void StopTimed()
    {
        if (instance != null) instance.StopTimedInternal();
    }

    void StopTimedInternal()
    {
        if (timed != null) StopCoroutine(timed);
        timed = null;
        if (loopSource != null) loopSource.Stop();
    }

    IEnumerator TimedRoutine(AudioClip clip, float seconds)
    {
        loopSource.clip = clip;
        loopSource.Play();
        yield return new WaitForSeconds(seconds);
        loopSource.Stop();
        timed = null;
    }
}
