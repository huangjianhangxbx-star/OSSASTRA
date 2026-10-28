/* 脚本：CommerceTemplateInstaller
 * 功能：从已安装地图保存自有交易根/独立NPC商人/箱子模板，并显式登记本地图新增实例。
 * 负责：PreviewScene内复制、清除模板场景引用/实例ID、校验后集中更新根的实例列表。
 * 边界：已有资产只读跳过，不保存其他dirty资产或地图；不在运行时全局猜服务，不自动改已有存档ID。
 */
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Ossastra.Characters;
using Ossastra.Commerce.Presentation;
using Ossastra.Commerce.Runtime;
using Ossastra.Npcs;
namespace Ossastra.Commerce.Editor
{
    public static class CommerceTemplateInstaller
    {
        public const string RootPrefabPath="Assets/OSSASTRA/Commerce/Prefabs/PF_CommerceService.prefab";
        public const string MerchantPrefabPath="Assets/OSSASTRA/Commerce/Prefabs/PF_Merchant.prefab";
        public const string WorldContainerPrefabPath="Assets/OSSASTRA/Commerce/Prefabs/PF_WorldContainer.prefab";
        public static string[] TemplatePaths=>new[]{RootPrefabPath,MerchantPrefabPath,WorldContainerPrefabPath};

        /// <summary>只读取已安装的当前编辑地图，所有临时复制在预览场中销毁；不会覆盖已有模板。</summary>
        public static void SaveTemplates(CommerceService service)
        {
            var scene=EditableScene();
            if(service==null||service.gameObject.scene!=scene||EditorUtility.IsPersistent(service))throw new ArgumentException("需要当前完整地图已安装的CommerceService实例");
            SaveIfMissing(RootPrefabPath,()=>
            {
                if(service.userInterface==null||service.userInterface.gameObject!=service.gameObject)throw new InvalidOperationException("交易根需持有同对象的CommerceUIRoot，不能把外部场景UI引用写入模板");
                var copy=UnityEngine.Object.Instantiate(service.gameObject);copy.name="CommerceService";
                var root=copy.GetComponent<CommerceService>();root.inventory=null;root.inventoryView=null;root.merchants=Array.Empty<MerchantShop>();root.worldContainers=Array.Empty<CommerceWorldContainer>();
                root.userInterface=copy.GetComponent<CommerceUIRoot>();root.profileId="commerce-main";root.saveDirectory="";
                return copy;
            });
            SaveIfMissing(MerchantPrefabPath,()=>
            {
                var merchant=(service.merchants??Array.Empty<MerchantShop>()).FirstOrDefault(m=>m!=null&&!m.personalShop&&m.GetComponent<NpcRoot>()!=null);
                if(merchant==null||merchant.gameObject.scene!=scene)throw new InvalidOperationException("当前安装缺少同地图独立NpcRoot普通商人样本");
                if(merchant.GetComponent<Ossastra.Characters.CharacterRoot>()!=null)throw new InvalidOperationException("普通商人模板不能复制玩家CharacterRoot");
                var copy=UnityEngine.Object.Instantiate(merchant.gameObject);copy.name="Merchant";copy.GetComponent<MerchantShop>().instanceId="";return copy;
            });
            SaveIfMissing(WorldContainerPrefabPath,()=>
            {
                var container=(service.worldContainers??Array.Empty<CommerceWorldContainer>()).FirstOrDefault(c=>c!=null&&c.gameObject.scene==scene);
                if(container==null)throw new InvalidOperationException("当前安装缺少普通世界箱样本");
                var copy=UnityEngine.Object.Instantiate(container.gameObject);copy.name="WorldContainer";copy.GetComponent<CommerceWorldContainer>().instanceId="";return copy;
            });
            Debug.Log("[Commerce] 模板已保存或保留现有资产：\n"+string.Join("\n",TemplatePaths),service);
        }
        private static void SaveIfMissing(string path,Func<GameObject> create)
        {
            if(ExistingAsset(path))return;
            EnsureFolder(path.Substring(0,path.LastIndexOf('/')));
            var previous=SceneManager.GetActiveScene();var preview=EditorSceneManager.NewPreviewScene();GameObject copy=null;
            try
            {
                // Tuanjie 1.10的PreviewScene不能设为active；先复制，再立即移入预览场，不保存原场。
                copy=create();SceneManager.MoveGameObjectToScene(copy,preview);copy.transform.SetParent(null,false);copy.transform.localPosition=Vector3.zero;copy.transform.localRotation=Quaternion.identity;
                // OnValidate必须排除PreviewScene，模板本身保持空ID，落图后才生成并保存稳定身份。
                foreach(var merchant in copy.GetComponentsInChildren<MerchantShop>(true))merchant.instanceId="";
                foreach(var container in copy.GetComponentsInChildren<CommerceWorldContainer>(true))container.instanceId="";
                if(ExistingAsset(path))return;
                var asset=PrefabUtility.SaveAsPrefabAsset(copy,path,out var success);
                if(!success||asset==null)throw new InvalidOperationException("模板保存失败，未报告成功："+path);
                AssetDatabase.SaveAssetIfDirty(asset);
            }
            finally
            {
                if(copy!=null)UnityEngine.Object.DestroyImmediate(copy);
                if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }
        private static bool ExistingAsset(string path)
        {
            var existing=AssetDatabase.LoadMainAssetAtPath(path);
            if(existing!=null)
            {
                if(!(existing is GameObject)||!PrefabUtility.IsPartOfPrefabAsset(existing))throw new InvalidOperationException("目标已有非Prefab资产，未覆盖："+path);
                return true;
            }
            var project=Directory.GetParent(Application.dataPath).FullName;
            if(!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path))||File.Exists(Path.Combine(project,path)))throw new InvalidOperationException("目标已有无法加载的资产，未覆盖："+path);
            return false;
        }
        private static void EnsureFolder(string path)
        {
            string current="Assets";var parts=path.Split('/');
            for(int index=1;index<parts.Length;index++)
            {var next=current+"/"+parts[index];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[index]);current=next;}
        }
        private static Scene EditableScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请退出Play后在实际地图登记交易实例");
            var scene=SceneManager.GetActiveScene();
            if(!scene.IsValid()||!scene.isLoaded||EditorSceneManager.IsPreviewScene(scene))throw new InvalidOperationException("当前不是可登记的真实地图场景");
            return scene;
        }

        [MenuItem("XingHai/COM-01/登记当前地图商人与箱子")]
        public static void RegisterCurrentMapMenu(){RegisterCurrentMapPlacements();}
        /// <summary>作者放置Prefab后显式执行；先完整校验，再通过Undo更新本地图唯一根，场景由作者自行保存。</summary>
        public static CommerceService RegisterCurrentMapPlacements()
        {
            var scene=EditableScene();var roots=scene.GetRootGameObjects();
            var services=roots.SelectMany(r=>r.GetComponentsInChildren<CommerceService>(true)).ToArray();
            if(services.Length!=1)throw new InvalidOperationException("当前地图必须有且只有一个CommerceService，实际："+services.Length);
            var service=services[0];
            var merchants=roots.SelectMany(r=>r.GetComponentsInChildren<MerchantShop>(true)).OrderBy(m=>m.Id,StringComparer.Ordinal).ToArray();
            var containers=roots.SelectMany(r=>r.GetComponentsInChildren<CommerceWorldContainer>(true)).OrderBy(c=>c.instanceId,StringComparer.Ordinal).ToArray();
            if(merchants.Any(m=>string.IsNullOrWhiteSpace(m.Id)||m.definition==null))throw new InvalidOperationException("商人稳定ID或商店目录缺失；新放置实例需先配置再登记");
            if(containers.Any(c=>string.IsNullOrWhiteSpace(c.instanceId)||string.IsNullOrWhiteSpace(c.mapId)||c.width<1||c.height<1))throw new InvalidOperationException("箱子稳定ID、地图或容量缺失");
            var ids=merchants.Select(m=>m.Id).Concat(containers.Select(c=>c.instanceId)).ToArray();
            if(ids.Distinct(StringComparer.Ordinal).Count()!=ids.Length)throw new InvalidOperationException("发现重复商人/箱子ID；未改写现有身份，请明确选择尚无存档的新副本再分配ID");
            foreach(var merchant in merchants)merchant.definition.Freeze();
            foreach(var container in containers)if(container.mapId!=service.mapId)throw new InvalidOperationException("箱子地图ID与当前交易根不同："+container.instanceId);
            Undo.RecordObject(service,"登记当前地图商人与箱子");service.merchants=merchants;service.worldContainers=containers;EditorUtility.SetDirty(service);EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Commerce] 已登记商人 "+merchants.Length+"、箱子 "+containers.Length+"；请保存当前地图。",service);return service;
        }

        [MenuItem("XingHai/COM-01/为所选新副本分配交易ID")]
        public static void AssignIdsToSelectedNewCopies()
        {
            var scene=EditableScene();var selected=Selection.gameObjects;
            if(selected.Length==0||selected.Any(go=>go==null||go.scene!=scene||EditorUtility.IsPersistent(go)))throw new InvalidOperationException("请只选择当前地图中尚未写入存档的新副本根对象");
            var merchants=selected.SelectMany(go=>go.GetComponents<MerchantShop>()).ToArray();
            var containers=selected.SelectMany(go=>go.GetComponents<CommerceWorldContainer>()).ToArray();
            if(merchants.Length+containers.Length==0)throw new InvalidOperationException("所选根对象没有MerchantShop或CommerceWorldContainer");
            foreach(var merchant in merchants){Undo.RecordObject(merchant,"为新商人副本分配ID");merchant.instanceId="merchant:"+Guid.NewGuid().ToString("N");EditorUtility.SetDirty(merchant);}
            foreach(var container in containers){Undo.RecordObject(container,"为新箱子副本分配ID");container.instanceId="box:"+Guid.NewGuid().ToString("N");EditorUtility.SetDirty(container);}
            EditorSceneManager.MarkSceneDirty(scene);Debug.Log("[Commerce] 仅所选新副本已分配新交易ID；已有存档实例不应使用此入口，请随后重新登记并保存地图。");
        }
    }
}
