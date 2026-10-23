using UnityEngine;

public enum MeleeHitGeometrySource
{
    CharacterAnchor,
    WeaponBoundingBox
}

[System.Serializable]
public class MeleeHitWindowDefinition
{
    [Header("窗口")]
    public string windowId = "Primary";

    [Header("判定来源")]
    public MeleeHitGeometrySource geometrySource = MeleeHitGeometrySource.CharacterAnchor;

    [Header("角色肢体攻击")]
    public string characterAnchorId = "KickFoot";

    [Min(0.01f)]
    public float anchorRadius = 0.35f;

    [Header("武器近战攻击")]
    public string weaponBoundingBoxId;

    [Header("Spine 事件")]
    public string openEvent = "MeleeHitOpen";
    public string closeEvent = "MeleeHitClose";

    [Header("命中配置")]
    public MeleeHitProfileSO hitProfile;
}