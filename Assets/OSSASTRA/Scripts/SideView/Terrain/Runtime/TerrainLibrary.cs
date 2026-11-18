/* 素材库收藏可复用的图件引用，支持从工坊添加项目自有 Sprite 或图片。 */
using System;
using UnityEngine;

namespace Ossastra.SideView.TerrainWorkshop
{
    public sealed class TerrainLibrary : ScriptableObject
    {
        public TerrainArt[] items = Array.Empty<TerrainArt>();
    }
}
