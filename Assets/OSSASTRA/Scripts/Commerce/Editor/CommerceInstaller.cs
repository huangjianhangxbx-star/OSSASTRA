/* 脚本：CommerceInstaller
 * 功能：在已有完整地图显式安装COM模块、可编辑SAMPLE目录/商人/箱代理；不重建角色、UI皮肤或音声。
 * 边界：仅编辑器操作，重复安装拒绝；稳定ID首次创建并落盘，复制后的冲突由校验明确报错。
 */
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Ossastra.Inventory;
using Ossastra.UserInterface;
using Ossastra.Commerce.Runtime;
using Ossastra.Commerce.Presentation;
namespace Ossastra.Commerce.Editor
{
    public static class CommerceInstaller
    {
        public const string AssetRoot="Assets/OSSASTRA/Commerce";
        [MenuItem("XingHai/COM-01/安装到当前完整地图")]
        public static void InstallMenu(){InstallInCurrentScene();}
        public static CommerceService InstallInCurrentScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("仅编辑模式可安装");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(!scene.IsValid()||!scene.isLoaded)throw new InvalidOperationException("当前地图未加载");
            var inventory=UnityEngine.Object.FindObjectsOfType<InventorySession>(true).Where(i=>i.gameObject.scene==scene).Single();
            var view=UnityEngine.Object.FindObjectsOfType<InventoryView>(true).Where(v=>v.gameObject.scene==scene).Single();
            var installed=UnityEngine.Object.FindObjectsOfType<CommerceService>(true).Where(s=>s.gameObject.scene==scene).ToArray();
            if(installed.Length>1||installed.Length==1&&(installed[0].inventory!=null||installed[0].inventoryView!=null||installed[0].merchants.Length>0))throw new InvalidOperationException("该地图已安装CommerceService，未重复创建");
            var co=inventory.Coordinator;if(co==null||co.Characters.Count!=2||co.Characters.Any(c=>c==null))throw new InvalidOperationException("需要既有的双角色协调器");
            var serialized=new SerializedObject(inventory);var catalog=serialized.FindProperty("catalog");
            var items=Enumerable.Range(0,catalog.arraySize).Select(i=>catalog.GetArrayElementAtIndex(i).objectReferenceValue as Ossastra.Characters.ItemDefinitionSO).Where(d=>d!=null).ToArray();
            var coin=items.Single(d=>d.definitionId=="trade-coin");
            EnsureFolder(AssetRoot);EnsureFolder(AssetRoot+"/Configs");EnsureFolder(AssetRoot+"/Prefabs");
            var general=CreateCatalog("COM01_General",items,false);var personal=CreateCatalog("COM01_Florecia",items,true);
            var host=installed.Length==1?installed[0].gameObject:new GameObject("CommerceService");
            if(installed.Length==0)Undo.RegisterCreatedObjectUndo(host,"Install commerce");
            var service=installed.Length==1?installed[0]:host.AddComponent<CommerceService>();Undo.RecordObject(service,"Wire commerce");
            service.inventory=inventory;service.inventoryView=view;service.userInterface=host.GetComponent<CommerceUIRoot>();if(service.userInterface==null)service.userInterface=Undo.AddComponent<CommerceUIRoot>(host);
            service.merchantCatalog=new[]{general,personal};service.mapId="commerce-map";service.profileId="commerce-main";
            var npc=UnityEngine.Object.FindObjectsOfType<Ossastra.Npcs.NpcRoot>(true).FirstOrDefault(n=>n.gameObject.scene==scene);
            if(npc==null)throw new InvalidOperationException("完整地图缺少NpcRoot样板，未制造第二套根");
            var one=CreateMerchant(npc.gameObject,"行商 · 迁移测试目录",new Vector3(-1.2f,.03f,0),"merchant:com01-general-a",general);
            var two=CreateMerchant(npc.gameObject,"旅商 · 独立存货",new Vector3(-5f,.03f,0),"merchant:com01-general-b",general);
            var heroine=co.Characters[1].gameObject;Undo.RecordObject(heroine,"Attach personal shop");
            var shop=heroine.GetComponent<MerchantShop>();if(shop==null)shop=Undo.AddComponent<MerchantShop>(heroine);
            shop.instanceId="merchant:florecia-personal";shop.displayName="芙蕾茜娅 · SAMPLE个人店";shop.personalShop=true;shop.definition=personal;
            shop.dialogue=CommerceSampleDialogueInstaller.EnsureSampleAsset(AssetRoot+"/Configs/COM01_FloreciaDialogue.asset");
            service.merchants=new[]{one,two,shop};
            var boxA=CreateBox("box:com01-supplies-a","物资箱 A",new Vector3(1.4f,.03f,-2),5,8,items.Where(d=>d.definitionId=="food-0").Select(d=>new InventoryGrant{definition=d,quantity=4}).ToArray());
            var boxB=CreateBox("box:com01-supplies-b","物资箱 B",new Vector3(3.2f,.03f,-2),3,3,items.Where(d=>d.definitionId=="magic-0").Select(d=>new InventoryGrant{definition=d,quantity=2}).ToArray());
            service.worldContainers=new[]{boxA,boxB};
            // 测试钱币只配置到这个新地图的首次初始化表，不改共享物品SO、旧场或已有档案。
            var initial=serialized.FindProperty("initialTeam");
            if(!Enumerable.Range(0,initial.arraySize).Any(i=>initial.GetArrayElementAtIndex(i).FindPropertyRelative("definition").objectReferenceValue==coin))
            {var index=initial.arraySize;initial.InsertArrayElementAtIndex(index);initial.GetArrayElementAtIndex(index).FindPropertyRelative("definition").objectReferenceValue=coin;initial.GetArrayElementAtIndex(index).FindPropertyRelative("quantity").intValue=300;serialized.ApplyModifiedProperties();}
            var route=inventory.GetComponent<InventoryInteraction>();if(route==null)throw new InvalidOperationException("缺少唯一InventoryInteraction");
            var routeSo=new SerializedObject(route);routeSo.FindProperty("commerce").objectReferenceValue=service;routeSo.FindProperty("commerceUi").objectReferenceValue=service.userInterface;routeSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(service);EditorUtility.SetDirty(shop);EditorSceneManager.MarkSceneDirty(scene);
            return service;
        }
        private static MerchantDefinitionSO CreateCatalog(string file,Ossastra.Characters.ItemDefinitionSO[] items,bool personal)
        {
            var path=AssetRoot+"/Configs/"+file+".asset";var existing=AssetDatabase.LoadAssetAtPath<MerchantDefinitionSO>(path);if(existing!=null)return existing;
            var config=ScriptableObject.CreateInstance<MerchantDefinitionSO>();config.definitionId=personal?"sample-florecia":"sample-general";config.refreshSeconds=300;
            var ids=personal?new[]{"food-1","magic-1","sample-al-regen"}:new[]{"food-0","food-1","food-2","magic-0","magic-1","sample-al-bomb","sample-seed","treasure-0"};
            config.goods=ids.Select((id,n)=>new MerchantGoodsConfig{item=items.Single(d=>d.definitionId==id),buyPrice=10+n*3,sellPrice=4+n,targetStock=personal?5:10}).ToArray();
            AssetDatabase.CreateAsset(config,path);AssetDatabase.SaveAssetIfDirty(config);return config;
        }
        private static MerchantShop CreateMerchant(GameObject prototype,string name,Vector3 position,string id,MerchantDefinitionSO definition)
        {
            var go=UnityEngine.Object.Instantiate(prototype);Undo.RegisterCreatedObjectUndo(go,"Create merchant");go.name=name;go.transform.position=position;
            var npc=go.GetComponent<Ossastra.Npcs.NpcRoot>();if(npc==null)throw new InvalidOperationException("样板缺少NpcRoot");
            var npcSo=new SerializedObject(npc);npcSo.FindProperty("npcId").stringValue="npc:"+id;npcSo.ApplyModifiedProperties();
            var shop=go.GetComponent<MerchantShop>();if(shop==null)shop=Undo.AddComponent<MerchantShop>(go);shop.instanceId=id;shop.displayName=name;shop.definition=definition;shop.personalShop=false;return shop;
        }
        private static CommerceWorldContainer CreateBox(string id,string name,Vector3 position,int width,int height,InventoryGrant[] grants)
        {
            var root=new GameObject(name);Undo.RegisterCreatedObjectUndo(root,"Create world box");root.transform.position=position;
            var box=root.AddComponent<CommerceWorldContainer>();box.instanceId=id;box.mapId="commerce-map";box.displayName=name;box.width=width;box.height=height;box.initialContents=grants;
            var mesh=GameObject.CreatePrimitive(PrimitiveType.Cube);mesh.name="TemporaryBoxVisual";mesh.transform.SetParent(root.transform,false);mesh.transform.localPosition=Vector3.up*.2f;mesh.transform.localScale=new Vector3(.6f,.4f,.45f);
            var collider=mesh.GetComponent<Collider>();if(collider!=null)UnityEngine.Object.DestroyImmediate(collider);
            return box;
        }
        private static void EnsureFolder(string path)
        {if(AssetDatabase.IsValidFolder(path))return;var parent=path.Substring(0,path.LastIndexOf('/'));EnsureFolder(parent);AssetDatabase.CreateFolder(parent,path.Substring(path.LastIndexOf('/')+1));}
    }
}
