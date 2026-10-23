using UnityEngine;

public abstract class BattlePresentationCueSO : ScriptableObject
{
    [SerializeField] private string cueId = "cue";
    [SerializeField] private BattlePresentationCueKind kind;
    [Min(0f)]
    [SerializeField] private float durationSeconds;

    public string CueId => string.IsNullOrWhiteSpace(cueId) ? name : cueId;
    public virtual BattlePresentationCueKind Kind => kind;
    public float DurationSeconds => durationSeconds;
}
