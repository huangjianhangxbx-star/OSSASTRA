/* 脚本：GroundWorkshopAuthoring
 * 功能：提供注册地图的作者门、图层锁、显示刷新和自有配方/主题创建入口。
 * 边界：不保存第二份格子；刷新不会重置手绘。换材事务在同名partial中，磁盘创建有独立精确清单。
 * 协作：窗口/Brush共享所有权；可选派生显示收到Rebuild通知，主题以不可变快照接续。
 */
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Ossastra.GroundResearch.Editor
{
    public static partial class GroundWorkshopAuthoring
    {
        public static bool CanEdit(GroundWorkshopMap map,out string reason) => GroundWorkshopOwnership.CanEdit(map,out reason);
        public static bool IsLayerLocked(GroundWorkshopMap map,int index) => map==null || map.surface==null ||
            map.surface.layers==null || index<0 || index>=map.surface.layers.Length || map.surface.layers[index]==null ||
            map.surface.layers[index].isLocked || map.theme==null || map.theme.layers==null || index>=map.theme.layers.Length ||
            map.theme.layers[index]==null || map.theme.layers[index].locked;
        internal static void RequireLayer(GroundWorkshopMap map,int index,bool allowLocked=false)
        {
            GroundWorkshopOwnership.Require(map);
            if(index<0||index>=map.surface.layers.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if(!allowLocked&&IsLayerLocked(map,index)) throw new InvalidOperationException("该层已锁定；请先在工坊明确解锁。");
        }
        public static void SetLayerLocked(GroundWorkshopMap map,int layer,bool value)
        {
            RequireLayer(map,layer,true); CancelPreview();
            Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(value?"锁定地表层":"解锁地表层");
            using(var ledger=new GroundAssetLedger())
            {
                try
                {
                    var snapshot=UnityEngine.Object.Instantiate(map.theme);snapshot.layers=CloneSlots(map.theme.layers);
                    snapshot.layers[layer].locked=value;
                    ledger.Create(snapshot,map.registration.assetFolder+"/Theme_"+Guid.NewGuid().ToString("N")+".asset");
                    Undo.RecordObjects(new UnityEngine.Object[]{map,map.surface},"调整地表作者锁");
                    map.theme=snapshot;map.surface.layers[layer].isLocked=value;
                    EditorUtility.SetDirty(map);EditorUtility.SetDirty(map.surface);
                    EditorSceneManager.MarkSceneDirty(map.gameObject.scene);Undo.FlushUndoRecordObjects();
                    if(Undo.GetCurrentGroup()==group)Undo.CollapseUndoOperations(group);
                    else Debug.LogWarning("锁状态已完成；外部Undo组保持独立，未跨组合并。",map);
                    ledger.Commit();
                }
                catch
                {
                    Undo.FlushUndoRecordObjects();
                    if(Undo.GetCurrentGroup()==group)Undo.RevertAllDownToGroup(group);
                    else ledger.Commit(); // 外部历史可能仍引用新快照，保留文件，绝不留悬空引用。
                    throw;
                }
                finally{Undo.IncrementCurrentGroup();}
            }
        }
        public static void Rebuild(GroundWorkshopMap map)
        {
            GroundWorkshopOwnership.Require(map);
            Bounds bounds=map.surface.WorldBounds;
            for(int i=0;i<map.surface.layers.Length;i++)
            {
                var layer=map.surface.layers[i]; var binding=map.layerBindings[i];
                var material=binding.ownedMaterial;
                material.SetVector("_GroundBounds",new Vector4(bounds.min.x,bounds.min.z,bounds.max.x,bounds.max.z));
                material.SetTexture("_SecondTex",binding.recipe.detailTexture);
                material.SetFloat("_WorldPeriod",layer.worldRepeatPeriod);
                material.SetFloat("_Cutoff",binding.recipe.cutoff);
                material.SetFloat("_BaseCoverage",map.theme.layers[i].purpose==GroundLayerPurpose.Base?1:0);
                var attributes=GroundTilemapAttributes.Capture(layer.tilemap,map.surface.EditableCells);
                layer.tilemap.RefreshAllTiles();attributes.Restore();EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
            }
            GroundWorkshopDisplayEvents.Notify(map);
            SceneView.RepaintAll();
        }
        public static GroundRecipe CreateRecipe(string displayName,Texture2D detail,GroundEdgeFamily edge,float period,float cutoff,string assetPath,string detailAssetId="")
        {
            RequireDefinitionPath(assetPath);
            var recipe=ScriptableObject.CreateInstance<GroundRecipe>();
            recipe.assetId="original-recipe-"+Guid.NewGuid().ToString("N"); recipe.displayName=displayName;
            recipe.detailAssetId=string.IsNullOrEmpty(detailAssetId)?recipe.assetId+":detail":detailAssetId;
            recipe.detailTexture=detail;recipe.edgeFamily=edge;recipe.worldRepeatPeriod=period;recipe.cutoff=cutoff;
            recipe.evidence="UNITY_ADAPTATION 用户自有配方；底纹与边缘身份分别记录，未自动认定无缝/美术效果。";
            if(!GroundWorkshopOwnership.ValidateRecipe(recipe,out string reason))
            {UnityEngine.Object.DestroyImmediate(recipe);throw new InvalidOperationException(reason);}
            using(var ledger=new GroundAssetLedger())
            {
                // 不能为配方把用户原纹理Importer改成Repeat；必要时导入一份受控PNG副本。
                if(detail.wrapMode!=TextureWrapMode.Repeat)
                {
                    string source=AssetDatabase.GetAssetPath(detail);
                    if(string.IsNullOrEmpty(source)||!source.EndsWith(".png",StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("底纹需Repeat采样；请用“导入自绘PNG”创建受控副本后再保存配方。");
                    recipe.detailTexture=ImportDetailTexture(source,ledger);
                }
                ledger.Create(recipe,assetPath);ledger.Commit();return recipe;
            }
        }
        public static GroundTheme CreateTheme(string displayName,GroundThemeLayer[] slots,GroundDecorationDefinition[] decorations,string assetPath)
        {
            RequireDefinitionPath(assetPath); ValidateSlots(slots);
            var theme=ScriptableObject.CreateInstance<GroundTheme>();theme.assetId="original-theme-"+Guid.NewGuid().ToString("N");
            theme.displayName=displayName;theme.layers=CloneSlots(slots);theme.decorations=(decorations??Array.Empty<GroundDecorationDefinition>()).ToArray();
            theme.evidence="UNITY_ADAPTATION 自有主题定义；不保存或重置地图布局。";
            using(var ledger=new GroundAssetLedger()){ledger.Create(theme,assetPath);ledger.Commit();return theme;}
        }
        public static Texture2D ImportDetailTexture(string sourcePath)
        {
            if(!GroundWorkshopOwnership.ContextReady(out string reason))throw new InvalidOperationException(reason);
            using(var ledger=new GroundAssetLedger()){var texture=ImportDetailTexture(sourcePath,ledger);ledger.Commit();return texture;}
        }
        private static Texture2D ImportDetailTexture(string sourcePath,GroundAssetLedger ledger)
        {
            if(string.IsNullOrEmpty(sourcePath)||!sourcePath.EndsWith(".png",StringComparison.OrdinalIgnoreCase)||!File.Exists(sourcePath))
                throw new InvalidOperationException("请选择实际存在的自有PNG底纹。");
            var info=new FileInfo(sourcePath);if(info.Length>64*1024*1024)throw new InvalidOperationException("PNG超过本轮64MiB单图上限。");
            var probe=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try{if(!ImageConversion.LoadImage(probe,File.ReadAllBytes(sourcePath))||probe.width<16||probe.height<16||probe.width>4096||probe.height>4096)
                throw new InvalidOperationException("底纹须为16—4096像素PNG；不要求正方形，无缝性请用3×3预览确认。");}
            finally{UnityEngine.Object.DestroyImmediate(probe);}
            string target=GroundWorkshopOwnership.Root+"/Palettes/Imports/"+Guid.NewGuid().ToString("N")+".png";
            ledger.Reserve(target);File.Copy(sourcePath,target,false);AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(target) as TextureImporter;
            if(importer==null)throw new InvalidOperationException("受控PNG导入器创建失败："+target);
            importer.textureType=TextureImporterType.Default;importer.wrapMode=TextureWrapMode.Repeat;importer.sRGBTexture=true;
            importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.maxTextureSize=4096;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(target);
        }
        private static void RequireDefinitionPath(string path)
        {
            if(!GroundWorkshopOwnership.ContextReady(out string reason))throw new InvalidOperationException(reason);
            if(!GroundWorkshopOwnership.IsUnder(path,GroundWorkshopOwnership.Root+"/Palettes")||!path.EndsWith(".asset",StringComparison.Ordinal))
                throw new InvalidOperationException("请保存到Assets/OSSASTRA/GroundWorkshop/Palettes内的自有.asset，不覆盖原资源。");
        }
        internal static void ValidateSlots(GroundThemeLayer[] slots)
        {
            if(slots==null||slots.Length<1||slots.Length>16||slots.Any(s=>s==null)||slots[0].purpose!=GroundLayerPurpose.Base||
                slots.Skip(1).Any(s=>s.purpose==GroundLayerPurpose.Base)||slots.Any(s=>string.IsNullOrEmpty(s.slotId))||
                slots.Select(s=>s.slotId).Distinct().Count()!=slots.Length)
                throw new InvalidOperationException("主题须有1—16个唯一槽位，仅首层为基础地面。");
            foreach(var slot in slots)if(!GroundWorkshopOwnership.ValidateRecipe(slot.recipe,out string reason))
                throw new InvalidOperationException(slot.displayName+"："+reason);
        }
        internal static GroundThemeLayer[] CloneSlots(GroundThemeLayer[] slots)=>slots.Select(s=>new GroundThemeLayer
        {slotId=s.slotId,displayName=s.displayName,recipe=s.recipe,purpose=s.purpose,locked=s.locked}).ToArray();
    }
}
#endif
