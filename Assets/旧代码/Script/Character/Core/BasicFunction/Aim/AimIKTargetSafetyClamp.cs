using UnityEngine;

/// <summary>
/// IK 安全钳制配置。
/// </summary>
[System.Serializable]
public struct AimIKSafetyConfig
{
    [Tooltip("是否启用安全钳制")]
    public bool enableSafetyClamp;

    [Tooltip("最大瞄准角度（度），不允许达到或超过90")]
    [Range(45f, 89f)]
    public float maxAimAngle;

    [Tooltip("最小目标距离")]
    public float minimumForwardDistance;

    [Tooltip("钳制后是否保留原始目标距离")]
    public bool preserveTargetDistance;

    [Tooltip("是否绘制调试Gizmo")]
    public bool enableClampDebug;

    public static AimIKSafetyConfig Default => new AimIKSafetyConfig
    {
        enableSafetyClamp = true,
        maxAimAngle = 82f,
        minimumForwardDistance = 0.05f,
        preserveTargetDistance = true,
        enableClampDebug = false
    };
}

/// <summary>
/// IK 钳制结果。
/// </summary>
public struct AimIKClampResult
{
    public Vector2 rawWorldTarget;
    public Vector2 clampedWorldTarget;
    public bool wasClamped;
    public float rawAngle;
    public float clampedAngle;
}

/// <summary>
/// 纯 C# 辅助类，在 AimIKController 最终写入 Spine IK Target 前进行前方扇区限制。
/// 防止 IK Target 进入角色视觉朝向的背面。
/// 不独立 Tick，不写 DataBus，不控制角色朝向。
/// </summary>
public static class AimIKTargetSafetyClamp
{
    /// <summary>
    /// 将原始世界目标钳制到角色前方扇区内。
    /// 使用方案B：在世界空间中计算，基于角色稳定朝向判定前方半平面。
    /// </summary>
    public static AimIKClampResult Clamp(
        Vector2 rawWorldTarget,
        Vector2 aimOriginWorld,
        int stableFacingSign,
        AimIKSafetyConfig config)
    {
        AimIKClampResult result = new AimIKClampResult
        {
            rawWorldTarget = rawWorldTarget,
            clampedWorldTarget = rawWorldTarget,
            wasClamped = false,
            rawAngle = 0f,
            clampedAngle = 0f
        };

        if (!config.enableSafetyClamp)
            return result;

        // 角色前方方向（世界空间）
        // stableFacingSign > 0 = 右, 前方 = Vector2.right
        // stableFacingSign < 0 = 左, 前方 = Vector2.left
        Vector2 forward = stableFacingSign > 0 ? Vector2.right : Vector2.left;

        Vector2 offset = rawWorldTarget - aimOriginWorld;
        float distance = offset.magnitude;

        if (distance < 0.001f)
        {
            // 目标就在原点，放到最小前方距离
            result.clampedWorldTarget = aimOriginWorld + forward * config.minimumForwardDistance;
            result.wasClamped = true;
            return result;
        }

        // 计算原始角度（相对于前方）
        float forwardDot = Vector2.Dot(offset.normalized, forward);
        float rawAngle = Mathf.Acos(Mathf.Clamp(forwardDot, -1f, 1f)) * Mathf.Rad2Deg;
        result.rawAngle = rawAngle;

        if (rawAngle <= config.maxAimAngle)
        {
            // 在允许范围内，不钳制
            result.clampedAngle = rawAngle;
            return result;
        }

        // 需要钳制：将目标投影到扇区边界
        // 计算偏移在前方轴和侧向轴上的分量
        float forwardComponent = Vector2.Dot(offset, forward);
        Vector2 sideAxis = new Vector2(-forward.y, forward.x); // 前方的垂直方向
        float sideComponent = Vector2.Dot(offset, sideAxis);

        // 如果前方分量为负（目标在背后），强制为最小前方距离
        if (forwardComponent < config.minimumForwardDistance)
            forwardComponent = config.minimumForwardDistance;

        // 钳制角度
        float clampedAngleRad = config.maxAimAngle * Mathf.Deg2Rad;

        // 使用角度钳制重建偏移
        float preserveDistance = config.preserveTargetDistance
            ? Mathf.Max(distance, config.minimumForwardDistance)
            : Mathf.Max(forwardComponent / Mathf.Cos(clampedAngleRad), config.minimumForwardDistance);

        // 方向：保持侧向符号，限制角度
        float sideSign = sideComponent >= 0f ? 1f : -1f;

        Vector2 clampedOffset = forward * Mathf.Cos(clampedAngleRad) * preserveDistance
                              + sideAxis * Mathf.Sin(clampedAngleRad) * preserveDistance * sideSign;

        result.clampedWorldTarget = aimOriginWorld + clampedOffset;
        result.clampedAngle = config.maxAimAngle;
        result.wasClamped = true;

        return result;
    }
}
