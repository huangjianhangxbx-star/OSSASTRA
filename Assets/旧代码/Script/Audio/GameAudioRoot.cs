using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameAudioRoot : MonoBehaviour, IGameSubsystem
{
    [SerializeField] private int initialPoolSize = 32;
    [SerializeField, Min(0f)] private float defaultMusicFadeSeconds = 0.45f;

    private readonly Queue<AudioSource> pool = new Queue<AudioSource>();
    private AudioSource musicSourceA;
    private AudioSource musicSourceB;
    private AudioSource activeMusicSource;
    private AudioSource voiceSource;
    private Coroutine musicFadeRoutine;
    private bool isInitialized;
    private bool isQuitting;

    public AudioClip CurrentMusicClip =>
        activeMusicSource != null ? activeMusicSource.clip : null;
    public AudioClip CurrentVoiceClip =>
        voiceSource != null ? voiceSource.clip : null;

    public void Initialize(GameFlowRoot root)
    {
        if (isInitialized)
            return;

        int poolSize = Mathf.Max(0, initialPoolSize);
        RecoverOwnedSources(poolSize);
        while (pool.Count < poolSize)
            pool.Enqueue(CreateSource("PooledAudioSource"));

        if (musicSourceA == null)
            musicSourceA = CreateSource("MusicSourceA");
        if (musicSourceB == null)
            musicSourceB = CreateSource("MusicSourceB");
        ConfigureDedicatedSource(musicSourceA, true);
        ConfigureDedicatedSource(musicSourceB, true);
        activeMusicSource = musicSourceA;

        if (voiceSource == null)
            voiceSource = CreateSource("VoiceSource");
        ConfigureDedicatedSource(voiceSource, false);

        isInitialized = true;
        Debug.Log("[GameAudioRoot] 初始化完成", this);
    }

    public void PlaySfx(AudioEventSO audioEvent, Vector3 position)
    {
        PlayPooled(audioEvent, position, false);
    }

    public bool PlayUi(AudioEventSO audioEvent)
    {
        return PlayPooled(audioEvent, transform.position, true);
    }

    public bool PlayVoice(AudioEventSO audioEvent)
    {
        EnsureInitialized();
        if (!TryConfigureSource(voiceSource, audioEvent, true, out _))
            return false;

        voiceSource.loop = false;
        voiceSource.Play();
        return true;
    }

    public bool PlayMusic(AudioEventSO audioEvent, float fadeSeconds = -1f)
    {
        EnsureInitialized();
        if (audioEvent == null || !audioEvent.TryGetClip(out AudioClip clip))
            return false;

        AudioSource previous = activeMusicSource;
        AudioSource next = previous == musicSourceA ? musicSourceB : musicSourceA;
        ConfigureSource(next, audioEvent, clip, true);
        next.loop = true;
        float targetVolume = next.volume;
        float duration = fadeSeconds >= 0f
            ? fadeSeconds
            : defaultMusicFadeSeconds;

        StopMusicFade();
        activeMusicSource = next;
        if (duration <= 0f || previous == null || previous.clip == null)
        {
            StopAndClear(previous);
            next.volume = targetVolume;
            next.Play();
            return true;
        }

        next.volume = 0f;
        next.Play();
        musicFadeRoutine = StartCoroutine(
            CrossfadeMusic(previous, next, targetVolume, duration));
        return true;
    }

    public void StopMusic(float fadeSeconds = -1f)
    {
        EnsureInitialized();
        float duration = fadeSeconds >= 0f
            ? fadeSeconds
            : defaultMusicFadeSeconds;
        StopMusicFade();

        // Battle/scene teardown may disable this host before the presentation
        // requests its final stop. An inactive host cannot run the fade.
        if (duration <= 0f || isQuitting || !Application.isPlaying || !isActiveAndEnabled)
        {
            StopAndClear(musicSourceA);
            StopAndClear(musicSourceB);
            return;
        }

        musicFadeRoutine = StartCoroutine(FadeOutMusic(duration));
    }

    private void OnApplicationQuit()
    {
        // Unity can still report an active component during quit teardown.
        // Later presentation OnDisable calls must not restart a fade.
        isQuitting = true;
        if (isInitialized) StopMusic(0f);
    }

    private bool PlayPooled(
        AudioEventSO audioEvent,
        Vector3 position,
        bool forceTwoDimensional)
    {
        EnsureInitialized();
        AudioSource source = GetSource();
        if (!TryConfigureSource(
                source,
                audioEvent,
                forceTwoDimensional,
                out AudioClip clip))
        {
            ReturnSource(source);
            return false;
        }

        source.transform.position = position;
        source.loop = false;
        source.Play();

        float duration = clip.length / Mathf.Max(0.01f, Mathf.Abs(source.pitch));
        StartCoroutine(ReturnAfterFinished(source, duration));
        return true;
    }

    private bool TryConfigureSource(
        AudioSource source,
        AudioEventSO audioEvent,
        bool forceTwoDimensional,
        out AudioClip clip)
    {
        clip = null;
        if (source == null || audioEvent == null ||
            !audioEvent.TryGetClip(out clip))
        {
            return false;
        }

        ConfigureSource(source, audioEvent, clip, forceTwoDimensional);
        return true;
    }

    private static void ConfigureSource(
        AudioSource source,
        AudioEventSO audioEvent,
        AudioClip clip,
        bool forceTwoDimensional)
    {
        source.clip = clip;
        source.outputAudioMixerGroup = audioEvent.OutputGroup;
        source.volume = audioEvent.GetVolume();
        source.pitch = audioEvent.GetPitch();
        source.spatialBlend = forceTwoDimensional ? 0f : audioEvent.SpatialBlend;
        source.minDistance = audioEvent.MinDistance;
        source.maxDistance = audioEvent.MaxDistance;
    }

    private AudioSource GetSource()
    {
        return pool.Count > 0
            ? pool.Dequeue()
            : CreateSource("PooledAudioSource");
    }

    private AudioSource CreateSource(string sourceName)
    {
        var child = new GameObject(sourceName);
        child.hideFlags |= HideFlags.DontSaveInEditor;
        child.transform.SetParent(transform, false);
        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        return source;
    }

    private void RecoverOwnedSources(int poolSize)
    {
        pool.Clear();
        musicSourceA = null;
        musicSourceB = null;
        voiceSource = null;

        var extras = new List<GameObject>();
        foreach (Transform child in transform)
        {
            AudioSource source = child.GetComponent<AudioSource>();
            if (source == null)
                continue;

            bool claimed = false;
            if (child.name == "PooledAudioSource" && pool.Count < poolSize)
            {
                StopAndClear(source);
                source.playOnAwake = false;
                pool.Enqueue(source);
                claimed = true;
            }
            else if (child.name == "MusicSourceA" && musicSourceA == null)
            {
                musicSourceA = source;
                claimed = true;
            }
            else if (child.name == "MusicSourceB" && musicSourceB == null)
            {
                musicSourceB = source;
                claimed = true;
            }
            else if (child.name == "VoiceSource" && voiceSource == null)
            {
                voiceSource = source;
                claimed = true;
            }

            if (claimed)
                child.gameObject.hideFlags |= HideFlags.DontSaveInEditor;
            else if (IsOwnedSourceName(child.name))
                extras.Add(child.gameObject);
        }

        for (int i = 0; i < extras.Count; i++)
        {
            if (Application.isPlaying)
                Destroy(extras[i]);
            else
                DestroyImmediate(extras[i]);
        }
    }

    private static bool IsOwnedSourceName(string sourceName)
    {
        return sourceName == "PooledAudioSource" ||
               sourceName == "MusicSourceA" ||
               sourceName == "MusicSourceB" ||
               sourceName == "VoiceSource";
    }

    private static void ConfigureDedicatedSource(AudioSource source, bool loop)
    {
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.loop = loop;
    }

    private IEnumerator ReturnAfterFinished(AudioSource source, float delay)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, delay));
        ReturnSource(source);
    }

    private void ReturnSource(AudioSource source)
    {
        if (source == null)
            return;

        source.Stop();
        source.clip = null;
        source.outputAudioMixerGroup = null;
        source.volume = 1f;
        source.pitch = 1f;
        source.loop = false;
        source.transform.SetParent(transform, false);
        pool.Enqueue(source);
    }

    private IEnumerator CrossfadeMusic(
        AudioSource previous,
        AudioSource next,
        float targetVolume,
        float duration)
    {
        float previousStart = previous != null ? previous.volume : 0f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (previous != null)
                previous.volume = Mathf.Lerp(previousStart, 0f, t);
            if (next != null)
                next.volume = Mathf.Lerp(0f, targetVolume, t);
            yield return null;
        }

        StopAndClear(previous);
        if (next != null)
            next.volume = targetVolume;
        musicFadeRoutine = null;
    }

    private IEnumerator FadeOutMusic(float duration)
    {
        AudioSource sourceA = musicSourceA;
        AudioSource sourceB = musicSourceB;
        float startA = sourceA != null ? sourceA.volume : 0f;
        float startB = sourceB != null ? sourceB.volume : 0f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (sourceA != null)
                sourceA.volume = Mathf.Lerp(startA, 0f, t);
            if (sourceB != null)
                sourceB.volume = Mathf.Lerp(startB, 0f, t);
            yield return null;
        }

        StopAndClear(sourceA);
        StopAndClear(sourceB);
        musicFadeRoutine = null;
    }

    private void StopMusicFade()
    {
        if (musicFadeRoutine == null)
            return;
        StopCoroutine(musicFadeRoutine);
        musicFadeRoutine = null;
    }

    private static void StopAndClear(AudioSource source)
    {
        if (source == null)
            return;
        source.Stop();
        source.clip = null;
        source.outputAudioMixerGroup = null;
        source.volume = 1f;
        source.pitch = 1f;
    }

    private void EnsureInitialized()
    {
        if (!isInitialized)
            Initialize(GameFlowRoot.Instance);
    }
}
