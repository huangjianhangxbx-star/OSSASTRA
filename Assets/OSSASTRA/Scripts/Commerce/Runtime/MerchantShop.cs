/* 脚本：MerchantShop
 * 功能：NPC或既有芙蕾角色上的外部商店能力；保存编辑期稳定实例ID，根角色不承担交易业务。
 * 边界：OnEnable不生成ID、不补货。复制实例须在安装器中明确分配新ID，重复一律拒绝。
 */
using UnityEngine;
namespace Ossastra.Commerce.Runtime
{
    [DisallowMultipleComponent]
    public sealed class MerchantShop:MonoBehaviour
    {
        public string instanceId;
        public string displayName="行商（测试目录）";
        public MerchantDefinitionSO definition;
        [Min(.1f)] public float interactionRange=3;
        public bool personalShop;
        public CommerceDialogueSO dialogue;
        public string Id=>instanceId;
#if UNITY_EDITOR
        private void OnValidate()
        {
            if(!Application.isPlaying&&gameObject.scene.IsValid()&&!UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(gameObject.scene)&&!UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)&&string.IsNullOrWhiteSpace(instanceId))
            {
                instanceId="merchant:"+System.Guid.NewGuid().ToString("N");
                var npc=GetComponent<Ossastra.Npcs.NpcRoot>();
                if(npc!=null){var identity=new UnityEditor.SerializedObject(npc);identity.FindProperty("npcId").stringValue="npc:"+instanceId;identity.ApplyModifiedPropertiesWithoutUndo();}
            }
        }
#endif
    }
}
