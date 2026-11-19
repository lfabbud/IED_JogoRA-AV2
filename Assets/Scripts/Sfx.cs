using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Toca efeitos sonoros. Um objeto "Audio" com este script em cada cena.
public class Sfx : MonoBehaviour
{
    static Sfx instance;
    // Volume geral dos efeitos (GameSettings.sfxVolume); cada cena o define ao começar.
    public static float Volume = 1f;
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

    static AudioClip buttonClip;

    // Liga o som de botão em todos os Button da cena (inclusive os desativados), menos os filhos de "exclude".
    public static void HookButtons(AudioClip clip, Transform exclude = null)
    {
        buttonClip = clip;
        if (clip == null) return;
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (exclude != null && b.transform.IsChildOf(exclude)) continue;
            b.onClick.AddListener(PlayButton);
        }
    }

    public static void PlayButton()
    {
        Play(buttonClip);
    }

    public static void Play(AudioClip clip, float volume = 1f)
    {
        if (instance == null || instance.source == null || clip == null) return;
        instance.source.PlayOneShot(clip, volume * Volume);
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
        loopSource.volume = Volume;
        loopSource.Play();
        yield return new WaitForSeconds(seconds);
        loopSource.Stop();
        timed = null;
    }
}
