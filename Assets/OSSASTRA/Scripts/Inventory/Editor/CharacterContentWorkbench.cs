/* 脚本：CharacterContentWorkbench
 * 功能：检查真实作者定义、导出候选依赖/模型目录，观察具名预览角色与功能挂点。
 * 边界：只写自有候选报告；不修改骨架或SO。使用预览明确消费当前B测试会话的真实物品。
 */
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Spine.Unity;
using Ossastra.Characters;
using Ossastra.Inventory;
using ItemDefinition = Ossastra.Characters.ItemDefinitionSO;
namespace Ossastra.Authoring
{
    using CharacterRoot = Ossastra.Characters.CharacterRoot;
    public sealed class CharacterContentWorkbench : EditorWindow
    {
        public const string ScenePath="Assets/OSSASTRA/Scenes/SC_VC02B_IntegrationReady.unity";
        public const string ReportPath="docs/ossastra/ArtIntegrationCandidate/Generated";
        private UnityEngine.Object target;
        private CharacterRoot preview;
        private string itemId="",report="选择一个角色、动作、能力、装备、物品或Rig定义。";
        private Vector2 scroll;
        private bool showSockets=true;
        [MenuItem("XingHai/OSSASTRA/Character Content Workbench")]
        public static void Open()=>GetWindow<CharacterContentWorkbench>("角色内容检查");
        private void OnEnable(){SceneView.duringSceneGui+=DrawSockets;}
        private void OnDisable(){SceneView.duringSceneGui-=DrawSockets;}
        private void OnGUI()
        {
            target=EditorGUILayout.ObjectField("作者定义",target,typeof(ScriptableObject),false);
            if(GUILayout.Button("使用当前选中并检查")){target=Selection.activeObject;report=Inspect(target);}
            if(GUILayout.Button("检查当前定义"))report=Inspect(target);
            if(target!=null)
            {
                foreach(var reference in References(target).Distinct())
                    if(GUILayout.Button(reference.GetType().Name+" · "+reference.name)){Selection.activeObject=reference;EditorGUIUtility.PingObject(reference);}
            }
            if(GUILayout.Button("导出B候选模型、SO图谱与动作合同（只读）"))
            {try{report=ExportCandidate();}catch(Exception error){report=error.ToString();}}
            EditorGUILayout.Space();
            preview=(CharacterRoot)EditorGUILayout.ObjectField("具名预览本体",preview,typeof(CharacterRoot),true);
            showSockets=EditorGUILayout.Toggle("Scene显示实际挂点",showSockets);
            if(preview!=null&&Application.isPlaying)
            {
                EditorGUILayout.LabelField("执行",preview.DataBus.ActionId.ToString());
                var view=preview.GetComponent<CharacterEquipmentSpineView>();
                if(view!=null)EditorGUILayout.LabelField("身体/装备",view.LastResult);
            }
            itemId=EditorGUILayout.TextField("测试库存实例ID",itemId);
            EditorGUILayout.HelpBox("预览使用只对B候选具名活体开放；调用实际消费者，会消耗该测试会话物品，不会授予或修改资产。",MessageType.Info);
            if(GUILayout.Button("在具名本体上使用该真实物品"))
            {
                var session=FindObjectOfType<InventorySession>();
                if(!Application.isPlaying||preview==null||preview.gameObject.scene.path!=ScenePath||session==null||session.Coordinator.Current!=preview)
                    report="拒绝：必须在B候选Play中显式选择当前实控本体。";
                else{var use=session.GetComponent<InventoryItemUse>();if(use==null)report="缺少InventoryItemUse";else{use.TryUse(itemId);report=use.LastResult;}}
            }
            scroll=EditorGUILayout.BeginScrollView(scroll);EditorGUILayout.TextArea(report,GUILayout.ExpandHeight(true));EditorGUILayout.EndScrollView();
        }
        private void DrawSockets(SceneView scene)
        {
            if(!showSockets||preview==null||!Application.isPlaying)return;
            var body=System.Linq.Enumerable.FirstOrDefault(System.Linq.Enumerable.OfType<ICharacterAnimationStateView>(preview.GetComponentsInChildren<MonoBehaviour>()));
            var skeleton=body==null?null:body.Skeleton;var rig=preview.Definition?.rigProfile;
            if(skeleton?.Skeleton==null||rig==null)return;
            foreach(var binding in rig.sockets)
            {
                try
                {
                    var resolved=rig.Resolve(binding.semanticSocketId,skeleton.Skeleton.Data);
                    var bone=skeleton.Skeleton.FindBone(resolved.BoneName);
                    var position=skeleton.transform.TransformPoint(resolved.Position(bone.AppliedPose));
                    Handles.color=Color.cyan;Handles.DrawLine(position,position+skeleton.transform.right*.2f);
                    Handles.color=Color.green;Handles.DrawLine(position,position+skeleton.transform.up*.2f);
                    Handles.Label(position,binding.semanticSocketId+" → "+resolved.BoneName);
                }
                catch(ArgumentException){/* 检查按钮给出完整字段错误；绘制不改变游戏或刷控制台。 */}
            }
        }
        public static string Inspect(UnityEngine.Object definition)
        {
            if(definition==null)return "未选中定义";
            try
            {
                if(definition is AbilityDefinitionSO ability)ability.Compile();
                else if(definition is AbilityLoadoutSO loadout)loadout.Validate();
                else if(definition is ActionDefinitionSO action)action.Freeze();
                else if(definition is ItemDefinition item)
                {
                    item.FreezeInventory();
                    if(item.useKind!=InventoryUseKind.None&&item.ability==null)throw new ArgumentException("ability required by useKind");
                    if(item.ability!=null)item.ability.Compile();
                }
                else if(definition is CharacterRigProfileSO rig)
                {foreach(var binding in rig.sockets)rig.Resolve(binding.semanticSocketId,rig.model==null?null:rig.model.GetSkeletonData(false));}
                else if(definition is CharacterDefinitionSO character)
                {
                    // 正式动画由场景/Prefab的ConfigManager持有，不要求作者额外维护隐藏的旧Profile。
                    foreach(var basic in character.basicAttacks){if(basic==null)throw new ArgumentException("basicAttacks null");basic.Freeze();}
                    var characterRig=character.rigProfile;if(characterRig!=null)foreach(var binding in characterRig.sockets)characterRig.Resolve(binding.semanticSocketId,characterRig.model.GetSkeletonData(false));
                }
                else if(definition is EquipmentDefinitionSO equipment)
                {
                    if(string.IsNullOrWhiteSpace(equipment.definitionId))throw new ArgumentException("definitionId missing");
                    if(equipment.skillLoadout!=null)equipment.skillLoadout.Validate();
                    foreach(var binding in equipment.abilityBindings??Array.Empty<AbilityBinding>())
                    {if(binding?.ability==null)throw new ArgumentException("abilityBindings missing ability");binding.ability.Compile();if(binding.longPressAbility!=null)binding.longPressAbility.Compile();}
                    foreach(var main in equipment.mainActionProfiles??Array.Empty<MainActionProfileSO>())
                    {if(main==null)throw new ArgumentException("mainActionProfiles null");main.Freeze();}
                }
                else if(definition is MainActionProfileSO mainProfile)mainProfile.Freeze();
                else if(definition is Ossastra.CharacterAnimation.AnimationPlanMappingSO mapping)
                {
                    if(mapping.skeletonDataAsset==null)throw new ArgumentException("Skeleton Data missing");
                    var skeleton=mapping.skeletonDataAsset.GetSkeletonData(false);
                    if(skeleton==null)throw new ArgumentException("Skeleton Data invalid");
                    foreach(Ossastra.CharacterAnimation.EAnimationLogicState logic in Enum.GetValues(typeof(Ossastra.CharacterAnimation.EAnimationLogicState)))
                    {
                        var plan=mapping.GetDefault(logic);if(plan==null)continue;
                        if(plan.clearTracks.Any(track=>track<0))throw new ArgumentException("negative clear track: "+logic);
                        foreach(var clip in plan.clips.Concat(plan.queuedClips.Where(q=>q!=null).Select(q=>q.clip)))
                            if(clip==null||clip.track<0||string.IsNullOrWhiteSpace(clip.animaName)||skeleton.FindAnimation(clip.animaName)==null||
                                clip.playbackRate<=0||float.IsNaN(clip.playbackRate)||float.IsInfinity(clip.playbackRate)||clip.mixDuration<0||float.IsNaN(clip.mixDuration)||float.IsInfinity(clip.mixDuration))
                                throw new ArgumentException("invalid mapped clip: "+logic+" / "+clip?.animaName);
                    }
                }
                else if(definition is CharacterAnimationProfileSO animationProfile)
                {
                    if(animationProfile.bindings==null||animationProfile.bindings.Any(b=>b==null||string.IsNullOrWhiteSpace(b.key)||string.IsNullOrWhiteSpace(b.clip))||animationProfile.bindings.Select(b=>b.key).Distinct().Count()!=animationProfile.bindings.Length)
                        throw new ArgumentException("bindings null/missing/duplicate key");
                }
                else if(definition is EquipmentSpinePresentationSO presentation)
                {
                    var rigs=AssetDatabase.FindAssets("t:CharacterRigProfileSO",new[]{"Assets/OSSASTRA/VC02B"})
                        .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<CharacterRigProfileSO>).ToArray();
                    if(rigs.Length!=1)throw new ArgumentException("请选择唯一B角色Rig集合；当前数量="+rigs.Length);
                    presentation.Freeze(rigs[0].model.GetSkeletonData(false),rigs[0]);
                }
                else return "INFO · 仅导出字段/引用，无此类型的独立验证器 · "+AssetDatabase.GetAssetPath(definition)+"\n"+string.Join("\n",Fields(definition));
                return "PASS · "+AssetDatabase.GetAssetPath(definition)+"\n"+string.Join("\n",Fields(definition));
            }
            catch(Exception error){return "REJECT · "+AssetDatabase.GetAssetPath(definition)+"\n"+error.Message;}
        }
        private static IEnumerable<UnityEngine.Object> References(UnityEngine.Object asset)
        {
            var serialized=new SerializedObject(asset);var property=serialized.GetIterator();
            while(property.Next(true))if(property.propertyType==SerializedPropertyType.ObjectReference&&property.objectReferenceValue!=null&&property.propertyPath!="m_Script")yield return property.objectReferenceValue;
        }
        private static IEnumerable<string> Fields(UnityEngine.Object asset)
        {
            var serialized=new SerializedObject(asset);var property=serialized.GetIterator();
            while(property.Next(true))
            {
                string value;
                switch(property.propertyType)
                {
                    case SerializedPropertyType.ObjectReference:value=AssetDatabase.GetAssetPath(property.objectReferenceValue);break;
                    case SerializedPropertyType.String:value=property.stringValue;break;
                    case SerializedPropertyType.Integer:value=property.longValue.ToString();break;
                    case SerializedPropertyType.Float:value=property.doubleValue.ToString("R",System.Globalization.CultureInfo.InvariantCulture);break;
                    case SerializedPropertyType.Boolean:value=property.boolValue.ToString();break;
                    case SerializedPropertyType.Enum:value=property.intValue.ToString();break;
                    default:value=property.propertyType.ToString();break;
                }
                yield return property.propertyPath+"\t"+Clean(value);
            }
        }
        private static string Clean(string value)=>(value??"").Replace("\t"," ").Replace("\r"," ").Replace("\n"," / ");
        private static string Hash(string path)
        {if(!File.Exists(path))return "local-nonfile";using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        public static string ExportCandidate()
        {
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)==null)throw new InvalidOperationException("missing "+ScenePath);
            Directory.CreateDirectory(ReportPath);
            var paths=AssetDatabase.GetDependencies(ScenePath,true).Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            var assets=paths.SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<ScriptableObject>().Where(a=>a.GetType().Namespace?.StartsWith("Ossastra",StringComparison.Ordinal)==true).Distinct().ToArray();
            var graph=new List<string>{"asset\tguid\ttype\tfield\tvalue"};var checks=new List<string>{"asset\tresult"};
            foreach(var asset in assets)
            {
                var path=AssetDatabase.GetAssetPath(asset);var prefix=path+"\t"+AssetDatabase.AssetPathToGUID(path)+"\t"+asset.GetType().FullName+"\t";
                graph.AddRange(Fields(asset).Select(f=>prefix+f));checks.Add(path+"\t"+Clean(Inspect(asset)));
            }
            File.WriteAllLines(ReportPath+"/SO-fields.tsv",graph);File.WriteAllLines(ReportPath+"/definition-checks.tsv",checks);
            File.WriteAllLines(ReportPath+"/asset-manifest.tsv",new[]{"path\tguid\tsha256"}.Concat(paths.Select(p=>p+"\t"+AssetDatabase.AssetPathToGUID(p)+"\t"+Hash(p))));
            var models=paths.Select(AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>).Where(a=>a!=null).ToArray();
            var rows=new List<string>{"model\tguid\tversion\tkind\tname\tparentOrOwner\tdetail\tcurrentConsumer"};
            var rigs=assets.OfType<CharacterRigProfileSO>().ToArray();
            foreach(var model in models)
            {
                var data=model.GetSkeletonData(false);if(data==null)throw new InvalidOperationException("cannot load "+AssetDatabase.GetAssetPath(model));
                var prefix=AssetDatabase.GetAssetPath(model)+"\t"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(model))+"\t"+data.Version+"\t";
                string Consumer(string name,bool slot)=>string.Join(";",rigs.Where(r=>r.model==model).SelectMany(r=>r.sockets).Where(b=>slot?b.slotName==name:b.boneName==name||(b.kind!=RigSocketKind.Bone&&data.FindSlot(b.slotName)?.BoneData.Name==name)).Select(b=>b.semanticSocketId).DefaultIfEmpty("当前无程序挂点消费者；骨架动画/美术用途，语义未冻结"));
                foreach(var bone in data.Bones)rows.Add(prefix+"bone\t"+bone.Name+"\t"+bone.Parent?.Name+"\tindex="+bone.Index+"\t"+Consumer(bone.Name,false));
                foreach(var slot in data.Slots)rows.Add(prefix+"slot\t"+slot.Name+"\t"+slot.BoneData.Name+"\tindex="+slot.Index+"\t"+Consumer(slot.Name,true));
                foreach(var skin in data.Skins)
                {
                    rows.Add(prefix+"skin\t"+skin.Name+"\t\t\t骨架附件选择；不猜方向语义");
                    foreach(var entry in skin.Attachments)rows.Add(prefix+(entry.Attachment is Spine.PointAttachment?"point":"attachment")+"\t"+entry.Placeholder+"\t"+skin.Name+" / "+data.Slots.Items[entry.SlotIndex].Name+"\t"+entry.Attachment.GetType().Name+"\t骨架美术；挂点用途仅以显式Rig为准");
                }
                foreach(var constraint in data.Constraints)rows.Add(prefix+"constraint\t"+constraint.Name+"\t\t"+constraint.GetType().Name+"\tSpine动画求解，非玩法权威");
                foreach(var animation in data.Animations)
                {
                    rows.Add(prefix+"animation\t"+animation.Name+"\t\tduration="+animation.Duration+"\t具体消费见SO-fields动画映射；未映射不猜用途");
                    foreach(var timeline in animation.Timelines.OfType<Spine.EventTimeline>())foreach(var fact in timeline.Events)
                        rows.Add(prefix+"animationEvent\t"+fact.Data.Name+"\t"+animation.Name+"\tseconds="+fact.Time+"\t原生事件；动作派生时间线见SO，不重复结算");
                }
                foreach(var fact in data.Events)rows.Add(prefix+"eventDefinition\t"+fact.Name+"\t\t\t原生事件目录");
            }
            File.WriteAllLines(ReportPath+"/model-inventory.tsv",rows);
            var actions=new List<string>{"asset\tid\tanimationLogicId\trelease\tmoveReady\tattackReady\tfinish\teventBindings"};
            foreach(var action in assets.OfType<ActionDefinitionSO>())
            {action.Freeze();actions.Add(AssetDatabase.GetAssetPath(action)+"\t"+action.definitionId+"\t"+action.animationLogicId+"\t"+action.releaseAt+"\t"+action.moveReadyAt+"\t"+action.attackReadyAt+"\t"+action.finishAt+"\t"+string.Join(";",action.eventTimeline.Select(e=>e.marker+":"+e.sourceEvent+"@"+e.seconds)));}
            File.WriteAllLines(ReportPath+"/action-contracts.tsv",actions);
            return "导出完成："+assets.Length+" SO / "+models.Length+"实际依赖模型 / "+(rows.Count-1)+"模型条目。\n"+ReportPath+"\nUnity "+Application.unityVersion+"；工具导出schema=2。REJECT项须单独处理，不以导出成功冒充检查通过。";
        }
    }
}
