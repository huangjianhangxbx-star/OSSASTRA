/* 场景的地图与显示材质入口。地图引用由工坊管理，冻结的派生物可在关闭工坊后直接载入。 */
using UnityEngine;

namespace Ossastra.SideView.TerrainWorkshop
{
    [DisallowMultipleComponent]
    public sealed class TerrainWorkshopRoot : MonoBehaviour
    {
        public TerrainMap map;
        public Material cardMaterial;
        public bool locked, frozen;
    }
}
