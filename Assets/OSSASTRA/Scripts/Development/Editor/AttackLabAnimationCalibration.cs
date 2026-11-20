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
using AnimationHandle = Ossastra.CharacterAnimation.AnimationHandleSystem;
using Object = UnityEngine.Object;

namespace Ossastra.Development.Editor
{
    internal sealed class AttackLabAnimationCalibration : IDisposable
    {
        private ActorRoot actor;
        private AnimationCalibrationSO draft, applied, baseline;
        private AnimationHandle appliedHandle;
        private string snapshot, behavior, clipName, message;
        private int track, eventIndex;
        private Vector2 scroll;
        private AudioSource preview;
        private static readonly string[] MeaningOptions = {"选择项目语义","出手释放","结束并开放覆盖","允许冲刺打断","脚步"};
        private string pendingEvent;
        private int pendingMeaning;
        private bool definitions;
        private bool Dirty => draft != null && snapshot != EditorJsonUtility.ToJson(draft);

        public void Draw(AttackLabSession session)
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            try { DrawContents(session); }
            finally { EditorGUILayout.EndScrollView(); }
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
            draft.framesPerSecond=Mathf.Clamp(EditorGUILayout.IntField("制作帧率",draft.framesPerSecond),1,240);
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
                EditorGUILayout.LabelField($"{b.eventName} #{b.occurrence+1}   {b.sourceTime*draft.framesPerSecond:0.##} 帧   →   {b.semantic}");
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
                    if (pendingEvent != key)
                    {
                        pendingEvent = key;
                        pendingMeaning = Array.IndexOf(new[] { "Hit","动作结束可覆盖","可冲刺打断","FootStep" }, e.Data.Name) + 1;
                    }
                    pendingMeaning = EditorGUILayout.Popup("要绑定的语义",pendingMeaning, MeaningOptions);
                    using (new EditorGUI.DisabledScope(pendingMeaning == 0))
                    {
                        if(GUILayout.Button("为此事件添加绑定"))
                        {
                            b=new AnimationCalibrationBinding{animation=clipName,behavior=behavior,track=track,
                                semantic=(AnimationEventMeaning)(pendingMeaning - 1)};
                            Capture(b,events,eventIndex); draft.bindings=entries.Append(b).ToArray();
                        }
                    }
                }
                else DrawBinding(b,data);
            }
            EditorGUILayout.Space();
            if(session!=null && session.Actor==actor && Application.isPlaying)
            {
                if(GUILayout.Button("应用候选并复位"))
                {
                    var h=actor.GetComponentInChildren<AnimationHandle>(true);
                    if(h!=null && session.ResetTrial("事件校准候选")){ReleaseApplied();applied=Object.Instantiate(draft);appliedHandle=h;h.CandidateCalibration=applied;message="候选已应用到本次试验。";}
                }
            }
            if (GUILayout.Button("恢复正式基线"))
            {
                ReleaseApplied();
                Bind(actor);
                if (Application.isPlaying && session != null && session.Actor == actor) session.ResetTrial("恢复正式校准");
                message = "已恢复当前角色的正式校准。";
                return;
            }
            if(GUILayout.Button("另存实验校准预设"))
            {
                string path=EditorUtility.SaveFilePanelInProject("保存候选",actor.name+"_校准实验","asset","选择项目路径");
                if(path.Length>0 && AssetDatabase.LoadMainAssetAtPath(path)==null){var copy=Object.Instantiate(draft);AssetDatabase.CreateAsset(copy,path);AssetDatabase.SaveAssetIfDirty(copy);}
            }
            var preset=(AnimationCalibrationSO)EditorGUILayout.ObjectField("读取实验校准预设",null,typeof(AnimationCalibrationSO),false);
            if(preset!=null && preset.MatchesSkeleton(draft.skeleton) && (!Dirty || EditorUtility.DisplayDialog("读取预设","替换当前候选？","读取","取消")))
                EditorUtility.CopySerialized(preset,draft);
            EditorGUILayout.LabelField("正式保存目标", baseline ? AssetDatabase.GetAssetPath(baseline) : actor.Definition.name, EditorStyles.wordWrappedMiniLabel);
            var original = baseline?.bindings ?? Array.Empty<AnimationCalibrationBinding>();
            var changes = entries.Where(x => x != null).Select(x =>
            {
                var old = original.FirstOrDefault(y => y != null && y.behavior == x.behavior && y.animation == x.animation &&
                    y.track == x.track && y.occurrence == x.occurrence && y.eventName == x.eventName);
                if (old == null) return $"新增 {x.behavior} / {x.eventName} #{x.occurrence + 1}";
                if (JsonUtility.ToJson(old) == JsonUtility.ToJson(x)) return null;
                return $"{x.behavior} / {x.eventName} #{x.occurrence + 1} · 标记 {old.offsetFrames} → {x.offsetFrames} 帧 · 声音 {old.soundOffsetFrames} → {x.soundOffsetFrames} 帧 · 特效 {old.effectOffsetFrames} → {x.effectOffsetFrames} 帧";
            }).Where(x=>x != null);
            foreach (var change in changes) EditorGUILayout.LabelField(change,EditorStyles.wordWrappedMiniLabel);
            if(GUILayout.Button("保存正式校准"))Save();
            if(!string.IsNullOrEmpty(message))EditorGUILayout.HelpBox(message,MessageType.Info);
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
            b.semantic=(AnimationEventMeaning)(EditorGUILayout.Popup("项目语义",(int)b.semantic, MeaningOptions.Skip(1).ToArray()));
            b.offsetFrames=EditorGUILayout.IntField("标记偏移（帧）",b.offsetFrames);
            EditorGUILayout.LabelField($"源 {b.sourceTime*draft.framesPerSecond:0.##} 帧   →   有效 {b.sourceTime*draft.framesPerSecond+b.offsetFrames:0.##} 帧（{1000*(b.sourceTime+(float)b.offsetFrames/draft.framesPerSecond):0.#} ms）");
            b.sound=(AudioClip)EditorGUILayout.ObjectField("声音",b.sound,typeof(AudioClip),false);
            b.soundOffsetFrames=EditorGUILayout.IntField("声音补偿（帧）",b.soundOffsetFrames);
            if(b.sound!=null)b.volume=EditorGUILayout.Slider("音量",b.volume,0,1);
            if(b.sound!=null && GUILayout.Button("试听声音"))
            {
                if(preview==null){var temp=UnityEditor.EditorUtility.CreateGameObjectWithHideFlags("校准试听",HideFlags.HideAndDontSave,typeof(AudioSource));preview=temp.GetComponent<AudioSource>();}
                preview.Stop();preview.clip=b.sound;preview.volume=b.volume;preview.Play();
            }
            b.effect=(GameObject)EditorGUILayout.ObjectField("特效预制体",b.effect,typeof(GameObject),false);
            b.effectOffsetFrames=EditorGUILayout.IntField("特效补偿（帧）",b.effectOffsetFrames);
            b.anchor=Choose("骨骼 / 插槽锚点", b.anchor, new[]{""}.Concat(data.Bones.Select(x=>x.Name)).Concat(data.Slots.Select(x=>"slot:"+x.Name)).ToArray());
            b.localOffset=EditorGUILayout.Vector3Field("挂点局部偏移",b.localOffset);
            if(b.effect!=null)
            {
                b.rotation=EditorGUILayout.FloatField("局部旋转（度）",b.rotation);
                b.scale=Mathf.Max(.001f,EditorGUILayout.FloatField("效果缩放",b.scale));
                b.lifetime=Mathf.Max(.001f,EditorGUILayout.FloatField("效果寿命（秒）",b.lifetime));
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
            Dispose();actor=who;baseline=actor.Definition.eventCalibration;
            draft=baseline!=null?Object.Instantiate(baseline):ScriptableObject.CreateInstance<AnimationCalibrationSO>();
            draft.skeleton=actor.GetComponentInChildren<Spine.Unity.SkeletonAnimation>(true).SkeletonDataAsset;
            snapshot=EditorJsonUtility.ToJson(draft);sourceSnapshot=baseline!=null?EditorJsonUtility.ToJson(baseline):null;
            behavior=clipName=message=null;eventIndex=track=0;
        }
        private string sourceSnapshot;
        private void Save()
        {
            draft.migratedBehaviors = (baseline?.migratedBehaviors ?? Array.Empty<string>())
                .Concat(draft.migratedBehaviors ?? Array.Empty<string>())
                .Concat((baseline?.bindings ?? Array.Empty<AnimationCalibrationBinding>()).Concat(draft.bindings ?? Array.Empty<AnimationCalibrationBinding>())
                    .Where(x => x != null && x.semantic != AnimationEventMeaning.Footstep).Select(x => x.behavior))
                .Where(x => !string.IsNullOrEmpty(x)).Distinct().ToArray();
            var skeleton=draft.skeleton.GetSkeletonData(true);
            foreach(var b in draft.bindings ?? Array.Empty<AnimationCalibrationBinding>())
            {
                if(skeleton==null || b==null || string.IsNullOrWhiteSpace(b.behavior) ||
                    !draft.Valid(b,skeleton.FindAnimation(b.animation)))
                {message="有事件、有效帧或挂点与当前骨架失配，请先核对绑定。";return;}
            }
            if(actor.Definition.eventCalibration!=baseline || (baseline!=null && EditorJsonUtility.ToJson(baseline)!=sourceSnapshot))
            {message="正式配置已在别处变更，请重新读取后合并。";return;}
            if(!EditorUtility.DisplayDialog("保存正式校准",$"{actor.Definition.name} / {draft.skeleton.name}\n绑定条数 {baseline?.bindings?.Length??0} → {draft.bindings?.Length??0}。此角色定义的所有实例均会采用。","保存","取消"))return;
            if(baseline==null)
            {
                string path=EditorUtility.SaveFilePanelInProject("保存正式校准",actor.Definition.name+"_事件校准","asset","选择正式配置目录");
                if(string.IsNullOrEmpty(path)||AssetDatabase.LoadMainAssetAtPath(path)!=null)return;
                baseline=Object.Instantiate(draft);AssetDatabase.CreateAsset(baseline,path);
                Undo.RecordObject(actor.Definition,"绑定正式动画校准");actor.Definition.eventCalibration=baseline;
                EditorUtility.SetDirty(actor.Definition);AssetDatabase.SaveAssetIfDirty(actor.Definition);
            }
            else {Undo.RecordObject(baseline,"保存正式动画校准");draft.name=baseline.name;EditorUtility.CopySerialized(draft,baseline);}
            EditorUtility.SetDirty(baseline);AssetDatabase.SaveAssetIfDirty(baseline);
            sourceSnapshot=EditorJsonUtility.ToJson(baseline);snapshot=EditorJsonUtility.ToJson(draft);message="正式校准已保存。";
        }
        private void ReleaseApplied(){if(appliedHandle!=null && appliedHandle.CandidateCalibration==applied)appliedHandle.CandidateCalibration=null;if(applied!=null)Object.DestroyImmediate(applied);appliedHandle=null;applied=null;}
        public void Dispose(){ReleaseApplied();if(preview!=null)Object.DestroyImmediate(preview.gameObject);if(draft!=null)Object.DestroyImmediate(draft);draft=null;}
    }
}
