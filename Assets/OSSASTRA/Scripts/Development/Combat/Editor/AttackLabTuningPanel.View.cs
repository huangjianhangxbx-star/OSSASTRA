/*
 * 脚本：AttackLabTuningPanel.View
 * 功能：显示手感试验的生效、候选和作者来源，将变更交给统一复位与写回。
 * 边界：Inspector 起点来自战斗状态机实际已冻结的连段来源。
 */
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ossastra.Development.Editor
{
    // 编辑器显示来源、生效快照与待应用值。应用交给会话执行统一复位。
    public sealed partial class AttackLabTuningPanel
    {
        public void Draw()
        {
            if (!actor || fields.Count==0) {EditorGUILayout.HelpBox("进入测试场的 Play 模式后读取当前实控角色与武器。",MessageType.Info);return;}
            EditorGUILayout.LabelField("本次实验",EditorStyles.boldLabel);
            var combat = actor.StateArbitrator?.Combat;
            var comboSource = combat?.ComboSource;
            EditorGUILayout.LabelField("普通攻击", profile ? profile.displayName :
                (equipment?.RightDefinition ? equipment.RightDefinition.name : actor.Definition?.name ?? "场内默认"));
            if (comboSource && combat.EffectiveComboSettings.HasValue)
            {
                var settings = combat.EffectiveComboSettings.Value;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("生效连段来源", comboSource.name);
                    if (GUILayout.Button("定位", GUILayout.Width(52)))
                    {
                        Selection.activeObject = comboSource;
                        EditorGUIUtility.PingObject(comboSource);
                    }
                }
                EditorGUILayout.LabelField($"保留 {Pretty(settings.retentionSeconds)} 秒  ·  记忆 {Pretty(settings.continuationSeconds)} 秒  ·  " +
                    (settings.requireHit ? "需命中" : "可空挥"), EditorStyles.wordWrappedMiniLabel);
                if (!fields.Any(f => f.id == "basic.buffer"))
                    EditorGUILayout.HelpBox("此来源仍使用兼容参数。可在来源 Inspector 中保存升级。", MessageType.Info);
            }
            var fallback = actor.GetComponent<Ossastra.Characters.CharacterMainAction>()?.ComboFallbackReason;
            if (!string.IsNullOrEmpty(fallback))
                EditorGUILayout.HelpBox("主手动作组已回退：" + fallback, MessageType.Warning);
            EditorGUILayout.LabelField("代表技能", skill ? skill.displayName : "当前主手没有已装配技能");
            section=GUILayout.Toolbar(section,new[]{"连段","分段动作","技能","命中反馈"});
            showSource=EditorGUILayout.Foldout(showSource,"查看参数来源和单位",true);
            var selected=fields.Where(f=>SectionOf(f)==section).ToArray();
            if(selected.Length==0)EditorGUILayout.HelpBox("当前角色没有接入这一类可调来源。",MessageType.None);
            if (section == 1)
            {
                foreach (var note in timingSourceNotes)
                    EditorGUILayout.LabelField(note, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField("定时动作的总时长参与动画速度计算。修改在下一次受理时生效。", EditorStyles.wordWrappedMiniLabel);
            }
            using(new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("参数",GUILayout.MinWidth(90));
                GUILayout.Label("来源",GUILayout.Width(56));GUILayout.Label("生效",GUILayout.Width(56));GUILayout.Label("候选",GUILayout.Width(70));
            }
            foreach(var f in selected)
            {
                using(new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent(f.title,f.Location),GUILayout.MinWidth(90));
                    GUILayout.Label(Pretty(f.baseline),GUILayout.Width(56));GUILayout.Label(Pretty(f.effective),GUILayout.Width(56));
                    EditorGUI.BeginChangeCheck();
                    float next;
                    if (f.boolean) next = EditorGUILayout.Toggle(f.pending != 0, GUILayout.Width(70)) ? 1 : 0;
                    else if (f.integer) next = EditorGUILayout.IntField((int)f.pending, GUILayout.Width(70));
                    else next = EditorGUILayout.FloatField(f.pending, GUILayout.Width(70));
                    if(EditorGUI.EndChangeCheck())f.pending=next;
                }
                if(showSource)EditorGUILayout.LabelField(f.Location,EditorStyles.wordWrappedMiniLabel);
            }
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("用基线复位")) RequestReset?.Invoke(false);
                if(GUILayout.Button("应用候选并复位"))
                {
                    if(CanApply(true,out var reason))RequestReset?.Invoke(true);
                    else message=reason;
                }
            }
            var changes=fields.Where(f=>f.Changed).ToArray();
            int pending=fields.Count(f=>f.pending!=f.effective);
            EditorGUILayout.LabelField(pending==0?"当前实测值与面板候选一致":$"{pending} 项候选等待应用",EditorStyles.miniLabel);
            if(!string.IsNullOrEmpty(message))EditorGUILayout.HelpBox(message,MessageType.Info);
            showSaving=EditorGUILayout.Foldout(showSaving,"实验预设与原配置写回",true);
            if(!showSaving)return;
            using (new EditorGUI.DisabledScope(actor.ActionActive || abilities && abilities.IsBusy))
            {
                if (GUILayout.Button("重新读取来源为基线…") && EditorUtility.DisplayDialog("重新读取基线",
                    "恢复运行组件并以当前来源重新开始。尚未另存的候选值将清除。","重新读取","取消"))
                {
                    var target = actor;
                    Dispose();
                    Bind(target);
                    message = "已重新读取角色与装备的当前配置。";
                    RequestReset?.Invoke(false);
                    return;
                }
            }
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("另存实验预设"))
                {
                    string path=EditorUtility.SaveFilePanelInProject("另存攻击实验","攻击实验","asset","选择未占用的位置。");
                    if(path.Length>0)message=SavePreset(path,out var reason)?"预设已保存":reason;
                }
                if(GUILayout.Button("读取实验预设"))
                {
                    string path=EditorUtility.OpenFilePanel("读取攻击实验",Application.dataPath,"asset");
                    if(path.Length>0)message=LoadPreset(FileUtil.GetProjectRelativePath(path),out var reason)?"已读取情境与候选，应用后生效":reason;
                }
            }
            if(changes.Length==0){EditorGUILayout.LabelField("候选与原配置相同。",EditorStyles.miniLabel);return;}
            foreach(var f in changes)
            {
                using(new EditorGUI.DisabledScope(!f.writable))
                    f.selected=EditorGUILayout.ToggleLeft($"{f.title}   {Pretty(f.baseline)} → {Pretty(f.pending)}",f.selected);
                EditorGUILayout.LabelField(f.Location+(f.writable ? " · 所有引用此配置的装配共用" : " · 仅实验预设"),EditorStyles.wordWrappedMiniLabel);
            }
            var chosen=changes.Where(f=>f.selected&&f.writable).ToArray();
            using(new EditorGUI.DisabledScope(chosen.Length==0))
                if(GUILayout.Button("写回已勾选的原配置…")&&EditorUtility.DisplayDialog("写回原配置",
                    string.Join("\n",chosen.Select(f=>f.Location+"  "+Pretty(f.baseline)+" → "+Pretty(f.pending))),"写回所选","取消"))
                    message=WriteSelected(chosen.Select(f=>f.id).ToArray(),out var reason)?"所选字段已写回":reason;
        }
        private static int SectionOf(Field f)
        {
            if(f.id.StartsWith("skill.",StringComparison.Ordinal))return 2;
            if(f.id.StartsWith("camera.",StringComparison.Ordinal))return 3;
            if(f.id=="basic.radius" || f.id=="basic.reach" || f.id=="basic.contact" ||
                f.id.StartsWith("basic.",StringComparison.Ordinal)&&f.id.Length>6&&char.IsDigit(f.id[6]))return 1;
            return 0;
        }
    }
}
