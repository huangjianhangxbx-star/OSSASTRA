/*
 * 脚本：AttackLabTuningPanel.Apply
 * 功能：复位并应用手感候选，执行明确的字段写回与实验预设保存。
 * 边界：运行覆盖只属于当前明示的角色和组件，不替换其他演员的配置。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Ossastra.Characters;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ossastra.Development.Editor
{
    public sealed partial class AttackLabTuningPanel
    {
        private Dictionary<ScriptableObject,ScriptableObject> BuildCopies(IEnumerable<Field> selected)
        {
            var copies = new Dictionary<ScriptableObject,ScriptableObject>();
            ScriptableObject Copy(ScriptableObject original)
            {
                if (!copies.TryGetValue(original,out var copy))
                {
                    copy=Object.Instantiate(original);
                    copy.name=original.name;
                    copy.hideFlags=HideFlags.HideAndDontSave;
                    copies.Add(original,copy);
                }
                return copy;
            }
            try
            {
                foreach (var f in selected)
                {
                    if (float.IsNaN(f.pending)||float.IsInfinity(f.pending)||f.pending<f.min||f.pending>f.max||f.integer&&f.pending!=Mathf.Floor(f.pending))
                        throw new ArgumentException(f.title+" 数值或范围无效");
                    if (f.source is ScriptableObject asset) Set(Copy(asset),f,f.pending);
                }
                // 主手的各段分别引用动作资产。只在临时动作组中重接被替换的段。
                if (profile && (copies.ContainsKey(profile)||copies.Keys.Any(s=>s is ActionDefinitionSO)))
                {
                    var p=(MainActionProfileSO)Copy(profile);
                    foreach(var stage in p.stages.Concat(p.rapidPrefix).Concat(p.rapidPlusPrefix).Concat(new[]{p.chisel}))
                        if(stage?.action && copies.TryGetValue(stage.action,out var a)) stage.action=(ActionDefinitionSO)a;
                    p.Freeze(abilities?.SnapshotEffects());
                }
                foreach(var action in copies.Values.OfType<ActionDefinitionSO>()) action.Freeze();
                if(skill && copies.TryGetValue(skill,out var ability)) ((AbilityDefinitionSO)ability).Compile();
                return copies;
            }
            catch { DestroyCopies(copies.Values); throw; }
        }
        private static void DestroyCopies(IEnumerable<ScriptableObject> values)
        {
            foreach(var value in values) if(value) Object.DestroyImmediate(value);
        }
        private bool SourcesValid(out string reason)
        {
            if(!actor||fields.Count==0){reason="尚未接到正式角色配置";return false;}
            if(Stamp()!=revision)
            {
                var changed = fields.FirstOrDefault(f => f.writable && !Read(f.writable, f.path).Equals(f.baseline));
                string details = changed == null ? "来源资产或引用的版本不同" :
                    $"{changed.title} {Pretty(changed.baseline)} → {Pretty(Read(changed.writable, changed.path))}（{changed.Location}）";
                reason="配置来源已变更："+details+"。候选已保留，可另存预设或重新读取基线。";
                return false;
            }
            var drift=fields.Find(f=>f.source is Component && !Read(f.source,f.path).Equals(f.effective));
            if(drift!=null){reason="运行组件已被其他工具修改："+drift.title;return false;}
            reason=null;return true;
        }
        public bool CanApply(bool candidate,out string reason)
        {
            if(!SourcesValid(out reason))return false;
            if(!candidate)return true;
            try{var copies=BuildCopies(fields.Where(f=>f.Changed));DestroyCopies(copies.Values);return true;}
            catch(Exception e){reason="候选无效："+e.Message;return false;}
        }

        public bool ApplyAfterReset(Ossastra.Characters.CharacterRoot target,bool candidate,out string reason)
        {
            Bind(target);
            if(!SourcesValid(out reason)){ ReleaseOverrides(); return false; }
            if(actor.ActionActive || abilities && abilities.IsBusy){reason="仍有执行中的攻击或技能，请先复位本次试验。";return false;}
            Dictionary<ScriptableObject,ScriptableObject> copies;
            try{copies=BuildCopies(candidate?fields.Where(f=>f.Changed):Enumerable.Empty<Field>());}
            catch(Exception e){ReleaseOverrides();reason="候选无效，已恢复基线："+e.Message;return false;}
            try
            {
                ReleaseOverrides();
                if(copies.Count>0)
                {
                    liveCopies=copies.Values.ToList();
                    active=new AttackLabBindings(actor,copies,library);
                    active.Activate();
                }
                bool changedSceneCombo = false;
                foreach (var f in fields)
                {
                    float next = candidate ? f.pending : f.baseline;
                    if (f.source is Component && next != f.baseline)
                    {
                        Set(f.source, f, next);
                        changedSceneCombo |= f.source is BasicChainProfile;
                    }
                    f.effective = next;
                }
                if (changedSceneCombo) actor.StateArbitrator?.Combat?.RebindLaboratorySequence();
                reason=null;return true;
            }
            catch(Exception e){ReleaseOverrides();DestroyCopies(copies.Values);reason="应用失败，已恢复基线："+e.Message;return false;}
        }
        // 招式效果独立试打只归还运行覆盖。手感候选仍可继续编辑或另存。
        public void ReleasePreviewForImpactTrial() => ReleaseOverrides();

        private void ReleaseOverrides()
        {
            bool refreshAtIdle = active != null && actor && actor.ActionActive;
            active?.Dispose();active=null;
            bool restoredSceneCombo = false;
            foreach(var f in fields)
            {
                if (f.source && f.source is Component && f.effective != f.baseline && Read(f.source,f.path) == f.effective)
                {
                    Set(f.source, f, f.baseline);
                    restoredSceneCombo |= f.source is BasicChainProfile;
                }
                f.effective=f.baseline;
            }
            if (restoredSceneCombo && actor)
            {
                // 旧场实例也按空闲边界换快照，退出实验时不清空正在执行的连段。
                if (actor.ActionActive) refreshAtIdle = true;
                else actor.StateArbitrator?.Combat?.RebindLaboratorySequence();
            }
            var retired = liveCopies;
            liveCopies = new List<ScriptableObject>();
            if (refreshAtIdle || (retired.Count > 0 && abilities && abilities.IsBusy))
            {
                var owner = actor;
                var actions = abilities;
                void Finish()
                {
                    if (EditorApplication.isPlaying && owner && (owner.ActionActive || actions && actions.IsBusy)) return;
                    EditorApplication.update -= Finish;
                    if (EditorApplication.isPlaying && owner && refreshAtIdle)
                        owner.StateArbitrator?.Combat?.RebindLaboratorySequence();
                    DestroyCopies(retired);
                }
                EditorApplication.update += Finish;
            }
            else DestroyCopies(retired);
        }
        public void Dispose()
        {
            ReleaseOverrides();fields.Clear();timingSourceNotes.Clear();actor=null;equipment=null;abilities=null;skill=null;profile=null;library=null;
            binding=null;revision=null;message=null;
        }

        public bool SavePreset(string assetPath,out string reason)
        {
            if(!actor||fields.Count==0){reason="需要已绑定的测试角色";return false;}
            if(!ValidNewAssetPath(assetPath)){reason="请选择 Assets 内未占用的 .asset 路径";return false;}
            try { var checkedCopies = BuildCopies(fields.Where(f => f.Changed)); DestroyCopies(checkedCopies.Values); }
            catch (Exception e) { reason = "候选无法执行："+e.Message; return false; }
            var p=ScriptableObject.CreateInstance<AttackLabPreset>();
            p.binding=binding;p.baselineRevision=revision;p.conditionJson=ConditionJson;
            p.changes=fields.Where(f=>f.Changed).Select(f=>new AttackLabChange{id=f.id,value=f.pending}).ToArray();
            try
            {
                AssetDatabase.CreateAsset(p,assetPath);
                if(!AssetDatabase.Contains(p)) throw new IOException("实验预设没有写入资产库");
                AssetDatabase.SaveAssetIfDirty(p); reason=null;return true;
            }
            catch(Exception e){if(!AssetDatabase.Contains(p))Object.DestroyImmediate(p);reason=e.Message;return false;}
        }
        private static bool ValidNewAssetPath(string path)
        {
            if(string.IsNullOrEmpty(path)||!path.StartsWith("Assets/",StringComparison.Ordinal)||!path.EndsWith(".asset",StringComparison.OrdinalIgnoreCase))return false;
            if(path.Split('/').Any(s=>s==".."||s=="."||s.Length==0)||path.Contains("\\"))return false;
            return AssetDatabase.IsValidFolder(Path.GetDirectoryName(path)?.Replace('\\','/')) && !File.Exists(path)&&!AssetDatabase.LoadMainAssetAtPath(path);
        }
        public bool LoadPreset(string assetPath,out string reason)
        {
            var p=AssetDatabase.LoadAssetAtPath<AttackLabPreset>(assetPath);
            if(!p||p.schema!=1||p.changes==null){reason="实验预设格式无效";return false;}
            if(p.binding!=binding || p.baselineRevision!=revision || !SourcesValid(out _))
            {reason="预设与此角色、武器或源版本不一致，已保留当前实验。";return false;}
            var before=fields.Select(f=>f.pending).ToArray();
            foreach(var f in fields)f.pending=f.baseline;
            try
            {
                var seen=new HashSet<string>();
                foreach(var change in p.changes)
                {
                    if(change==null||!seen.Add(change.id))throw new ArgumentException("预设包含空值或重复参数");
                    if(!TrySet(change.id,change.value,out reason))throw new ArgumentException(reason);
                }
                if(!CanApply(true,out reason))throw new ArgumentException(reason);
                RestoreCondition?.Invoke(p.conditionJson);
                ConditionJson=p.conditionJson;reason=null;return true;
            }
            catch(Exception e){for(int i=0;i<fields.Count;i++)fields[i].pending=before[i];reason=e.Message;return false;}
        }
        public bool WriteSelected(string[] ids,out string reason)
        {
            if(!SourcesValid(out reason))return false;
            if(actor.ActionActive || abilities && abilities.IsBusy){reason="请先结束当前动作";return false;}
            var chosen=fields.Where(f=>f.Changed&&ids!=null&&ids.Contains(f.id)).ToArray();
            if(chosen.Length==0){reason="请勾选要写回的改动";return false;}
            var blocked=chosen.FirstOrDefault(f=>!f.writable||EditorUtility.IsDirty(f.writable)||!AssetDatabase.IsOpenForEdit(f.writable));
            if(blocked!=null){reason="来源为场内实例、有未保存修改或不可写："+blocked.Location;return false;}
            try{var check=BuildCopies(chosen);DestroyCopies(check.Values);}
            catch(Exception e){reason="仅写这些字段会导致无效配置："+e.Message;return false;}
            var targets=chosen.Select(f=>f.writable).Distinct().ToArray();
            var prior=fields.ToDictionary(f=>f.id,f=>f.pending);
            var oldValues=chosen.Select(f=>Read(f.writable,f.path)).ToArray();
            var current=actor;
            Undo.IncrementCurrentGroup();
            int group=Undo.GetCurrentGroup();
            try
            {
                Undo.SetCurrentGroupName("写回调试台所选参数");
                Undo.RegisterCompleteObjectUndo(targets,"写回调试台所选参数");
                foreach(var f in chosen){Set(f.writable,f,f.pending);EditorUtility.SetDirty(f.writable);}
                foreach(var t in targets)SaveTarget(t);
                foreach(var f in chosen) if(!Read(f.writable,f.path).Equals(f.pending))throw new IOException("写回后值不一致："+f.title);
                Undo.CollapseUndoOperations(group);
            }
            catch(Exception e)
            {
                Undo.RevertAllDownToGroup(group);
                for(int i=0;i<chosen.Length;i++){Set(chosen[i].writable,chosen[i],oldValues[i]);EditorUtility.SetDirty(chosen[i].writable);}
                foreach(var t in targets)SaveTarget(t);
                reason="写回失败，已还原原值："+e.Message;return false;
            }
            Dispose();
            // Play 中已有的实例可能要等下次实例化才收到 Prefab 的变化，显式更新已写回的字段。
            foreach(var f in chosen)if(f.source is Component && f.source) Set(f.source,f,f.pending);
            Bind(current);
            foreach(var f in fields)if(prior.TryGetValue(f.id,out var value))f.pending=value;
            current.StateArbitrator?.Combat?.RebindLaboratorySequence();
            reason=null;return true;
        }
        private static void SaveTarget(Object target)
        {
            if(target is Component c)
            {
                PrefabUtility.SavePrefabAsset(c.transform.root.gameObject,out bool success);
                if(!success)throw new IOException("未能保存 Prefab "+AssetDatabase.GetAssetPath(target));
            }
            else AssetDatabase.SaveAssetIfDirty(target);
        }
    }
}
