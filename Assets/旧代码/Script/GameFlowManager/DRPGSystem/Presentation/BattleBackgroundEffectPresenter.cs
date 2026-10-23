using System.Collections.Generic;
using UnityEngine;

public sealed class BattleBackgroundEffectPresenter : MonoBehaviour
{
    [SerializeField] private CanvasGroup dimOverlay;
    private readonly Dictionary<BattlePresentationScope, float> capturedAlpha =
        new Dictionary<BattlePresentationScope, float>();

    public void Capture(BattlePresentationScope scope)
    {
        if (!capturedAlpha.ContainsKey(scope))
            capturedAlpha.Add(scope, dimOverlay != null ? dimOverlay.alpha : 0f);
    }

    public void Apply(BattleBackgroundPresentationPolicy policy, float intensity)
    {
        if (dimOverlay == null)
            return;
        dimOverlay.gameObject.SetActive(true);
        dimOverlay.alpha = policy == BattleBackgroundPresentationPolicy.Keep
            ? 0f
            : Mathf.Clamp01(intensity);
        dimOverlay.interactable = false;
        dimOverlay.blocksRaycasts = false;
    }

    public void Restore(BattlePresentationScope scope)
    {
        if (!capturedAlpha.TryGetValue(scope, out float alpha))
            return;
        if (dimOverlay != null)
        {
            dimOverlay.alpha = alpha;
            dimOverlay.gameObject.SetActive(alpha > 0.001f);
        }
        capturedAlpha.Remove(scope);
    }
}
