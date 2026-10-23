using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(fileName = "AE_NewAudioEvent",menuName = "ScriptableObject/ÒôÆµ/ÒôÆµEvent"
)]
public class AudioEventSO : ScriptableObject
{
    [SerializeField] private AudioClip[] clips;
    [SerializeField] private AudioMixerGroup outputGroup;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField, Range(0f, 0.5f)] private float volumeRandomRange = 0.05f;

    [Header("Pitch")]
    [SerializeField] private float pitch = 1f;
    [SerializeField] private float pitchRandomRange = 0.04f;

    [Header("Spatial")]
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 0f;
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 20f;

    public AudioMixerGroup OutputGroup => outputGroup;
    public float SpatialBlend => spatialBlend;
    public float MinDistance => minDistance;
    public float MaxDistance => maxDistance;

    public bool TryGetClip(out AudioClip clip)
    {
        clip = null;

        if (clips == null || clips.Length == 0)
            return false;

        clip = clips[Random.Range(0, clips.Length)];
        return clip != null;
    }

    public float GetVolume()
    {
        return Mathf.Clamp01(volume + Random.Range(-volumeRandomRange, volumeRandomRange));
    }

    public float GetPitch()
    {
        return Mathf.Max(0.01f, pitch + Random.Range(-pitchRandomRange, pitchRandomRange));
    }
}