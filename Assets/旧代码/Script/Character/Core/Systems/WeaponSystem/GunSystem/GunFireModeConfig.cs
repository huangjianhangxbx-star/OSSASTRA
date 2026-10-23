using System;
using UnityEngine;

[Serializable]
public class GunFireModeConfig
{
    [Header("射击模式")]
    public GunFireModeType fireModeType = GunFireModeType.SemiAuto;

    [Header("通用")]
    [Tooltip("两次可触发射击行为之间的间隔。SemiAuto / FullAuto 会直接使用它。")]
    [Min(0.01f)]
    public float fireInterval = 0.15f;

    [Header("Burst 连发配置")]
    [Tooltip("Burst 模式下一次输入打几发。三连发填 3。")]
    [Min(1)]
    public int burstCount = 3;

    [Tooltip("Burst 内部每一发之间的间隔。")]
    [Min(0.01f)]
    public float burstShotInterval = 0.08f;

    [Tooltip("一轮 Burst 结束后，下一轮 Burst 允许开始的间隔。")]
    [Min(0.01f)]
    public float burstCycleInterval = 0.35f;

    [Header("自动射击配置")]
    [Tooltip("FullAuto 是否允许按住攻击键持续开火。")]
    public bool allowHoldToFire = true;

    [Header("蓄力 / 特殊预留")]
    [Min(0f)]
    public float chargeTime = 0f;
}