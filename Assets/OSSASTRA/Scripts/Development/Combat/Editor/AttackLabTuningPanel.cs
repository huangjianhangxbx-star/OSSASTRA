/*
 * 脚本：AttackLabTuningPanel
 * 功能：从正式角色的实控动作组读取连段与调参来源，组织试验候选。
 * 边界：绘制与候选状态不成为正式运行配置，写回需通过现有明确保存入口。
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ossastra.CharacterCore;
using Ossastra.Characters;
using Ossastra.Combat;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ossastra.Development.Editor
{
    /// <summary>从当前实控角色和主手读取有效配置。窗口持有实验候选，明确写回时才保存作者资产。</summary>
    public sealed partial class AttackLabTuningPanel : IDisposable
    {
        private sealed class Field
        {
            public string id, title, path, sourceId;
            public Object source, writable;
            public float baseline, effective, pending, min, max;
            public bool integer, boolean, selected;
            public bool Changed => !pending.Equals(baseline);
            public string Location => (writable ? AssetDatabase.GetAssetPath(writable) : source.name + "（场内组件）") + " · " + path;
        }
        private readonly List<Field> fields = new List<Field>();
        private readonly List<string> timingSourceNotes = new List<string>();
        private Ossastra.Characters.CharacterRoot actor;
        private CharacterEquipment equipment;
        private CharacterAbilities abilities;
        private MainActionProfileSO profile;
        private ActionDefinitionSO[] library;
        private AbilityDefinitionSO skill;
        private string binding, revision, message;
        private AttackLabBindings active;
        private List<ScriptableObject> liveCopies = new List<ScriptableObject>();
        private bool showSource, showSaving;
        private int section;
        public Action<bool> RequestReset;
        public Action<string> RestoreCondition;
        public string ConditionJson { get; set; }
        public int ObservedSkillSlot { get; set; } = -1;
        public string Summary => string.Join("\n", fields.Where(f => f.Changed || f.effective != f.baseline)
            .Select(f => $"{f.title}  来源 {Pretty(f.baseline)} / 生效 {Pretty(f.effective)} / 候选 {Pretty(f.pending)}  {f.Location}"));
        public string[] FieldIds => fields.Select(f => f.id).ToArray();

        public void Bind(Ossastra.Characters.CharacterRoot target)
        {
            if (target == null) { if (!ReferenceEquals(actor,null)) Dispose(); return; }
            var equip = target.GetComponent<CharacterEquipment>();
            var comboSource = target.StateArbitrator?.Combat?.ComboSource;
            var nextProfile = comboSource as MainActionProfileSO;
            var skills = target.GetComponent<CharacterAbilities>();
            var nextSkill = skills && skills.isActiveAndEnabled && equip?.State != null
                ? skills.SkillDefinition(ObservedSkillSlot) ?? Enumerable.Range(0,3).Select(skills.SkillDefinition).FirstOrDefault(s => s) : null;
            var right = equip?.RightDefinition;
            var nextLibrary = right && right.actions != null && right.actions.Length > 0 ? right.actions : target.Definition?.basicAttacks;
            string identity = target.ActorId + "|" + AssetId(target.Definition) + "|" + AssetId(right) + "|" + AssetId(nextProfile) + "|" + AssetId(nextSkill);
            var sequence = target.GetComponent<CharacterMainAction>()?.Sequence;
            if (nextProfile && sequence != null)
            {
                var stages = Enumerable.Range(0, sequence.Count).Select(sequence.Stage);
                identity += "|" + string.Join(",", stages.Select(s => string.IsNullOrEmpty(s.SourceId) ? s.Id : s.Id + "=" + s.Timeline.Damage));
            }
            if (target == actor && identity == binding) return;
            var previous = identity == binding ? fields.ToDictionary(f => f.id, f => f.pending) : null;
            var previousVersion = revision;
            Dispose();
            actor = target; equipment = equip; abilities = skills; binding = identity;
            profile = nextProfile; skill = nextSkill; library = nextLibrary ?? Array.Empty<ActionDefinitionSO>();
            AddCombo(comboSource);
            if (profile) AddProfile();
            else
            {
                for (int i=0; i<library.Length; i++) AddTiming(library[i],i+1);
                var hit = actor.GetComponent<CombatMeleeHit>();
                if (hit)
                {
                    Add("basic.reach","查球前移距离（世界单位）",hit,"reach");
                    Add("basic.radius","实际查球半径（世界单位）",hit,"radius",.01f);
                }
            }
            if (skill)
            {
                Add("skill.cooldown",skill.displayName+" 冷却（秒）",skill,"cooldown");
                Add("skill.cost",skill.displayName+" 受理费用",skill,"acceptCost");
            }
            var lens = actor.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SideViewCameraFollow>(true))
                .FirstOrDefault(c => c.isActiveAndEnabled);
            if (lens) Add("camera.shake","命中震屏强度（0 为关闭）",lens,"shake.intensity",0,1);
            revision = Stamp();
            if (previous != null && previousVersion == revision)
                foreach (var f in fields) if (previous.TryGetValue(f.id,out var value)) f.pending = value;
        }

        private void AddCombo(Object source)
        {
            if (!source) return;
            using var serialized = new SerializedObject(source);
            // 历史来源尚未迁移时由角色状态显示实际兼容值，保存前避免写到未生效的新字段。
            if (serialized.FindProperty("comboSettings.version")?.intValue != 1) return;
            Add("basic.buffer", "普攻预输入保留时间（秒）", source, "comboSettings.retentionSeconds");
            Add("basic.continuation", "连段记忆时间（秒）", source, "comboSettings.continuationSeconds");
            Add("basic.requireHit", "进入下一段需要命中", source, "comboSettings.requireHit", 0, 1);
        }

        private void AddProfile()
        {
            Add("basic.recovery","末段额外收尾（秒）",profile,"fullComboRecovery");
            Add("basic.contact","有效接触时长（秒）",profile,"contactDuration");
            var frozen = profile.Freeze(abilities?.SnapshotEffects(), actor.Definition?.eventCalibration);
            for (int i=0;i<frozen.Sequence.Count;i++)
            {
                string id = frozen.Sequence.Stage(i).Id;
                string path = StagePath(id);
                // 凿冰的最终伤害来自当前装备效果，时序仍来自动作资产。
                AddTiming(frozen.AnimationDefinition(i),i+1,string.IsNullOrEmpty(frozen.Sequence.Stage(i).SourceId));
                if (path == null) continue;
                var prefix = "basic." + (i+1);
                string name = $"第 {i+1} 段 ";
                Add(prefix+".range",name+"范围（世界单位）",profile,path+".range",.01f);
                Add(prefix+".arc",name+"扇角（度）",profile,path+".arcDegrees",0,360);
                Add(prefix+".dashAt",name+"突进开始（秒）",profile,path+".dashAt");
                Add(prefix+".dashDistance",name+"突进距离（世界单位）",profile,path+".dashDistance");
                Add(prefix+".dashDuration",name+"突进持续（秒）",profile,path+".dashDuration");
            }
        }

        private string StagePath(string id)
        {
            if (profile.chisel != null && profile.chisel.stageId == id) return "chisel";
            foreach (var pair in new[]{("stages",profile.stages),("rapidPrefix",profile.rapidPrefix),("rapidPlusPrefix",profile.rapidPlusPrefix)})
            {
                int i = Array.FindIndex(pair.Item2,s => s != null && s.stageId == id);
                if (i >= 0) return $"{pair.Item1}.Array.data[{i}]";
            }
            return null;
        }

        private void AddTiming(ActionDefinitionSO action,int stage,bool useActionDamage = true)
        {
            if (!action) return;
            if (useActionDamage) Add($"basic.{stage}.damage", $"第 {stage} 段 基础伤害", action, "damage", 1, 1000000);
            try
            {
                var phase = actor.FreezeAbilityPhase(action, new CharacterAnimationPreparation(true, stage, useAuthoredLogic: true));
                if (phase.Timeline.EventDriven)
                {
                    timingSourceNotes.Add($"第 {stage} 段 · 动画事件 · {phase.Clip}");
                    return;
                }
            }
            catch (ArgumentException error)
            {
                timingSourceNotes.Add($"第 {stage} 段 · 动画配置错误 · {error.Message}");
                return;
            }
            timingSourceNotes.Add($"第 {stage} 段 · 定时时序 · {action.name}");
            var markers = new[]{ActionFactKind.Released,ActionFactKind.MoveReady,ActionFactKind.AttackReady,ActionFactKind.Finished};
            string[] ids = {"release","move","ready","finish"}, labels = {"出手","移动许可","再攻击许可","结束"}, fallback = {"releaseAt","moveReadyAt","attackReadyAt","finishAt"};
            for (int i=0;i<markers.Length;i++)
            {
                int n = Array.FindIndex(action.eventTimeline ?? Array.Empty<ActionTimelineBinding>(),e=>e!=null&&e.marker==markers[i]);
                if (n<0 && action.eventTimeline?.Length>0) continue;
                Add($"basic.{stage}."+ids[i],$"第 {stage} 段 {labels[i]}（秒）",action,n<0?fallback[i]:$"eventTimeline.Array.data[{n}].seconds");
            }
        }

        private void Add(string id,string title,Object source,string path,float min=0,float max=float.MaxValue)
        {
            if (fields.Any(f=> f.source==source && f.path==path)) return;
            using var so = new SerializedObject(source);
            var property = so.FindProperty(path);
            if (property==null) return;
            bool boolean = property.propertyType == SerializedPropertyType.Boolean;
            bool integer = boolean || property.propertyType == SerializedPropertyType.Integer;
            float value = ReadValue(property);
            Object persistent = EditorUtility.IsPersistent(source) ? source : null;
            if (!persistent && source is Component c && !property.prefabOverride)
            {
                var prefab = PrefabUtility.GetCorrespondingObjectFromSource(c);
                if (prefab && Read(prefab,path)==value) persistent=prefab;
            }
            fields.Add(new Field{id=id,title=title,path=path,source=source,writable=persistent,sourceId=persistent?AssetId(persistent):source.GetType().FullName,
                baseline=value,effective=value,pending=value,min=min,max=max,integer=integer,boolean=boolean});
        }

        private static float Read(Object target,string path)
        {
            using var so = new SerializedObject(target);
            var prop=so.FindProperty(path);
            return prop == null ? float.NaN : ReadValue(prop);
        }

        private static float ReadValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean: return property.boolValue ? 1 : 0;
                case SerializedPropertyType.Integer: return property.intValue;
                default: return property.floatValue;
            }
        }

        private static void Set(Object target,Field f,float value)
        {
            using var so = new SerializedObject(target);
            var prop=so.FindProperty(f.path);
            if (f.boolean) prop.boolValue = value != 0;
            else if (f.integer) prop.intValue = (int)value;
            else prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static string AssetId(Object obj) => obj && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out string guid,out long id)
            ? guid + "/" + id : "";
        private static string Pretty(float n)=>n.ToString("G7",CultureInfo.InvariantCulture);
        private static string Number(float n)=>n.ToString("R",CultureInfo.InvariantCulture);
        private string Stamp()
        {
            var assets = fields.Where(f=>f.writable).Select(f=>AssetDatabase.GetAssetPath(f.writable)).Distinct().OrderBy(p=>p,StringComparer.Ordinal)
                .Select(p=>p+"="+AssetDatabase.GetAssetDependencyHash(p));
            var values = fields.Select(f=>f.sourceId+"."+f.path+"="+Number(f.writable ? Read(f.writable,f.path) : f.baseline));
            return Hash128.Compute(binding+"|"+string.Join("|",assets)+"|"+string.Join("|",values)).ToString();
        }
        public string VersionFor(bool candidate)=> (candidate?"候选 ":"基线 ")+Hash128.Compute(revision+"|"+string.Join("|",fields.Select(f=>Number(candidate?f.pending:f.baseline)))).ToString().Substring(0,12);
        public bool TryGet(string id,out float baseline,out float effective,out float pending)
        {
            var f=fields.Find(p=>p.id==id); baseline=f?.baseline??0;effective=f?.effective??0;pending=f?.pending??0;return f!=null;
        }
        public bool TrySet(string id,float value,out string reason)
        {
            var f=fields.Find(p=>p.id==id);
            if(f==null){reason="此来源没有该参数："+id;return false;}
            if(float.IsNaN(value)||float.IsInfinity(value)||value<f.min||value>f.max||f.integer&&value!=Mathf.Floor(value))
            {reason=f.title+" 数值或范围无效";return false;}
            f.pending=value;reason=null;return true;
        }
    }
}
