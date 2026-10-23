using UnityEngine;

[CreateAssetMenu(menuName = "ANKALUTE/GameRender/Global Style Profile")]
public sealed class GameRenderProfile : ScriptableObject
{
    [Header("场景：黑线与图形暗部")]
    public bool enableWorldStyle = true;
    [Range(0f, 1f)] public float inkStrength = 0.16f;
    [Range(0f, 1f)] public float darknessThreshold = 0.13f;
    [Range(0f, 1f)] public float darkSpread = 0.13f;
    [Range(0f, 2f)] public float outerLineStrength = 0.22f;
    [Range(0f, 2f)] public float innerLineStrength = 0.045f;
    [Range(0f, 1f)] public float highlightLift = 0.035f;
    [Range(0f, 1f)] public float roughEdgeStrength = 0f;

    [Header("场景：细颗粒（后处理之后、UI之前）")]
    public bool enableGrain = true;
    [Range(0f, 0.12f)] public float grainStrength = 0.006f;
    [Range(0.5f, 4f)] public float grainPixelSize = 1.25f;
    [Range(0f, 8f)] public float grainFramesPerSecond = 1.5f;

    [HideInInspector] public bool debugClassification;

    [Header("角色默认值（材质逐项覆盖的基础值；仅手动指定对象使用角色材质）")]
    [Range(0f, 1f)] public float characterInkStrength = 0.18f;
    [Range(0f, 1f)] public float characterOutlineStrength = 0.25f;
    [Range(0f, 1f)] public float characterHighlightLift = 0.04f;
}
