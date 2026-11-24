using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Toca efeitos sonoros. Um objeto "Audio" com este script em cada cena.
public class Sfx : MonoBehaviour
{
    static Sfx instance;
    // Master Geral (GameSettings.soundVolumes.masterGeral): multiplica todos os efeitos; cada cena o define ao começar.
    public static float Volume = 1f;
    AudioSource source;
    AudioSource loopSource;
    AudioSource stepsSource;
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
        // Passadas da Lu: tocador próprio em repetição, separado do som da tontura (loopSource).
        stepsSource = gameObject.AddComponent<AudioSource>();
        stepsSource.playOnAwake = false;
        stepsSource.spatialBlend = 0f;
        stepsSource.loop = true;
    }

    static AudioClip buttonClip;
    static float buttonVolume = 1f;

    // Liga o som de botão em todos os Button da cena (inclusive os desativados), menos os filhos de "exclude".
    public static void HookButtons(AudioClip clip, Transform exclude = null, float volume = 1f)
    {
        buttonClip = clip;
        buttonVolume = volume;
        if (clip == null) return;
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (exclude != null && b.transform.IsChildOf(exclude)) continue;
            b.onClick.AddListener(PlayButton);
        }
    }

    public static void PlayButton()
    {
        Play(buttonClip, buttonVolume);
    }

    public static void Play(AudioClip clip, float volume = 1f)
    {
        if (instance == null || instance.source == null || clip == null) return;
        instance.source.PlayOneShot(clip, volume * Volume);
    }

    // Toca o clipe em repetição por exatamente "seconds" segundos (usado no som da tontura).
    public static void PlayFor(AudioClip clip, float seconds, float volume = 1f)
    {
        if (instance == null || clip == null) return;
        instance.StopTimedInternal();
        instance.timed = instance.StartCoroutine(instance.TimedRoutine(clip, seconds, volume));
    }

    // Passadas: toca o clipe em repetição, desde o começo, até StopSteps.
    public static void StartSteps(AudioClip clip, float volume = 1f)
    {
        if (instance == null || instance.stepsSource == null || clip == null) return;
        var src = instance.stepsSource;
        src.Stop();
        src.clip = clip;
        src.volume = volume * Volume;
        src.Play();
    }

    public static void StopSteps()
    {
        if (instance != null && instance.stepsSource != null) instance.stepsSource.Stop();
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

    IEnumerator TimedRoutine(AudioClip clip, float seconds, float volume)
    {
        loopSource.clip = clip;
        loopSource.volume = volume * Volume;
        loopSource.Play();
        yield return new WaitForSeconds(seconds);
        loopSource.Stop();
        timed = null;
    }
}
