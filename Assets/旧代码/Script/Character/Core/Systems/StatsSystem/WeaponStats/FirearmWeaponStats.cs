using System;
using UnityEngine;

[Serializable]
public class FirearmWeaponStats
{
    [Header("射击")]
    [Tooltip("射程")]
    [Min(0f)]
    public float range = 30f;

    [Tooltip("子弹 / 射线速度。Hitscan 武器可以不用。")]
    [Min(0f)]
    public float projectileSpeed = 60f;

    [Header("弹药")]
    [Min(1)]
    public int magazineSize = 30;

    [Min(0f)]
    public float reloadTime = 1.2f;

    [Header("散布 / 精度")]
    [Tooltip("基础散布角度，单位：度")]
    [Min(0f)]
    public float spreadAngle = 1.5f;

    [Tooltip("移动时额外散布")]
    [Min(0f)]
    public float movingSpreadAdd = 1.0f;

    [Tooltip("连续射击时每发增加的散布")]
    [Min(0f)]
    public float spreadIncreasePerShot = 0.2f;

    [Tooltip("最大散布")]
    [Min(0f)]
    public float maxSpreadAngle = 5f;

    [Header("后坐力")]
    [Min(0f)]
    public float recoil = 1f;

    [Min(0f)]
    public float recoilRecoverSpeed = 8f;

    [Header("瞄准")]
    [Tooltip("开镜/进入瞄准状态的速度")]
    [Min(0f)]
    public float aimEnterTime = 0.15f;

    [Tooltip("瞄准时移动速度倍率")]
    [Range(0f, 1f)]
    public float aimMoveSpeedMultiplier = 0.65f;
}