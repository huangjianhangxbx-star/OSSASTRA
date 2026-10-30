/* 脚本：GroundWorkshopRecipeChanges
 * 功能：预检、临时预览、取消和提交当前层配方变更；保存/关场/Play/重载前释放临时显示。
 * 边界：预览只用HideAndDontSave的Native Tilemap；只有Commit写作者数据，异种Tile不被隐式迁移。
 * 协作：可选显示后端只读真实临时Tilemap；Existing替换识别Tile，Future追加独立材质变体层。
 */
#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
namespace Ossastra.GroundResearch.Editor
{
    public sealed class GroundRecipeChange
    {
        public GroundWorkshopMap map;
        public int layerIndex,knownCount;
        public GroundRecipe recipe;
        public GroundReplacementScope scope;
        public string description;
        public string[] unknownTiles=Array.Empty<string>();
        public bool IsValid {get;internal set;}
        internal TileBase previousTile;
        internal Material previousMaterial;
        internal string previousRevision,cellSignature,recipeSignature;
    }
    [InitializeOnLoad]
    public static partial class GroundWorkshopAuthoring
    {
        static GroundWorkshopAuthoring()
        {
            EditorSceneManager.sceneSaving+=BeforeSceneSaving;
            EditorSceneManager.sceneClosing+=BeforeSceneClosing;
            AssemblyReloadEvents.beforeAssemblyReload+=CancelPreview;
            EditorApplication.quitting+=CancelPreview;
            EditorApplication.playModeStateChanged+=BeforeModeChange;
        }
        private static void BeforeSceneSaving(Scene scene,string path)
        {if(ActivePreview!=null&&ActivePreview.map!=null&&ActivePreview.map.gameObject.scene==scene)CancelPreview();}
        private static void BeforeSceneClosing(Scene scene,bool removing)
        {if(ActivePreview!=null&&ActivePreview.map!=null&&ActivePreview.map.gameObject.scene==scene)CancelPreview();}
        private static void BeforeModeChange(PlayModeStateChange state)
        {if(state==PlayModeStateChange.ExitingEditMode)CancelPreview();}
        private static GameObject previewRoot;
        private static TileBase previewTile;
        private static Material previewMaterial;
        private static TilemapRenderer previewOriginal;
        private static bool previousEnabled;
        public static bool HasPreview => previewRoot!=null;
        public static GroundRecipeChange ActivePreview {get;private set;}
        public static bool TryGetPreviewTilemap(GroundWorkshopMap map,out Tilemap tilemap,out Material material,out int layer,out GroundReplacementScope scope)
        {
            tilemap=null;material=null;layer=-1;scope=GroundReplacementScope.ExistingAndFuture;
            if(ActivePreview==null||ActivePreview.map!=map||previewRoot==null)return false;
            tilemap=previewRoot.GetComponent<Tilemap>();material=previewMaterial;
            layer=ActivePreview.layerIndex;scope=ActivePreview.scope;return tilemap!=null&&material!=null;
        }
        public static GroundRecipeChange PrepareReplacement(GroundWorkshopMap map,int layer,GroundRecipe recipe,GroundReplacementScope scope)
        {
            RequireLayer(map,layer);
            if(!GroundWorkshopOwnership.ValidateRecipe(recipe,out string reason))throw new InvalidOperationException(reason);
            var current=map.surface.layers[layer];var cells=map.surface.EditableCells.allPositionsWithin;
            int known=0;var unknown=new System.Collections.Generic.HashSet<string>();
            foreach(var cell in cells){var tile=current.tilemap.GetTile(cell);if(tile==current.paintTile)known++;else if(tile!=null)unknown.Add(tile.name+" ["+AssetDatabase.GetAssetPath(tile)+"]");}
            bool valid=scope==GroundReplacementScope.FutureOnly?map.surface.layers.Length<16:unknown.Count==0;
            return new GroundRecipeChange{map=map,layerIndex=layer,recipe=recipe,scope=scope,knownCount=known,unknownTiles=unknown.ToArray(),
                previousTile=current.paintTile,previousMaterial=map.layerBindings[layer].ownedMaterial,previousRevision=recipe.revision,
                cellSignature=CellSignature(map,layer),recipeSignature=RecipeSignature(recipe),IsValid=valid,
                description=scope==GroundReplacementScope.FutureOnly?"新增一个后续绘制变体层；当前"+known+"个旧格与原材质保留。":
                    "替换当前层"+known+"个已识别格和后续笔画；异种Tile "+unknown.Count+"类。布局、手摆装饰不变。"};
        }
        public static void Preview(GroundRecipeChange change)
        {
            ValidateChange(change);CancelPreview();var map=change.map;var source=map.surface.layers[change.layerIndex].tilemap;
            try
            {
                var oldPurpose=map.theme.layers[change.layerIndex].purpose;
                var purpose=change.scope==GroundReplacementScope.FutureOnly&&oldPurpose==GroundLayerPurpose.Base?GroundLayerPurpose.Overlay:oldPurpose;
                var template=purpose==GroundLayerPurpose.Base?change.recipe.edgeFamily.fillTemplate:change.recipe.edgeFamily.paintTemplate;
                previewTile=UnityEngine.Object.Instantiate(template);previewTile.hideFlags=HideFlags.HideAndDontSave;
                previewMaterial=new Material(Shader.Find(GroundWorkshopOwnership.ShaderName)){hideFlags=HideFlags.HideAndDontSave};
                var b=map.surface.WorldBounds;previewMaterial.SetVector("_GroundBounds",new Vector4(b.min.x,b.min.z,b.max.x,b.max.z));
                previewMaterial.SetTexture("_SecondTex",change.recipe.detailTexture);previewMaterial.SetFloat("_WorldPeriod",change.recipe.worldRepeatPeriod);
                previewMaterial.SetFloat("_Cutoff",change.recipe.cutoff);previewMaterial.SetFloat("_BaseCoverage",purpose==GroundLayerPurpose.Base?1:0);
                previewRoot=new GameObject("配方预览_不保存作者格"){hideFlags=HideFlags.HideAndDontSave};
                previewRoot.transform.SetParent(source.transform.parent,false);previewRoot.transform.localPosition=source.transform.localPosition+Vector3.up*.0005f;
                previewRoot.transform.localRotation=source.transform.localRotation;previewRoot.transform.localScale=source.transform.localScale;
                var tilemap=previewRoot.AddComponent<Tilemap>();tilemap.orientation=source.orientation;tilemap.orientationMatrix=source.orientationMatrix;
                tilemap.tileAnchor=source.tileAnchor;tilemap.color=source.color;
                var renderer=previewRoot.AddComponent<TilemapRenderer>();renderer.sharedMaterial=previewMaterial;
                var sourceRenderer=source.GetComponent<TilemapRenderer>();renderer.sortingOrder=sourceRenderer.sortingOrder;
                renderer.sortingLayerID=sourceRenderer.sortingLayerID;renderer.mode=TilemapRenderer.Mode.Chunk;
                var properties=new MaterialPropertyBlock();sourceRenderer.GetPropertyBlock(properties);
                properties.SetTexture("_SecondTex",change.recipe.detailTexture);properties.SetFloat("_WorldPeriod",change.recipe.worldRepeatPeriod);
                properties.SetFloat("_Cutoff",change.recipe.cutoff);properties.SetFloat("_BaseCoverage",purpose==GroundLayerPurpose.Base?1:0);renderer.SetPropertyBlock(properties);
                if(change.scope==GroundReplacementScope.ExistingAndFuture)
                {
                    var attributes=GroundTilemapAttributes.Capture(source,map.surface.EditableCells);
                    foreach(var cell in map.surface.EditableCells.allPositionsWithin)if(source.GetTile(cell)==change.previousTile)tilemap.SetTile(cell,previewTile);
                    attributes.CopyTo(tilemap,change.previousTile,previewTile);
                    previewOriginal=source.GetComponent<TilemapRenderer>();previousEnabled=previewOriginal.enabled;previewOriginal.enabled=false;
                }
                else
                {
                    // 未来模式用左下3×3临时小样说明新外观，不把旧层假装成已被替换。
                    var bounds=map.surface.EditableCells;
                    for(int y=bounds.yMin;y<Mathf.Min(bounds.yMin+3,bounds.yMax);y++)for(int x=bounds.xMin;x<Mathf.Min(bounds.xMin+3,bounds.xMax);x++)tilemap.SetTile(new Vector3Int(x,y,0),previewTile);
                }
                ActivePreview=change;GroundWorkshopDisplayEvents.Notify(map);SceneView.RepaintAll();
            }
            catch{CancelPreview();throw;}
        }
        public static void CancelPreview()
        {
            var changedMap=ActivePreview==null?null:ActivePreview.map;
            if(previewOriginal!=null&&!previewOriginal.enabled)previewOriginal.enabled=previousEnabled;
            previewOriginal=null;ActivePreview=null;
            if(previewRoot!=null)UnityEngine.Object.DestroyImmediate(previewRoot);
            if(previewTile!=null)UnityEngine.Object.DestroyImmediate(previewTile);
            if(previewMaterial!=null)UnityEngine.Object.DestroyImmediate(previewMaterial);
            previewRoot=null;previewTile=null;previewMaterial=null;GroundWorkshopDisplayEvents.Notify(changedMap);SceneView.RepaintAll();
        }
        public static void CancelPreview(GroundRecipeChange owner)
        {if(owner!=null&&ActivePreview==owner)CancelPreview();}
        public static int Commit(GroundRecipeChange change)
        {
            ValidateChange(change);CancelPreview();var map=change.map;int index=change.layerIndex;
            var previousTheme=map.theme;var previousLayers=map.surface.layers.ToArray();var previousBindings=map.layerBindings.ToArray();
            var previousRenderer=previousLayers[index].tilemap.GetComponent<TilemapRenderer>();
            var previousCells=new System.Collections.Generic.List<AuthorCell>();
            foreach(var cell in map.surface.EditableCells.allPositionsWithin)
                if(previousLayers[index].tilemap.GetTile(cell)==change.previousTile)previousCells.Add(new AuthorCell(previousLayers[index].tilemap,cell));
            GroundTheme themeSnapshot=null;GroundMapLayerBinding createdBinding=null;GameObject createdLayer=null;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("应用地表配方");
            using(var ledger=new GroundAssetLedger())
            {
                try
                {
                    Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[]{map,map.surface},"应用地表配方");
                    themeSnapshot=UnityEngine.Object.Instantiate(map.theme);themeSnapshot.layers=CloneSlots(map.theme.layers);map.theme=themeSnapshot;
                    var oldSlot=map.theme.layers[index];
                    var slot=new GroundThemeLayer{slotId=change.scope==GroundReplacementScope.FutureOnly?Guid.NewGuid().ToString("N"):oldSlot.slotId,
                        displayName=change.recipe.displayName+(change.scope==GroundReplacementScope.FutureOnly?"（后续变体）":""),
                        recipe=change.recipe,purpose=change.scope==GroundReplacementScope.FutureOnly&&oldSlot.purpose==GroundLayerPurpose.Base?GroundLayerPurpose.Overlay:oldSlot.purpose};
                    var binding=GroundWorkshopMaps.CreateBinding(map,slot,"Revision_"+Guid.NewGuid().ToString("N"),ledger);createdBinding=binding;
                    Tilemap tilemap;
                    if(change.scope==GroundReplacementScope.FutureOnly)
                    {
                        index=map.surface.layers.Length;var go=new GameObject(slot.displayName);createdLayer=go;Undo.RegisterCreatedObjectUndo(go,"新增后续绘制变体层");
                        go.transform.SetParent(map.transform,false);go.transform.localPosition=new Vector3(0,.006f+index*.002f,0);
                        tilemap=go.AddComponent<Tilemap>();tilemap.orientation=Tilemap.Orientation.XZ;
                        var renderer=go.AddComponent<TilemapRenderer>();renderer.mode=TilemapRenderer.Mode.Chunk;renderer.sortingOrder=index;renderer.sharedMaterial=binding.ownedMaterial;
                        map.surface.layers=map.surface.layers.Concat(new[]{GroundWorkshopMaps.DescribeLayer(slot,tilemap,binding)}).ToArray();
                        map.layerBindings=map.layerBindings.Concat(new[]{binding}).ToArray();map.theme.layers=map.theme.layers.Concat(new[]{slot}).ToArray();
                    }
                    else
                    {
                        tilemap=map.surface.layers[index].tilemap;var renderer=tilemap.GetComponent<TilemapRenderer>();
                        Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[]{tilemap,renderer},"替换当前层已有同类地块");
                        foreach(var cell in map.surface.EditableCells.allPositionsWithin)if(tilemap.GetTile(cell)==change.previousTile)GroundWorkshopMaps.ReplaceCell(tilemap,cell,binding.ownedTile);
                        renderer.sharedMaterial=binding.ownedMaterial;map.layerBindings[index]=binding;map.theme.layers[index]=slot;
                        map.surface.layers[index]=GroundWorkshopMaps.DescribeLayer(slot,tilemap,binding);
                    }
                    EditorUtility.SetDirty(map);EditorUtility.SetDirty(map.surface);EditorUtility.SetDirty(map.theme);
                    // 新主题先完整落盘，Scene只切换引用；旧Scene即使保存失败仍引用完整旧快照。
                    ledger.Create(map.theme,map.registration.assetFolder+"/Theme_"+Guid.NewGuid().ToString("N")+".asset");
                    EditorSceneManager.MarkSceneDirty(map.gameObject.scene);Rebuild(map);
                    if(change.scope==GroundReplacementScope.ExistingAndFuture)
                        foreach(var cell in previousCells)cell.RestoreAttributesIfStillOurs(binding.ownedTile);
                    Undo.FlushUndoRecordObjects();
                    if(Undo.GetCurrentGroup()==group)Undo.CollapseUndoOperations(group);
                    else Debug.LogWarning("检测到外部Undo组；本次换材已完成并保留其资源，没有跨组合并或回滚其他操作。",map);
                    ledger.Commit();return index;
                }
                catch
                {
                    Undo.FlushUndoRecordObjects();
                    if(Undo.GetCurrentGroup()==group)Undo.RevertAllDownToGroup(group);
                    else
                    {
                        // 不能跨Undo组回滚外部对象；仅还原仍属于本事务的地图绑定/格子。
                        if(map!=null&&map.surface!=null&&(map.theme==themeSnapshot||map.theme==previousTheme))
                        {
                            map.theme=previousTheme;map.surface.layers=previousLayers;map.layerBindings=previousBindings;
                            if(createdBinding!=null)
                            {
                                foreach(var cell in previousCells)cell.RestoreIfStillOurs(createdBinding.ownedTile);
                                if(previousRenderer!=null&&previousRenderer.sharedMaterial==createdBinding.ownedMaterial)
                                    previousRenderer.sharedMaterial=change.previousMaterial;
                            }
                            if(createdLayer!=null&&createdLayer.transform.childCount==0&&createdLayer.GetComponents<Component>().All(c=>c is Transform||c is Tilemap||c is TilemapRenderer))
                                UnityEngine.Object.DestroyImmediate(createdLayer);
                            EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
                        }
                        ledger.Commit();
                        Debug.LogError("换材异常已仅恢复本事务作者绑定；外部Undo保持独立，新资产保留供历史引用/恢复，未删除仍可能被引用的文件。",map);
                    }
                    throw;
                }
                finally{Undo.IncrementCurrentGroup();}
            }
        }
        private static void ValidateChange(GroundRecipeChange change)
        {
            if(change==null)throw new ArgumentNullException(nameof(change));RequireLayer(change.map,change.layerIndex);
            if(!change.IsValid)throw new InvalidOperationException("配方预检未通过：存在异种Tile或已达16层，请先处理；未写入任何绑定。");
            if(!GroundWorkshopOwnership.ValidateRecipe(change.recipe,out string reason))throw new InvalidOperationException(reason);
            var layer=change.map.surface.layers[change.layerIndex];
            if(layer.paintTile!=change.previousTile||change.map.layerBindings[change.layerIndex].ownedMaterial!=change.previousMaterial||
                    change.recipe.revision!=change.previousRevision||RecipeSignature(change.recipe)!=change.recipeSignature||CellSignature(change.map,change.layerIndex)!=change.cellSignature)
                throw new InvalidOperationException("目标、配方或作者格已改变，请重新预检，旧预览不能回写新目标。");
        }
        private static string RecipeSignature(GroundRecipe recipe)=>recipe.assetId+"|"+recipe.revision+"|"+
            recipe.detailTexture.GetInstanceID()+"|"+recipe.edgeFamily.GetInstanceID()+"|"+recipe.edgeFamily.paintTemplate.GetInstanceID()+"|"+
            recipe.edgeFamily.fillTemplate.GetInstanceID()+"|"+recipe.worldRepeatPeriod.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"|"+
            recipe.cutoff.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
        private sealed class AuthorCell
        {
            private readonly Tilemap map;private readonly Vector3Int cell;private readonly TileBase tile;
            private readonly Color color;private readonly TileFlags flags;private readonly Matrix4x4 matrix;
            public AuthorCell(Tilemap map,Vector3Int cell)
            {this.map=map;this.cell=cell;tile=map.GetTile(cell);color=map.GetColor(cell);flags=map.GetTileFlags(cell);matrix=map.GetTransformMatrix(cell);}
            public void RestoreIfStillOurs(TileBase expected)
            {
                if(map==null||map.GetTile(cell)!=expected)return;
                map.SetTile(cell,tile);map.SetTileFlags(cell,TileFlags.None);map.SetColor(cell,color);
                if((flags&TileFlags.LockTransform)==0)map.SetTransformMatrix(cell,matrix);map.SetTileFlags(cell,flags);
            }
            public void RestoreAttributesIfStillOurs(TileBase expected)
            {
                if(map==null||map.GetTile(cell)!=expected)return;
                map.SetTileFlags(cell,TileFlags.None);map.SetColor(cell,color);
                if((flags&TileFlags.LockTransform)==0)map.SetTransformMatrix(cell,matrix);map.SetTileFlags(cell,flags);
            }
        }
        private static string CellSignature(GroundWorkshopMap map,int index)
        {
            var tilemap=map.surface.layers[index].tilemap;var text=new System.Text.StringBuilder();
            foreach(var cell in map.surface.EditableCells.allPositionsWithin)
            {var tile=tilemap.GetTile(cell);if(tile!=null)text.Append(cell.x).Append(',').Append(cell.y).Append(':').Append(tile.GetInstanceID()).Append(';');}
            return text.ToString();
        }
    }
}
#endif
