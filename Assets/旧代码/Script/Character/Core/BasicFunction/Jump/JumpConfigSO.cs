using UnityEngine;

/// <summary>
/// 跳跃参数集中配置。通过 ScriptableObject 在 Inspector 中调整。
/// </summary>
[CreateAssetMenu(
    fileName = "JumpConfig",
    menuName = "ScriptableObject/战斗配置/跳跃配置")]
public class JumpConfigSO : ScriptableObject
{
    [Header("起跳")]
    [Tooltip("沿 GravityUp 的起跳初速度（米/秒）")]
    [Min(0.1f)]
    public float jumpSpeed = 9f;

    [Header("重力")]
    [Tooltip("基础重力加速度（米/秒²）")]
    [Min(0.1f)]
    public float baseGravityAcceleration = 30f;

    [Tooltip("上升时重力倍率")]
    [Min(0.01f)]
    public float riseGravityMultiplier = 1.0f;

    [Tooltip("下落时重力倍率")]
    [Min(0.01f)]
    public float fallGravityMultiplier = 1.8f;

    [Tooltip("接近顶点时重力倍率（越小悬停感越强）")]
    [Min(0.01f)]
    public float apexGravityMultiplier = 0.6f;

    [Tooltip("判定接近顶点的速度阈值（米/秒）")]
    [Min(0.01f)]
    public float apexSpeedThreshold = 2f;

    [Tooltip("松开跳跃键后上升速度的保留比例")]
    [Range(0f, 1f)]
    public float jumpCutMultiplier = 0.5f;

    [Tooltip("最大下落速度（米/秒）")]
    [Min(0.1f)]
    public float maxFallSpeed = 20f;

    [Header("时间窗口")]
    [Tooltip("土狼时间（秒）：离开平台后仍可起跳的窗口")]
    [Min(0f)]
    public float coyoteTime = 0.1f;

    [Tooltip("输入缓存时间（秒）：提前按键后落地自动起跳的窗口")]
    [Min(0f)]
    public float jumpBufferTime = 0.1f;

    [Header("空中控制")]
    [Tooltip("空中加速度（米/秒²）")]
    [Min(0f)]
    public float airAcceleration = 20f;

    [Tooltip("空中减速度（米/秒²），无输入时")]
    [Min(0f)]
    public float airDeceleration = 10f;

    [Tooltip("空中最大横向速度（米/秒）")]
    [Min(0.1f)]
    public float maxAirMoveSpeed = 5f;

    [Header("探测")]
    [Tooltip("地面探测距离（含 skinWidth）")]
    [Min(0.01f)]
    public float groundProbeDistance = 0.15f;

    [Tooltip("天花板探测距离")]
    [Min(0.01f)]
    public float ceilingProbeDistance = 0.1f;

    [Tooltip("碰撞皮肤宽度")]
    [Min(0.001f)]
    public float collisionSkinWidth = 0.02f;

    [Header("落地")]
    [Tooltip("轻落地持续时间（秒）")]
    [Min(0.01f)]
    public float landingDuration = 0.03f;

    [Tooltip("重落地速度阈值（预留）")]
    [Min(0f)]
    public float heavyLandingSpeedThreshold = 15f;

    [Header("调试")]
    public bool enableDebugLogs = true;

#if UNITY_EDITOR
    private void OnValidate()
    {
        jumpSpeed = Mathf.Max(0.1f, jumpSpeed);
        baseGravityAcceleration = Mathf.Max(0.1f, baseGravityAcceleration);
        maxFallSpeed = Mathf.Max(0.1f, maxFallSpeed);
        landingDuration = Mathf.Max(0.01f, landingDuration);
    }
#endif
}
