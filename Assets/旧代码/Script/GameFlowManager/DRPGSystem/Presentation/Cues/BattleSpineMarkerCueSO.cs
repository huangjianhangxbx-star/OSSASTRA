using UnityEngine;

[CreateAssetMenu(
    fileName = "BattleSpineMarkerCue",
    menuName = "ANKALUTE/DRPG/Presentation Cues/Spine Marker")]
public sealed class BattleSpineMarkerCueSO : BattlePresentationCueSO
{
    [SerializeField] private string markerName =
        BattleSpineMarkerNames.WindupComplete;
    [Min(0)]
    [SerializeField] private int markerIndex;
    [Min(0f)]
    [SerializeField] private float timeoutSeconds = 1.5f;

    public override BattlePresentationCueKind Kind =>
        BattlePresentationCueKind.SpineMarker;
    public string MarkerName => string.IsNullOrWhiteSpace(markerName)
        ? BattleSpineMarkerNames.WindupComplete
        : markerName;
    public int MarkerIndex => Mathf.Max(0, markerIndex);
    public float TimeoutSeconds => Mathf.Max(0f, timeoutSeconds);
    public BattleSpineMarker Marker =>
        new BattleSpineMarker(MarkerName, MarkerIndex);
}
