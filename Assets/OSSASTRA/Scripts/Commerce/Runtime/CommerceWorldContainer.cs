/* 脚本：CommerceWorldContainer
 * 功能：地图中的箱子代理配置；逻辑内容留在唯一账本，代理卸载不销毁物品。
 * 边界：稳定ID由编辑期安装生成并落盘；空箱同样登记，初始物品仅首次注册授予。
 */
using System;
using UnityEngine;
using Ossastra.Inventory;
namespace Ossastra.Commerce.Runtime
{
    public sealed class CommerceWorldContainer:MonoBehaviour
    {
        public string instanceId,mapId="commerce-map",displayName="储物箱";
        [Min(1)] public int width=5,height=8;
        public InventoryGrant[] initialContents=Array.Empty<InventoryGrant>();
#if UNITY_EDITOR
        private void OnValidate()
        {
            if(!Application.isPlaying&&gameObject.scene.IsValid()&&!UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(gameObject.scene)&&!UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)&&string.IsNullOrWhiteSpace(instanceId))
                instanceId="box:"+Guid.NewGuid().ToString("N");
        }
#endif
    }
}
