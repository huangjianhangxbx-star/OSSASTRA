/*
 * 脚本：GroundLayer
 * 功能：保存一个研究地表图层的 Tilemap、刷子、擦除替代和来源证据。
 * 边界：仅是可序列化作者数据，不执行刷地、不拥有角色状态，也不创建材质。
 * 协作：GroundResearchSurface 限定图层归属；Editor 作者工具使用同一份字段完成编辑。
 */
using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Ossastra.GroundResearch
{
    [Serializable]
    public sealed class GroundLayer
    {
        [Tooltip("作者工具中显示的图层名称。")]
        public string displayName;

        [Tooltip("必须属于当前地表层级，并使用该地表登记的 Grid。")]
        public Tilemap tilemap;

        [Tooltip("绘制时写入的 TileBase；可引用现有 RuleTile。")]
        public TileBase paintTile;

        [Tooltip("擦除时写回的地块；留空表示清除这一层，不删除其他层。")]
        public TileBase eraseReplacement;

        [Tooltip("作者工具预览；不替代 Tile 的真实表现或碰撞数据。")]
        public Sprite preview;

        [Header("来源证据；未知项保持为空，不把 Unity 适配当作原作事实")]
        public string sourceId;
        public string sourceContainer;
        public string sourceObjectId;
        [TextArea(2, 5)] public string evidence;

        [Header("局部细节纹理与真实世界重复尺度")]
        public Texture2D detailTexture;
        [Min(0.0001f)] public float worldRepeatPeriod = 1f;
    }
}
