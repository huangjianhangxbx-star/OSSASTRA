/*
 * 脚本：GroundWorkshopDecorations
 * 功能：已登记地图的装饰手摆、定种子散布、局部清生成与保护锁作者事务。
 * 负责：地图/实例所有权预检、世界间距/道路排除、有界确定性、单操作Undo和自有材质账本。
 * 不负责：相机控制、角色、玩法碰撞、自动重撒、源图片导入或原生根之外的位置副本。
 * 协作：GroundWorkshopOwnership守写入边界，Maps复制复用同一Owned判定；Orientation只派生视觉与脚点补偿。
 * 关键约束：只清区域内本工具未锁生成根；手摆、锁、外部物件和外部Undo组始终保留。
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ossastra.GroundResearch.Editor
{
    public static class GroundWorkshopDecorations
    {
        private const string ShaderName = "Ossastra/GroundWorkshop/DecorationCutout";
        private const int MaximumInstances = 512;
        private static string Prefix(GroundWorkshopMap map) => "Ossastra.GroundWorkshop:" + map.registration.mapId + ":";

        /// <summary>放置一个作者根，手摆默认不参与清生成；视觉脚点由地图级组件派生。</summary>
        public static GroundDecorationInstance Place(GroundWorkshopMap map, GroundDecorationDefinition definition, Vector3 world, bool manual = true)
        {
            RequireMap(map); RequireDefinition(definition);
            Require(Finite(world) && map.surface.ContainsCell(Cell(map,world)),"装饰脚点必须在当前地图逻辑范围内。");
            return Edit(map,"工坊放置装饰",tx =>
            {
                Material material=MaterialFor(map,tx); OrientationFor(map,tx);
                float scale=Mathf.Clamp(1f,definition.scaleRange.x,definition.scaleRange.y);
                var result=Spawn(map,definition,world,!manual,0,Prefix(map)+(manual?"manual":"single"),scale,false,material,tx);
                map.GetComponent<GroundDecorationOrientation>().Refresh();
                return result;
            });
        }

        /// <summary>显式重撒选区；只替换本区域本工具未锁生成根，数量/尝试均有上限。</summary>
        public static int Scatter(GroundWorkshopMap map, GroundDecorationDefinition[] variants, BoundsInt area, int seed, float densityPer100, float spacing, bool avoidRoads)
        {
            RequireMap(map);
            Require(variants!=null&&variants.Length>0&&variants.Length<=128,"请选择至少一个、最多128个合法装饰变体。");
            foreach(var definition in variants)RequireDefinition(definition);
            Require(GroundWorkshopOwnership.IsFinite(densityPer100)&&densityPer100>=0f&&GroundWorkshopOwnership.IsFinite(spacing)&&spacing>=.05f,"密度须为有限非负数，世界间距至少0.05米。");
            BoundsInt clipped=Clip(map,area); if(clipped.size.x==0||clipped.size.y==0)return 0;
            Require((long)clipped.size.x*clipped.size.y<=262144,"一次散布区域超过262144逻辑格，请缩小选区。");
            int roadMargin=map.theme.roadExclusionCells;
            Require(roadMargin>=0&&roadMargin<=3,"道路排除格数须在0到3之间。");
            var removable=Instances(map).Where(i=>Owned(map,i)&&i.generated&&!i.isLocked&&clipped.Contains(Cell(map,i.transform.position))).ToArray();
            var removableSet=new HashSet<GroundDecorationInstance>(removable);
            var spacingGrid=new SpacingGrid(spacing);
            // 连外部原生子物件也作为间距障碍，但不读取、修改或给它们补工具标记。
            foreach(Transform child in map.decorationRoot)
            {
                var instance=child.GetComponent<GroundDecorationInstance>();
                if(instance==null||!removableSet.Contains(instance))spacingGrid.Add(child.position);
            }
            var blocked=avoidRoads?RoadCells(map,clipped,roadMargin):new HashSet<Vector3Int>();
            Vector3 start=map.surface.grid.CellToWorld(clipped.min);
            Vector3 alongX=map.surface.grid.CellToWorld(new Vector3Int(clipped.xMax,clipped.yMin,0))-start;
            Vector3 alongY=map.surface.grid.CellToWorld(new Vector3Int(clipped.xMin,clipped.yMax,0))-start;
            double worldArea=Math.Abs(Vector3.Cross(alongX,alongY).y);
            int target=(int)Math.Min(MaximumInstances,Math.Floor(worldArea*densityPer100/100.0+.5));
            var random=new StableRandom(ConfigurationSeed(map,variants,clipped,seed,densityPer100,spacing,avoidRoads));
            var planned=new List<Placement>();
            int maximumAttempts=Math.Max(256,target*48);
            for(int attempt=0;attempt<maximumAttempts&&planned.Count<target;attempt++)
            {
                Vector3 world=start+alongX*random.Unit()+alongY*random.Unit();
                Vector3Int cell=Cell(map,world);
                if(!clipped.Contains(cell)||blocked.Contains(cell)||!spacingGrid.CanAdd(world))continue;
                var definition=variants[random.Index(variants.Length)];
                float scale=Mathf.Lerp(definition.scaleRange.x,definition.scaleRange.y,random.Unit());
                bool mirror=definition.allowMirror&&random.Unit()<.5f;
                planned.Add(new Placement{definition=definition,world=world,scale=scale,mirror=mirror});
                spacingGrid.Add(world);
            }
            // 布局先纯计算完成；任何参数/定义/道路错误都不会先清掉既有生成结果。
            return Edit(map,"工坊重撒装饰",tx =>
            {
                Material material=planned.Count==0?null:MaterialFor(map,tx);
                if(planned.Count>0)OrientationFor(map,tx);
                foreach(var instance in removable)tx.Remove(instance);
                string group=Prefix(map)+"scatter:"+clipped.xMin+","+clipped.yMin+","+clipped.size.x+","+clipped.size.y;
                foreach(var item in planned)Spawn(map,item.definition,item.world,true,seed,group,item.scale,item.mirror,material,tx);
                var orientation=map.GetComponent<GroundDecorationOrientation>();if(orientation!=null)orientation.Refresh();
                return planned.Count;
            });
        }

        /// <summary>仅删除选区中本工具的未锁生成实例；手摆/锁/外部层级保持。</summary>
        public static int EraseGenerated(GroundWorkshopMap map,BoundsInt area)
        {
            RequireMap(map);BoundsInt clipped=Clip(map,area);
            var targets=Instances(map).Where(i=>Owned(map,i)&&i.generated&&!i.isLocked&&clipped.Contains(Cell(map,i.transform.position))).ToArray();
            if(targets.Length==0)return 0;
            return Edit(map,"工坊清除生成装饰",tx=>
            {
                foreach(var instance in targets)tx.Remove(instance);
                var orientation=map.GetComponent<GroundDecorationOrientation>();if(orientation!=null)orientation.Refresh();
                return targets.Length;
            });
        }

        /// <summary>只为真实工具实例切换保护锁；一个Undo，不改变任何根变换。</summary>
        public static void SetProtected(GroundDecorationInstance instance,bool value)
        {
            Require(instance!=null,"请选择本工具装饰实例。");
            var map=instance.GetComponentInParent<GroundWorkshopMap>();RequireMap(map);
            Require(Owned(map,instance),"所选对象不是当前注册地图的本工具装饰实例。");
            if(instance.isLocked==value)return;
            Edit(map,"工坊装饰保护锁",tx=>
            {
                tx.CheckGroup();tx.locks.Add(new KeyValuePair<GroundDecorationInstance,bool>(instance,instance.isLocked));
                Undo.RecordObject(instance,"工坊装饰保护锁");instance.isLocked=value;EditorUtility.SetDirty(instance);return 0;
            });
        }

        private static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}
        private static bool Finite(Vector3 value)=>GroundWorkshopOwnership.IsFinite(value.x)&&GroundWorkshopOwnership.IsFinite(value.y)&&GroundWorkshopOwnership.IsFinite(value.z);
        private static void RequireMap(GroundWorkshopMap map)
        {
            GroundWorkshopOwnership.Require(map);
            Require(map.decorationRoot!=map.transform&&map.decorationRoot.gameObject.scene==map.gameObject.scene&&
                map.decorationRoot.GetComponents<Component>().All(c=>c is Transform),"装饰根必须是当前地图内的独立原生容器，不接受外部脚本或碰撞宿主。");
        }
        private static void RequireDefinition(GroundDecorationDefinition definition)
        {
            Require(definition!=null&&definition.schemaVersion==1&&!string.IsNullOrWhiteSpace(definition.assetId)&&definition.sprite!=null,"装饰定义缺少版本、assetId或真实Sprite。");
            Require(EditorUtility.IsPersistent(definition)&&GroundWorkshopOwnership.IsUnder(AssetDatabase.GetAssetPath(definition),GroundWorkshopOwnership.Root)&&EditorUtility.IsPersistent(definition.sprite),"请选择已保存到工坊受控目录的装饰SO与真实Sprite。");
            Require(GroundWorkshopOwnership.IsFinite(definition.footpointNormalized.x)&&GroundWorkshopOwnership.IsFinite(definition.footpointNormalized.y)&&
                definition.footpointNormalized.x>=0&&definition.footpointNormalized.x<=1&&definition.footpointNormalized.y>=0&&definition.footpointNormalized.y<=1&&
                GroundWorkshopOwnership.IsFinite(definition.scaleRange.x)&&GroundWorkshopOwnership.IsFinite(definition.scaleRange.y)&&definition.scaleRange.x>0&&definition.scaleRange.y>=definition.scaleRange.x&&
                GroundWorkshopOwnership.IsFinite(definition.groundOffset)&&Enum.IsDefined(typeof(GroundDecorationMode),definition.mode),"装饰的脚点、缩放、朝向或离地偏移不合法。");
        }
        private static Vector3Int Cell(GroundWorkshopMap map,Vector3 world)
        {
            Vector3Int cell=map.surface.grid.WorldToCell(world);cell.z=0;return cell;
        }
        private static BoundsInt Clip(GroundWorkshopMap map,BoundsInt area)
        {
            Require(area.zMin==0&&area.size.z==1&&area.size.x>0&&area.size.y>0,"装饰选区须为正尺寸的逻辑XY范围，z=0且厚度1。");
            var bounds=map.surface.EditableCells;
            int x=Math.Max(area.xMin,bounds.xMin),y=Math.Max(area.yMin,bounds.yMin);
            return new BoundsInt(x,y,0,Math.Max(0,Math.Min(area.xMax,bounds.xMax)-x),Math.Max(0,Math.Min(area.yMax,bounds.yMax)-y),1);
        }
        private static GroundDecorationInstance[] Instances(GroundWorkshopMap map)=>map.decorationRoot.GetComponentsInChildren<GroundDecorationInstance>(true);
        internal static bool Owned(GroundWorkshopMap map,GroundDecorationInstance instance)
        {
            if(instance==null||instance.schemaVersion!=1||instance.transform.parent!=map.decorationRoot||instance.definition==null||
                instance.assetId!=instance.definition.assetId||!Guid.TryParseExact(instance.instanceId,"N",out _)||string.IsNullOrEmpty(instance.generationGroup)||
                !instance.generationGroup.StartsWith(Prefix(map),StringComparison.Ordinal)||instance.visual==null||instance.visual.parent!=instance.transform||
                instance.transform.childCount!=1||instance.visual.childCount!=0||PrefabUtility.IsPartOfPrefabInstance(instance.gameObject)||
                instance.GetComponents<Component>().Any(c=>!(c is Transform)&&!(c is GroundDecorationInstance))||
                instance.visual.GetComponents<Component>().Any(c=>!(c is Transform)&&!(c is SpriteRenderer)))return false;
            var renderer=instance.visual.GetComponent<SpriteRenderer>();
            return renderer!=null&&renderer.sprite==instance.definition.sprite;
        }
        private static HashSet<Vector3Int> RoadCells(GroundWorkshopMap map,BoundsInt area,int margin)
        {
            var result=new HashSet<Vector3Int>();
            var scan=new BoundsInt(area.xMin-margin,area.yMin-margin,0,area.size.x+margin*2,area.size.y+margin*2,1);
            for(int layer=0;layer<map.theme.layers.Length;layer++)
            {
                if(map.theme.layers[layer].purpose!=GroundLayerPurpose.Road)continue;
                var tilemap=map.surface.layers[layer].tilemap;
                foreach(var cell in scan.allPositionsWithin)
                    if(tilemap.HasTile(cell))for(int y=-margin;y<=margin;y++)for(int x=-margin;x<=margin;x++)result.Add(cell+new Vector3Int(x,y,0));
            }
            return result;
        }

        private static Material MaterialFor(GroundWorkshopMap map,Transaction tx)
        {
            tx.CheckGroup();string path=map.registration.assetFolder+"/Decoration.mat";
            Require(GroundWorkshopOwnership.IsUnder(path,map.registration.assetFolder),"装饰材质超出地图资产目录。");
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material!=null){Require(material.shader!=null&&material.shader.name==ShaderName,"已有装饰材质使用其他Shader，保留并停止。");return material;}
            Require(!File.Exists(Absolute(path))&&!File.Exists(Absolute(path)+".meta")&&AssetDatabase.LoadMainAssetAtPath(path)==null,"同名装饰材质或Meta已存在，不覆盖。");
            var shader=Shader.Find(ShaderName);Require(shader!=null,"缺少已编译的装饰Cutout Shader。");
            material=new Material(shader){name="Decoration"};AssetDatabase.CreateAsset(material,path);
            tx.assets.Add(new KeyValuePair<string,UnityEngine.Object>(path,material));EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }
        private static string Absolute(string path)=>Path.GetFullPath(Path.Combine(Application.dataPath,"..",path));
        private static GroundDecorationOrientation OrientationFor(GroundWorkshopMap map,Transaction tx)
        {
            tx.CheckGroup();var component=map.GetComponent<GroundDecorationOrientation>();
            if(component==null){component=Undo.AddComponent<GroundDecorationOrientation>(map.gameObject);tx.addedOrientation=component;component.map=map;}
            Require(component.map==null||component.map==map,"地图朝向组件指向了其他地图，保留并停止。");
            return component;
        }
        private static GroundDecorationInstance Spawn(GroundWorkshopMap map,GroundDecorationDefinition definition,Vector3 world,bool generated,int seed,string group,float scale,bool mirror,Material material,Transaction tx)
        {
            tx.CheckGroup();var root=new GameObject(string.IsNullOrEmpty(definition.displayName)?"工坊装饰":definition.displayName);tx.created.Add(root);
            root.transform.SetParent(map.decorationRoot,false);root.transform.position=world;
            // 地表逻辑根可以是(4,1,4)。装饰scaleRange表示Sprite的世界倍率，不能把草横向再放大四倍；
            // 即使Builder已抵消容器缩放，此处仍以真实祖先尺度计算，既支持新图也支持显式注册的旧根。
            Vector3 ancestorScale=map.decorationRoot.lossyScale;
            Require(Finite(ancestorScale)&&Mathf.Abs(ancestorScale.x)>.0001f&&Mathf.Abs(ancestorScale.y)>.0001f&&Mathf.Abs(ancestorScale.z)>.0001f,"装饰容器祖先缩放必须有限且非零。");
            root.transform.localScale=new Vector3(scale/Mathf.Abs(ancestorScale.x),scale/Mathf.Abs(ancestorScale.y),scale/Mathf.Abs(ancestorScale.z));
            var instance=root.AddComponent<GroundDecorationInstance>();instance.instanceId=Guid.NewGuid().ToString("N");instance.assetId=definition.assetId;
            instance.definition=definition;instance.generated=generated;instance.seed=seed;instance.generationGroup=group;
            var visual=new GameObject("VisualSprite");visual.transform.SetParent(root.transform,false);visual.transform.localScale=new Vector3(mirror?-1f:1f,1f,1f);instance.visual=visual.transform;
            var renderer=visual.AddComponent<SpriteRenderer>();renderer.sprite=definition.sprite;renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            Undo.RegisterCreatedObjectUndo(root,tx.label);tx.CheckGroup();return instance;
        }
        private static T Edit<T>(GroundWorkshopMap map,string label,Func<Transaction,T> body)
        {
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName(label);
            var tx=new Transaction(map,label,group);
            try
            {
                T result=body(tx);tx.CheckGroup();Undo.FlushUndoRecordObjects();tx.CheckGroup();
                EditorSceneManager.MarkSceneDirty(map.gameObject.scene);Undo.CollapseUndoOperations(group);Undo.IncrementCurrentGroup();return result;
            }
            catch(Exception exception)
            {
                string recovery=tx.Rollback();throw new InvalidOperationException(label+"失败；"+recovery,exception);
            }
        }

        private struct Placement{public GroundDecorationDefinition definition;public Vector3 world;public float scale;public bool mirror;}
        private struct StableRandom
        {
            private uint state;
            public StableRandom(uint seed){state=seed==0?0x6D2B79F5u:seed;}
            private uint Next(){uint x=state;x^=x<<13;x^=x>>17;x^=x<<5;state=x;return x;}
            public float Unit()=>(Next()&0x00ffffffu)/16777216f;
            public int Index(int length)=>(int)(Next()%(uint)length);
        }
        private static uint ConfigurationSeed(GroundWorkshopMap map,GroundDecorationDefinition[] variants,BoundsInt area,int seed,float density,float spacing,bool avoidRoads)
        {
            uint hash=2166136261u;
            Action<string> add=text=>{foreach(char character in text??""){hash^=character;hash*=16777619u;}hash^=0xffu;hash*=16777619u;};
            add(seed.ToString(System.Globalization.CultureInfo.InvariantCulture));add(map.registration.mapId);add(map.schemaVersion.ToString());add(map.theme.assetId);add(map.theme.revision);
            add(area.xMin+","+area.yMin+","+area.size.x+","+area.size.y);add(density.ToString("R",System.Globalization.CultureInfo.InvariantCulture));add(spacing.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
            add(avoidRoads.ToString());add(map.theme.roadExclusionCells.ToString());
            foreach(var definition in variants){add(definition.assetId);add(definition.revision);add(((int)definition.mode).ToString());add(definition.scaleRange.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture));add(definition.scaleRange.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture));add(definition.allowMirror.ToString());}
            return hash;
        }
        private sealed class SpacingGrid
        {
            private readonly float spacing,squared;
            private readonly Dictionary<Vector2Int,List<Vector3>> buckets=new Dictionary<Vector2Int,List<Vector3>>();
            public SpacingGrid(float value){spacing=value;squared=value*value;}
            private Vector2Int Key(Vector3 p)=>new Vector2Int(Mathf.FloorToInt(p.x/spacing),Mathf.FloorToInt(p.z/spacing));
            public void Add(Vector3 p){if(!Finite(p))return;var key=Key(p);if(!buckets.TryGetValue(key,out var list)){list=new List<Vector3>();buckets.Add(key,list);}list.Add(p);}
            public bool CanAdd(Vector3 p)
            {
                var key=Key(p);
                for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)if(buckets.TryGetValue(key+new Vector2Int(x,y),out var list))
                    foreach(var old in list){float dx=p.x-old.x,dz=p.z-old.z;if(dx*dx+dz*dz<squared)return false;}
                return true;
            }
        }

        private sealed class Transaction
        {
            public readonly string label;
            public readonly List<GameObject> created=new List<GameObject>();
            public readonly List<KeyValuePair<string,UnityEngine.Object>> assets=new List<KeyValuePair<string,UnityEngine.Object>>();
            public readonly List<KeyValuePair<GroundDecorationInstance,bool>> locks=new List<KeyValuePair<GroundDecorationInstance,bool>>();
            public GroundDecorationOrientation addedOrientation;
            private readonly GroundWorkshopMap map;private readonly int group;
            private readonly List<Removed> removed=new List<Removed>();
            public Transaction(GroundWorkshopMap owner,string name,int id){map=owner;label=name;group=id;}
            public void CheckGroup(){Require(Undo.GetCurrentGroup()==group,"检测到外部Undo组变化，停止本次装饰事务，不合并外部操作。");}
            public void Remove(GroundDecorationInstance instance){CheckGroup();removed.Add(new Removed(instance));Undo.DestroyObjectImmediate(instance.gameObject);CheckGroup();}
            public string Rollback()
            {
                bool foreign=Undo.GetCurrentGroup()!=group;
                if(!foreign){Undo.RevertAllDownToGroup(group);Undo.IncrementCurrentGroup();}
                // 外部组出现时绝不跨组Revert。只按精确对象账本撤销自己的创建/锁与删除快照。
                foreach(var root in created)if(root!=null)
                {
                    if(foreign)
                    {
                        var marker=root.GetComponent<GroundDecorationInstance>();
                        // 不把恰好被外部回调挂到本次新根下的对象连带删除。
                        foreach(Transform child in root.transform.Cast<Transform>().ToArray())
                            if(marker==null||child!=marker.visual)child.SetParent(map.decorationRoot,true);
                        if(root.GetComponents<Component>().Any(c=>!(c is Transform)&&!(c is GroundDecorationInstance)))
                        {Debug.LogWarning("外部代码已复用本次新装饰根，保留该根以保护外部组件："+root.name);continue;}
                    }
                    UnityEngine.Object.DestroyImmediate(root);
                }
                if(foreign)
                {
                    foreach(var snapshot in removed)snapshot.Restore();
                    foreach(var item in locks)if(item.Key!=null)item.Key.isLocked=item.Value;
                }
                if(!foreign)
                    foreach(var item in assets)if(item.Value!=null&&AssetDatabase.LoadMainAssetAtPath(item.Key)==item.Value)AssetDatabase.DeleteAsset(item.Key);
                var orientation=map==null?null:map.GetComponent<GroundDecorationOrientation>();if(orientation!=null)orientation.Refresh();
                if(foreign)Debug.LogWarning("工坊装饰检测到外部Undo组：已仅恢复本次作者对象，不跨组撤销；本次新建的地图材质/表现组件保留，避免破坏外部引用。");
                return foreign?"已恢复本次作者对象，外部Undo保持；新建地图共享资源保留。":"已撤销本次独立Undo组与新建资产。";
            }
        }

        // 只对结构已验证的工具根建立失败恢复快照，不克隆外部脚本、碰撞、Prefab或其他子物件。
        private sealed class Removed
        {
            private readonly GroundDecorationInstance original;
            private readonly Transform parent;private readonly string name,visualName,id,asset,group;private readonly int sibling,seed,schema,layer,visualLayer;
            private readonly bool active,visualActive,locked,generated;private readonly Vector3 position,scale,visualPosition,visualScale;
            private readonly Quaternion rotation,visualRotation;private readonly GroundDecorationDefinition definition;
            private readonly Sprite sprite;private readonly Material material;private readonly Color color;private readonly bool flipX,flipY;private readonly int sortingLayer,sortingOrder;
            public Removed(GroundDecorationInstance instance)
            {
                original=instance;var root=instance.transform;var visual=instance.visual;var renderer=visual.GetComponent<SpriteRenderer>();
                parent=root.parent;name=root.name;visualName=visual.name;sibling=root.GetSiblingIndex();position=root.localPosition;rotation=root.localRotation;scale=root.localScale;
                visualPosition=visual.localPosition;visualRotation=visual.localRotation;visualScale=visual.localScale;active=root.gameObject.activeSelf;visualActive=visual.gameObject.activeSelf;
                id=instance.instanceId;asset=instance.assetId;group=instance.generationGroup;seed=instance.seed;schema=instance.schemaVersion;locked=instance.isLocked;generated=instance.generated;definition=instance.definition;
                layer=root.gameObject.layer;visualLayer=visual.gameObject.layer;sprite=renderer.sprite;material=renderer.sharedMaterial;color=renderer.color;flipX=renderer.flipX;flipY=renderer.flipY;sortingLayer=renderer.sortingLayerID;sortingOrder=renderer.sortingOrder;
            }
            public void Restore()
            {
                if(original!=null||parent==null)return;
                var root=new GameObject(name);root.layer=layer;root.transform.SetParent(parent,false);root.transform.localPosition=position;root.transform.localRotation=rotation;root.transform.localScale=scale;root.transform.SetSiblingIndex(sibling);
                var instance=root.AddComponent<GroundDecorationInstance>();instance.schemaVersion=schema;instance.instanceId=id;instance.assetId=asset;instance.generationGroup=group;instance.seed=seed;instance.isLocked=locked;instance.generated=generated;instance.definition=definition;
                var visual=new GameObject(visualName);visual.layer=visualLayer;visual.transform.SetParent(root.transform,false);visual.transform.localPosition=visualPosition;visual.transform.localRotation=visualRotation;visual.transform.localScale=visualScale;instance.visual=visual.transform;
                var renderer=visual.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sharedMaterial=material;renderer.color=color;renderer.flipX=flipX;renderer.flipY=flipY;renderer.sortingLayerID=sortingLayer;renderer.sortingOrder=sortingOrder;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                visual.SetActive(visualActive);root.SetActive(active);
            }
        }
    }
}
#endif
