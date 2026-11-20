// 在攻击调试台核对 Spine 原时间轴。正式资产只在显式写回时修改。
using System;
using System.Linq;
using System.Collections.Generic;
using Ossastra.CharacterAnimation;
using Ossastra.Characters;
using Spine;
using UnityEditor;
using UnityEngine;
using ActorRoot = Ossastra.Characters.CharacterRoot;
using Object = UnityEngine.Object;

namespace Ossastra.Development.Editor
{
    internal sealed class AttackLabAnimationCalibration : IDisposable
    {
        private ActorRoot actor;
        private AnimationCalibrationSO draft, baseline;
        private string snapshot, behavior, clipName, message;
        private int track, eventIndex;
        private Vector2 scroll;
        private AudioSource preview;
        private static readonly string[] MeaningOptions = {"选择触发用途","出手释放","结束并开放覆盖","允许冲刺打断","脚步"};
        private string pendingEvent;
        private int pendingMeaning;
        private bool definitions, presets;
        private bool Dirty => draft != null && snapshot != EditorJsonUtility.ToJson(draft);

        public void Draw(AttackLabSession session)
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            try { DrawContents(session); }
            finally { EditorGUILayout.EndScrollView(); }
            if (UnityEngine.Event.current.type != EventType.Layout && UnityEngine.Event.current.type != EventType.Repaint && Dirty)
                SaveIfChanged();
        }

        private void DrawContents(AttackLabSession session)
        {
            var actors = Object.FindObjectsOfType<ActorRoot>().Where(a => a.Definition != null &&
                a.GetComponentInChildren<Spine.Unity.SkeletonAnimation>(true) != null).OrderBy(a => a.name).ToArray();
            if (actors.Length == 0) { EditorGUILayout.HelpBox("打开含正式横版角色的场景，或到实战验证进入独立测试场。",MessageType.Info);return; }
            if (actor == null || draft == null) Bind(session?.Actor ?? actors[0]);
            int selected = Array.IndexOf(actors,actor);
            int next = EditorGUILayout.Popup("角色",Mathf.Max(0,selected),actors.Select(a=>a.name).ToArray());
            if (next != selected && (!Dirty || EditorUtility.DisplayDialog("切换角色","放弃此角色未保存的校准候选？","切换","保留"))) Bind(actors[next]);
            if (draft == null || draft.skeleton == null) return;
            if (!Dirty && SourceChanged()) Reload();
            EditorGUILayout.HelpBox(message ?? "自动保存 · 下次播放采用新设置。",
                message == null ? MessageType.None : MessageType.Warning);
            if (message != null && GUILayout.Button("撤销未保存的更改"))
            {
                Reload();
                return;
            }
            var data = draft.skeleton.GetSkeletonData(true);
            if(data == null || data.Animations.Count == 0) { EditorGUILayout.HelpBox("骨架尚无可读取的动画。",MessageType.Warning);return; }
            EditorGUILayout.LabelField("实际骨架", draft.skeleton.name);
            if (session != null && session.Actor != null)
                EditorGUILayout.LabelField("受控角色 / 武器", session.Actor.name + " / " + session.WeaponLabel);
            var clips=data.Animations.Select(a=>a.Name).ToArray();
            var routed=actor.GetComponent<CharacterMainAction>()?.Choices ?? Array.Empty<MainActionProfileSO>();
            var skills=actor.GetComponent<CharacterAbilities>()?.SkillChoices ?? Array.Empty<AbilityDefinitionSO>();
            var actions=(actor.Definition.basicAttacks ?? Array.Empty<ActionDefinitionSO>()).Concat(routed.Where(p=>p!=null).SelectMany(p=>p.stages).Where(s=>s!=null).Select(s=>s.action))
                .Where(a=>a!=null).Select(a=>a.definitionId).Concat(Enumerable.Range(1,4).Select(i=>"Attack_Normal_"+i))
                .Concat(skills.Where(a=>a!=null && a.action!=null).Select(a=>a.action.definitionId)).Concat(clips).Distinct().ToArray();
            string previousBehavior=behavior;
            behavior=Choose("行为标识",behavior,actions);
            behavior=EditorGUILayout.DelayedTextField("行为绑定",behavior);
            if (previousBehavior != behavior)
            {
                var bound = (draft.bindings ?? Array.Empty<AnimationCalibrationBinding>()).FirstOrDefault(x => x != null && x.behavior == behavior);
                if (bound != null)
                {
                    clipName = bound.animation;
                    track = bound.track;
                    var sourceRows = AnimationCalibrationSO.Events(data.FindAnimation(clipName)).ToArray();
                    eventIndex = Mathf.Max(0, Array.FindIndex(sourceRows, e => e.Data.Name == bound.eventName));
                }
                else if (clips.Contains(behavior)) { clipName = behavior; eventIndex = 0; }
            }
            var name=Choose("实际动画",clipName,clips);
            if(name!=clipName){clipName=name;eventIndex=0;}
            track=Mathf.Max(0,EditorGUILayout.IntField("轨道",track));
            var live=actor.GetComponentInChildren<Spine.Unity.SkeletonAnimation>(true)?.AnimationState?.GetTrack(track);
            if(Application.isPlaying && live?.Animation!=null && GUILayout.Button($"查看正在播放 · 轨 {track} · {live.Animation.Name}"))
            {clipName=live.Animation.Name;eventIndex=0;}
            draft.framesPerSecond=Mathf.Clamp(EditorGUILayout.DelayedIntField("制作帧率",draft.framesPerSecond),1,240);
            var animation=data.FindAnimation(clipName);
            if (animation == null) { EditorGUILayout.HelpBox("骨架中已找不到所选片段。请从实际动画列表重新选择。", MessageType.Warning);return; }
            var events=AnimationCalibrationSO.Events(animation).ToArray();
            EditorGUILayout.LabelField($"{animation.Duration:0.###} 秒   ·   {events.Length} 个时间轴事件");
            definitions=EditorGUILayout.Foldout(definitions,"骨架声明的全部事件");
            if(definitions)EditorGUILayout.LabelField(string.Join("、",data.Events.Select(e=>e.Name)),EditorStyles.wordWrappedLabel);
            var entries=draft.bindings ?? Array.Empty<AnimationCalibrationBinding>();
            var rows=entries.Where(b=>b!=null && b.behavior==behavior && b.track==track && b.animation==clipName).ToArray();
            EditorGUILayout.LabelField($"此行为在该动画的绑定   {rows.Length}",EditorStyles.boldLabel);
            DrawTiming(session, animation, events, rows);
            foreach(var b in rows)
            {
                bool changed = !draft.SourceMatches(b, animation);
                EditorGUILayout.LabelField($"{b.eventName} #{b.occurrence+1}   {b.sourceTime*draft.framesPerSecond:0.##} 帧   →   {MeaningLabel(b.semantic)}");
                if(changed)
                {
                    EditorGUILayout.HelpBox("源事件已变化。请在下方选中新的实际事件，核对后重新绑定。",MessageType.Warning);
                    if(events.Length>0)
                    {
                        int i=Mathf.Clamp(eventIndex,0,events.Length-1);
                        EditorGUILayout.LabelField($"原标记 {b.sourceTime*draft.framesPerSecond:0.##} 帧 → 所选 {events[i].Data.Name} {events[i].Time*draft.framesPerSecond:0.##} 帧");
                        if(GUILayout.Button("改绑并保留有效帧"))
                        {float previous=b.sourceTime*draft.framesPerSecond+b.offsetFrames;Capture(b,events,i);b.offsetFrames=Mathf.RoundToInt(previous-b.sourceTime*draft.framesPerSecond);}
                        if(GUILayout.Button("跟随新源并保留原偏移"))Capture(b,events,i);
                        if(GUILayout.Button("采用新源，清零标记偏移")){Capture(b,events,i);b.offsetFrames=0;}
                    }
                }
                if(GUILayout.Button("移除此绑定")){draft.bindings=entries.Where(item=>item!=b).ToArray();return;}
            }
            if(events.Length>0)
            {
                eventIndex=EditorGUILayout.Popup("时间轴实录",Mathf.Clamp(eventIndex,0,events.Length-1),events.Select((e,i)=>$"{i+1}.  {e.Time*draft.framesPerSecond:0.##} 帧   {e.Data.Name}").ToArray());
                var e=events[eventIndex];int ordinal=events.Take(eventIndex).Count(x=>x.Data.Name==e.Data.Name);
                var b=rows.FirstOrDefault(x=>x.eventName==e.Data.Name && x.occurrence==ordinal);
                EditorGUILayout.LabelField($"源时间 {e.Time*1000:0.#} ms    参数 {e.Int} / {e.Float:0.###} / {e.String}",EditorStyles.wordWrappedMiniLabel);
                if(b==null)
                {
                    string key = behavior + "/" + clipName + "/" + track + "/" + eventIndex;
                    var standard = StandardMeaning(e.Data.Name);
                    if (pendingEvent != key)
                    {
                        pendingEvent = key;
                        pendingMeaning = 0;
                    }
                    if (standard.HasValue)
                        EditorGUILayout.LabelField("触发", MeaningLabel(standard.Value));
                    else
                        pendingMeaning = EditorGUILayout.Popup("触发用途", pendingMeaning, MeaningOptions);
                    var meaning = standard ?? (pendingMeaning > 0 ? (AnimationEventMeaning?)(pendingMeaning - 1) : null);
                    using (new EditorGUI.DisabledScope(!meaning.HasValue))
                    {
                        if(GUILayout.Button("为此事件添加绑定"))
                        {
                            b=new AnimationCalibrationBinding{animation=clipName,behavior=behavior,track=track,
                                semantic=meaning.Value};
                            Capture(b,events,eventIndex); draft.bindings=entries.Append(b).ToArray();
                        }
                    }
                }
                else DrawBinding(b,data);
            }
            EditorGUILayout.Space();
            presets = EditorGUILayout.Foldout(presets, "校准预设", true);
            if (presets)
            {
                if (GUILayout.Button("另存当前校准为预设"))
                {
                    string path = EditorUtility.SaveFilePanelInProject("另存校准预设", actor.name + "_校准实验",
                        "asset", "选择项目路径");
                    if (path.Length > 0 && AssetDatabase.LoadMainAssetAtPath(path) == null)
                        AssetDatabase.CreateAsset(Object.Instantiate(draft), path);
                }
                var preset = (AnimationCalibrationSO)EditorGUILayout.ObjectField("导入到当前校准", null,
                    typeof(AnimationCalibrationSO), false);
                if (preset != null && preset.MatchesSkeleton(draft.skeleton) &&
                    EditorUtility.DisplayDialog("导入校准预设", "用此预设替换当前角色的校准？", "导入", "取消"))
                {
                    var currentSkeleton = draft.skeleton;
                    EditorUtility.CopySerialized(preset, draft);
                    draft.skeleton = currentSkeleton;
                }
            }
        }

        private void DrawTiming(AttackLabSession session, Spine.Animation clip, Spine.Event[] events, AnimationCalibrationBinding[] rows)
        {
            var records = session != null && session.Actor == actor && session.Trace != null
                ? session.Trace.Events.Where(e => e.trial == session.Generation && e.animationTrack == track && e.clip == clip.Name && e.markerSequence != 0).ToArray()
                : Array.Empty<AttackLabEvent>();
            EditorGUILayout.LabelField("时间轴（制作帧）", $"0 — {clip.Duration * draft.framesPerSecond:0.#}");
            void Lane(string title, IEnumerable<float> times, Color color)
            {
                Rect row = EditorGUILayout.GetControlRect(false, 17);
                EditorGUI.LabelField(new Rect(row.x,row.y,70,row.height),title,EditorStyles.miniLabel);
                row.xMin += 72;
                EditorGUI.DrawRect(row,new Color(.13f,.13f,.13f));
                foreach(float time in times)
                {
                    float pos = Mathf.Clamp01(time / Mathf.Max(.001f,clip.Duration));
                    EditorGUI.DrawRect(new Rect(row.x + (row.width - 3) * pos,row.y + 2,3,row.height - 4),color);
                }
            }
            Lane("源事件", events.Select(e=>e.Time), new Color(.54f,.78f,.9f));
            Lane("校准标记", rows.Select(b=>b.sourceTime+(float)b.offsetFrames/draft.framesPerSecond),new Color(.84f,.72f,.39f));
            Lane("本次实录", records.Select(e=>Mathf.Max(0,e.collectedTrackTime - e.markerCycle * clip.Duration)),new Color(.54f,.84f,.6f));
            var last=records.LastOrDefault();
            if(last!=null)EditorGUILayout.LabelField($"最近实录 {last.label} · 目标 {last.effectiveMarkerTime:0.###} 秒 · 采集轨时 {last.collectedTrackTime:0.###} 秒",EditorStyles.wordWrappedMiniLabel);
        }

        private static string Choose(string label,string value,string[] choices)
        {
            if(!string.IsNullOrWhiteSpace(value) && !choices.Contains(value)) choices=choices.Append(value).ToArray();
            return choices[EditorGUILayout.Popup(label,Mathf.Max(0,Array.IndexOf(choices,value)),choices)];
        }
        private static void Capture(AnimationCalibrationBinding b,Spine.Event[] list,int i)
        {
            var e=list[i];b.eventName=e.Data.Name;b.occurrence=list.Take(i).Count(x=>x.Data.Name==b.eventName);b.sourceTime=e.Time;
            b.sourceInt=e.Int;b.sourceFloat=e.Float;b.sourceString=e.String;b.sourceCount=list.Count(x=>x.Data.Name==b.eventName);
            b.previousEvent=i>0?list[i-1].Data.Name:"";b.nextEvent=i+1<list.Length?list[i+1].Data.Name:"";
        }
        private void DrawBinding(AnimationCalibrationBinding b,SkeletonData data)
        {
            var standard = StandardMeaning(b.eventName);
            if (standard.HasValue && b.semantic == standard.Value)
                EditorGUILayout.LabelField("触发", MeaningLabel(standard.Value));
            else
                b.semantic = (AnimationEventMeaning)EditorGUILayout.Popup("触发用途", (int)b.semantic, MeaningOptions.Skip(1).ToArray());
            b.offsetFrames=EditorGUILayout.DelayedIntField("标记偏移（帧）",b.offsetFrames);
            EditorGUILayout.LabelField($"源 {b.sourceTime*draft.framesPerSecond:0.##} 帧   →   有效 {b.sourceTime*draft.framesPerSecond+b.offsetFrames:0.##} 帧（{1000*(b.sourceTime+(float)b.offsetFrames/draft.framesPerSecond):0.#} ms）");
            b.sound=(AudioClip)EditorGUILayout.ObjectField("声音",b.sound,typeof(AudioClip),false);
            b.soundOffsetFrames=EditorGUILayout.DelayedIntField("声音补偿（帧）",b.soundOffsetFrames);
            if(b.sound!=null)b.volume=EditorGUILayout.Slider("音量",b.volume,0,1);
            if(b.sound!=null && GUILayout.Button("试听声音"))
            {
                if(preview==null){var temp=UnityEditor.EditorUtility.CreateGameObjectWithHideFlags("校准试听",HideFlags.HideAndDontSave,typeof(AudioSource));preview=temp.GetComponent<AudioSource>();}
                preview.Stop();preview.clip=b.sound;preview.volume=b.volume;preview.Play();
            }
            b.effect=(GameObject)EditorGUILayout.ObjectField("特效预制体",b.effect,typeof(GameObject),false);
            b.effectOffsetFrames=EditorGUILayout.DelayedIntField("特效补偿（帧）",b.effectOffsetFrames);
            b.anchor=Choose("骨骼 / 插槽锚点", b.anchor, new[]{""}.Concat(data.Bones.Select(x=>x.Name)).Concat(data.Slots.Select(x=>"slot:"+x.Name)).ToArray());
            b.localOffset=EditorGUILayout.Vector3Field("挂点局部偏移",b.localOffset);
            if(b.effect!=null)
            {
                b.rotation=EditorGUILayout.DelayedFloatField("局部旋转（度）",b.rotation);
                b.scale=Mathf.Max(.001f,EditorGUILayout.DelayedFloatField("效果缩放",b.scale));
                b.lifetime=Mathf.Max(.001f,EditorGUILayout.DelayedFloatField("效果寿命（秒）",b.lifetime));
                b.follow=EditorGUILayout.Toggle("跟随挂点",b.follow);
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUILayout.Button("在当前角色挂点试放特效"))
                    {
                        var driver = actor.GetComponentInChildren<Ossastra.CharacterAnimation.CharacterAnimationController>(true);
                        if (driver != null && driver.Skeleton?.Skeleton != null)
                        {
                            var instance = Object.Instantiate(b.effect, driver.AnchorPosition(b), Quaternion.Euler(0, 0, b.rotation));
                            instance.transform.localScale *= b.scale;
                            Object.Destroy(instance, b.lifetime);
                        }
                    }
                }
            }
        }
        private void Bind(ActorRoot who)
        {
            Dispose();
            actor = who;
            Reload();
            behavior = clipName = pendingEvent = null;
            eventIndex = track = 0;
            Undo.undoRedoPerformed += OnUndoRedo;
        }
        private string sourceSnapshot;

        private void Reload()
        {
            baseline = actor.Definition.eventCalibration;
            if (draft != null) Object.DestroyImmediate(draft);
            draft = baseline != null ? Object.Instantiate(baseline) : ScriptableObject.CreateInstance<AnimationCalibrationSO>();
            draft.skeleton = actor.GetComponentInChildren<Spine.Unity.SkeletonAnimation>(true).SkeletonDataAsset;
            snapshot = EditorJsonUtility.ToJson(draft);
            sourceSnapshot = baseline != null ? EditorJsonUtility.ToJson(baseline) : null;
            message = null;
        }

        private bool SourceChanged() => actor.Definition.eventCalibration != baseline ||
            (baseline != null && EditorJsonUtility.ToJson(baseline) != sourceSnapshot);

        private void OnUndoRedo()
        {
            if (actor == null || draft == null || !SourceChanged()) return;
            if (baseline != null) AssetDatabase.SaveAssetIfDirty(baseline);
            AssetDatabase.SaveAssetIfDirty(actor.Definition);
            Reload();
        }

        private bool SaveIfChanged()
        {
            if (!Dirty) return true;
            if (SourceChanged())
                return Defer("当前配置已在别处变更。撤销未保存的更改即可重新读取。");

            var skeleton = draft.skeleton.GetSkeletonData(true);
            var bindings = draft.bindings ?? Array.Empty<AnimationCalibrationBinding>();
            foreach (var binding in bindings)
            {
                if (skeleton == null || binding == null || string.IsNullOrWhiteSpace(binding.behavior) ||
                    !draft.Valid(binding, skeleton.FindAnimation(binding.animation)))
                    return Defer("尚未保存。请检查源事件、挂点及偏移，有效帧须在动画范围内。");

                var standard = StandardMeaning(binding.eventName);
                if (standard.HasValue && standard.Value != binding.semantic)
                    return Defer($"尚未保存。{binding.eventName} 对应「{MeaningLabel(standard.Value)}」。请核对绑定。");
            }

            foreach (var group in bindings.Where(b => b.semantic != AnimationEventMeaning.Footstep)
                         .GroupBy(b => (b.behavior, b.animation, b.track)))
            {
                var finishes = group.Where(b => b.semantic == AnimationEventMeaning.Finish).ToArray();
                if (group.GroupBy(b => (b.eventName, b.occurrence)).Any(markers => markers.Count() > 1))
                    return Defer($"尚未保存。{group.Key.animation} 的同一源事件重复绑定。");
                if (finishes.Length != 1 || !group.Any(b => b.semantic == AnimationEventMeaning.Release))
                    return Defer($"尚未保存。{group.Key.animation} 需要出手事件和一个结束事件。");
                float EffectiveTime(AnimationCalibrationBinding b) => b.sourceTime + (float)b.offsetFrames / draft.framesPerSecond;
                if (group.Any(b => EffectiveTime(b) > EffectiveTime(finishes[0])))
                    return Defer($"尚未保存。{group.Key.animation} 的出手和冲刺许可须在结束之前或同一帧。");
            }

            var migrated = (baseline?.migratedBehaviors ?? Array.Empty<string>())
                .Concat(draft.migratedBehaviors ?? Array.Empty<string>())
                .Concat(bindings.Where(b => b.semantic != AnimationEventMeaning.Footstep).Select(b => b.behavior))
                .Where(x => !string.IsNullOrEmpty(x)).Distinct().ToArray();
            if (migrated.Any(id => !bindings.Any(b => b.behavior == id && b.semantic == AnimationEventMeaning.Release)))
                return Defer("尚未保存。已接入事件驱动的动作需要保留出手事件。");

            draft.migratedBehaviors = migrated;
            if (baseline == null)
            {
                string definitionPath = AssetDatabase.GetAssetPath(actor.Definition);
                if (string.IsNullOrEmpty(definitionPath) || !definitionPath.StartsWith("Assets/", StringComparison.Ordinal))
                    return Defer("此角色缺少可写入的角色定义资产。");

                string path = AssetDatabase.GenerateUniqueAssetPath(
                    System.IO.Path.ChangeExtension(definitionPath, null) + "_事件校准.asset");
                baseline = Object.Instantiate(draft);
                AssetDatabase.CreateAsset(baseline, path);
                Undo.RegisterCreatedObjectUndo(baseline, "创建动画校准");
                Undo.RecordObject(actor.Definition, "绑定正式动画校准");
                actor.Definition.eventCalibration = baseline;
                EditorUtility.SetDirty(actor.Definition);
                AssetDatabase.SaveAssetIfDirty(actor.Definition);
            }
            else
            {
                Undo.RecordObject(baseline, "调整动画校准");
                draft.name = baseline.name;
                EditorUtility.CopySerialized(draft, baseline);
            }
            EditorUtility.SetDirty(baseline);
            Undo.FlushUndoRecordObjects();
            AssetDatabase.SaveAssetIfDirty(baseline);
            sourceSnapshot = EditorJsonUtility.ToJson(baseline);
            snapshot = EditorJsonUtility.ToJson(draft);
            message = null;
            return true;
        }

        private bool Defer(string reason)
        {
            message = reason;
            return false;
        }

        private static AnimationEventMeaning? StandardMeaning(string name)
        {
            switch (name)
            {
                case "Hit": return AnimationEventMeaning.Release;
                case "动作结束可覆盖": return AnimationEventMeaning.Finish;
                case "可冲刺打断": return AnimationEventMeaning.DodgeCancel;
                case "FootStep":
                case "Footstep": return AnimationEventMeaning.Footstep;
                default: return null;
            }
        }

        private static string MeaningLabel(AnimationEventMeaning meaning) => MeaningOptions[(int)meaning + 1];

        public void Dispose()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            if (preview != null) Object.DestroyImmediate(preview.gameObject);
            if (draft != null) Object.DestroyImmediate(draft);
            draft = null;
        }
    }
}
