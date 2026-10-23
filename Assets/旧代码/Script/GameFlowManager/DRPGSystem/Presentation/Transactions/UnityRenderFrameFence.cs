using System.Collections;
using UnityEngine;

// Frame count means EndOfFrame was actually observed, never Update or elapsed time.
public sealed class UnityRenderFrameFence : MonoBehaviour, IDRPGBattlePresentationClock
{
    // Unity freezes unscaledTime for the whole frame. Sampling wall time here
    // gave every cue in one batch a different timestamp (even microseconds
    // apart), so a valid command could look stale before it reached its sink.
    public double Now => Time.unscaledTimeAsDouble;
    public long RenderedFrame { get; private set; }
    private Coroutine rendering;
    private void OnEnable() { rendering = StartCoroutine(ObserveRenderedFrames()); }
    private void OnDisable() { if (rendering != null) StopCoroutine(rendering); rendering = null; }
    private IEnumerator ObserveRenderedFrames()
    {
        var fence = new WaitForEndOfFrame();
        while (true) { yield return fence; RenderedFrame++; }
    }
}
