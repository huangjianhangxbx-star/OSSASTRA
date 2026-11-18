/* 套件保存方向槽的图件和接点。稳定 ID 由新建或复制时的资产 GUID 分配，改名和排序不会串材质。 */
using System;
using System.Linq;
using UnityEngine;

namespace Ossastra.SideView.TerrainWorkshop
{
    public enum TerrainPlacement { Repeat, Whole, Scatter }

    [Serializable]
    public sealed class TerrainRule
    {
        public TerrainSlot slot;
        public TerrainArt art;
        public float start, end = 1, anchor = .48f, span = 1, spacing, overlap = .03f, offset, minLength = .25f, weight = 1;
        public Vector2 size;
        public bool flip, rotate;
        public TerrainPlacement placement;
    }

    public sealed class TerrainKit : ScriptableObject
    {
        [SerializeField] private string stableId;
        public string Id => stableId;
        public Color fill = new Color(.028f, .037f, .058f, 1);
        public TerrainRule[] rules = Array.Empty<TerrainRule>();
        public int state;
        public TerrainRule Rule(TerrainSlot slot) => rules.FirstOrDefault(rule => rule != null && rule.slot == slot);

#if UNITY_EDITOR
        public void SetAuthoringId(string guid) => stableId = guid;
#endif
    }
}
