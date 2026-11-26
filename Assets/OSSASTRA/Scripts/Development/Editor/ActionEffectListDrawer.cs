/*
 * 功能：为动作和技能的托管引用效果列表提供同一套 Inspector 控件与实际片段事件选择。
 * Unity SerializedProperty 记录修改和撤销，复制时重新分配词条身份，战斗只读正式接受快照。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.CharacterAnimation;
using Ossastra.CharacterAnimation.Editor;
using Ossastra.Characters;
using UnityEditor;
using UnityEngine;
using RuntimeAnimationConfig = Ossastra.CharacterAnimation.CharacterAnimationConfigManager;

namespace Ossastra.Development.Editor
{
    [CustomPropertyDrawer(typeof(ActionEffectList))]
    public sealed class ActionEffectListDrawer : PropertyDrawer
    {
        private static readonly Type[] Types = { typeof(SelfDisplacementEffect), typeof(InputMovementScaleEffect),
            typeof(TargetKnockbackEffect), typeof(SelfRecoilEffect), typeof(LaunchEffect) };
        private static readonly string[] Names = { "自身主动位移", "输入微移", "目标推移", "自身反冲", "腾空／挑飞" };
        private static readonly Dictionary<string,string> Labels = new Dictionary<string,string>
        {
            {"trigger","触发阶段"},{"frequency","触发频率"},{"spineEvent","Spine 事件原名"},{"phaseClip","进入片段（可选）"},{"emissionIndex","释放序号（-1 全部）"},
            {"channel","释放通道（空为全部）"},{"onGuard","格挡也触发"},{"interval","触发间隔（秒）"},{"maxTriggers","次数上限（0 无限）"},
            {"ground","地空条件"},{"distance","请求距离（U）"},{"duration","持续时间（秒）"},{"curve","归一化位移曲线"},
            {"direction","位移方向"},{"customDirection","世界 XY 方向"},{"replaceInput","覆盖输入位移"},{"scale","输入移动倍率"},
            {"allowed","允许输入方向"},{"fullBody","维持全身攻击动画"},{"endAtMoveReady","到可移动事件结束"},
            {"awayFromAttacker","从攻击者向外推开"},{"velocity","起始速度（U/s）"},{"target","作用于受击目标"}
        };
        private const float Row = 21;
        private static string Title(ActionEffectEntry effect)
        {
            int index = effect == null ? -1 : Array.IndexOf(Types, effect.GetType());
            return index < 0 ? "缺失效果类型" : Names[index];
        }

        // 只展开本类型的可编辑字段。序列化身份由新增和复制操作维护。
        private static IEnumerable<SerializedProperty> Fields(SerializedProperty entry)
        {
            if (!(entry.managedReferenceValue is ActionEffectEntry effect)) yield break;
            var child = entry.Copy();
            var end = entry.GetEndProperty();
            if (!child.NextVisible(true)) yield break;
            do
            {
                if (SerializedProperty.EqualContents(child, end) || child.depth <= entry.depth) yield break;
                switch (child.name)
                {
                    case "id": case "enabled": continue;
                    case "channel": case "emissionIndex":
                        if (effect.trigger != ActionEffectTrigger.Released && effect.trigger != ActionEffectTrigger.Contact) continue;
                        break;
                    case "onGuard": if (effect.trigger != ActionEffectTrigger.Contact) continue; break;
                    case "spineEvent": if (effect.trigger != ActionEffectTrigger.SpineEvent) continue; break;
                    case "phaseClip": if (effect.trigger != ActionEffectTrigger.PhaseChanged) continue; break;
                    case "interval": if (effect.frequency != ActionEffectFrequency.Interval) continue; break;
                    case "customDirection": if (effect is SelfDisplacementEffect step && step.direction != ActionImpactDirection.Custom) continue; break;
                }
                yield return child.Copy();
            } while (child.NextVisible(false));
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => Height(property);

        private static float Height(SerializedProperty property)
        {
            if (!property.isExpanded) return Row;
            var list = property.FindPropertyRelative("entries");
            float height = Row * (3 + list.arraySize);
            for (int i = 0; i < list.arraySize; i++)
            {
                var entry = list.GetArrayElementAtIndex(i);
                if (!entry.isExpanded) continue;
                foreach (var field in Fields(entry)) height += EditorGUI.GetPropertyHeight(field, true) + 3;
            }
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            => Draw(position, property, null);

        internal static void DrawForMove(SerializedObject so, Action changed)
        {
            var property = so.FindProperty("effects");
            var rect = EditorGUILayout.GetControlRect(false, Height(property));
            Draw(rect, property, changed);
            if (so.ApplyModifiedProperties()) changed();
        }

        private static void Draw(Rect position, SerializedProperty property, Action changed)
        {
            var r = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(r, property.isExpanded, "招式效果", true);
            if (!property.isExpanded) return;
            r.y += Row;
            EditorGUI.PropertyField(r, property.FindPropertyRelative("bypass"), new GUIContent("旁路全部招式效果"));
            var list = property.FindPropertyRelative("entries");
            for (int i = 0; i < list.arraySize; i++)
            {
                r.y += Row;
                var entry = list.GetArrayElementAtIndex(i);
                var effect = entry.managedReferenceValue as ActionEffectEntry;
                var on = entry.FindPropertyRelative("enabled");
                if (on != null) on.boolValue = EditorGUI.Toggle(new Rect(r.x, r.y, 18, r.height), on.boolValue);
                var header = new Rect(r.x + 30, r.y, Mathf.Max(12, r.width - 155), r.height);
                string error = effect?.ValidationError();
                var title = new GUIContent(Title(effect) + (error == null ? "" : " ⚠"),error ?? "");
                entry.isExpanded = EditorGUI.Foldout(header, entry.isExpanded, title, true);
                float x = r.xMax - 118;
                using (new EditorGUI.DisabledScope(i == 0))
                    if (GUI.Button(new Rect(x, r.y, 23, r.height), "↑")) { list.MoveArrayElement(i, i - 1); break; }
                using (new EditorGUI.DisabledScope(i + 1 >= list.arraySize))
                    if (GUI.Button(new Rect(x + 23, r.y, 23, r.height), "↓")) { list.MoveArrayElement(i, i + 1); break; }
                using (new EditorGUI.DisabledScope(effect == null))
                    if (GUI.Button(new Rect(x + 46, r.y, 36, r.height), "复制"))
                    {
                        var copy = (ActionEffectEntry)JsonUtility.FromJson(JsonUtility.ToJson(effect), effect.GetType());
                        copy.id = Guid.NewGuid().ToString("N");
                        int tail = list.arraySize;
                        list.arraySize++;
                        list.GetArrayElementAtIndex(tail).managedReferenceValue = copy;
                        if (tail != i + 1) list.MoveArrayElement(tail, i + 1);
                        break;
                    }
                if (GUI.Button(new Rect(x + 82, r.y, 36, r.height), "删除")) { list.DeleteArrayElementAtIndex(i); break; }
                if (!entry.isExpanded) continue;
                foreach (var field in Fields(entry))
                {
                    r.y += r.height + 3;
                    r.height = EditorGUI.GetPropertyHeight(field, true);
                    var caption = new GUIContent(Labels.TryGetValue(field.name,out var cn) ? cn : field.displayName, field.tooltip);
                    var fieldRect = new Rect(r.x + 16, r.y, r.width - 16, r.height);
                    if (field.name == "spineEvent" || field.name == "phaseClip") DrawAnimationChoice(fieldRect, entry, field);
                    else EditorGUI.PropertyField(fieldRect, field, caption, true);
                }
                r.y += r.height - EditorGUIUtility.singleLineHeight;
                r.height = EditorGUIUtility.singleLineHeight;
            }
            r.y += Row;
            if (!GUI.Button(r, "添加招式效果…")) return;
            var menu = new GenericMenu();
            var owner = property.serializedObject.targetObject;
            string path = list.propertyPath;
            for (int i = 0; i < Types.Length; i++)
            {
                var type = Types[i];
                menu.AddItem(new GUIContent(Names[i]), false, () => AddEntry(owner, path, type, changed));
            }
            menu.ShowAsContext();
        }

        private static void DrawAnimationChoice(Rect rect, SerializedProperty entry, SerializedProperty field)
        {
            bool phase = field.name == "phaseClip";
            var prefix = phase ? "进入片段" : "Spine 事件";
            var current = field.stringValue;
            var clip = entry.FindPropertyRelative("spineEventClip")?.stringValue;
            int occurrence = entry.FindPropertyRelative("spineEventOccurrence")?.intValue ?? -1;
            string scope = phase || string.IsNullOrEmpty(clip) ? "" :
                clip + " · " + (occurrence < 0 ? "全部 " : "第 " + (occurrence + 1) + " 次 ");
            string display = string.IsNullOrEmpty(current) ? phase ? "任何阶段" : "选择源事件…" : scope + current;
            if (!EditorGUI.DropdownButton(EditorGUI.PrefixLabel(rect, new GUIContent(prefix)),
                    new GUIContent(display), FocusType.Keyboard)) return;

            var menu = new GenericMenu();
            var asset = field.serializedObject.targetObject;
            string entryPath = entry.propertyPath;
            void Select(string value, string sourceClip, int sourceOccurrence)
            {
                if (!asset) return;
                using var so = new SerializedObject(asset);
                var destination = so.FindProperty(entryPath);
                if (destination == null) return;
                destination.FindPropertyRelative(phase ? "phaseClip" : "spineEvent").stringValue = value;
                if (!phase)
                {
                    destination.FindPropertyRelative("spineEventClip").stringValue = sourceClip;
                    destination.FindPropertyRelative("spineEventOccurrence").intValue = sourceOccurrence;
                }
                so.ApplyModifiedProperties();
            }
            if (phase) menu.AddItem(new GUIContent("任何阶段"), string.IsNullOrEmpty(current), () => Select("", "", -1));
            var ability = asset as AbilityDefinitionSO;
            var stages = ability?.executor as SustainedAttackAbilitySO;
            var owners = new[] { asset as ActionDefinitionSO ?? ability?.action, stages?.loopAction, stages?.endAction };
            var logics = owners.Where(a => a).Select(a => a.animationLogicId).ToHashSet();
            var sources = UnityEngine.Object.FindObjectsOfType<RuntimeAnimationConfig>(true);
            bool found = false;
            foreach (var owner in sources)
            {
                var actor = owner.GetComponentInParent<CharacterRoot>();
                if (!actor) continue;
                foreach (var match in AnimationAuthoredClips.ForCharacter(owner, actor.GetComponent<CharacterEquipment>()))
                {
                    if (!logics.Contains(match.Logic) || match.Clip == null || phase && match.Clip.track != 0) continue;
                    var name = match.Clip.animaName;
                    string context = actor.name + "/" + match.Source.name + "/" + name.Replace('/', '／');
                    if (phase)
                    {
                        menu.AddItem(new GUIContent(context), current == name, () => Select(name, "", -1));
                        found = true;
                        continue;
                    }
                    var animation = match.Clip.skeletonDataAsset?.GetSkeletonData(true)?.FindAnimation(name);
                    foreach (var group in AnimationCalibrationSO.Events(animation).GroupBy(e => e.Data.Name))
                    {
                        string eventName = group.Key;
                        menu.AddItem(new GUIContent(context + "/" + eventName + "（全部同名）"),
                            current == eventName && clip == name && occurrence < 0, () => Select(eventName, name, -1));
                        int index = 0;
                        foreach (var source in group)
                        {
                            int eventIndex = index++;
                            menu.AddItem(new GUIContent(context + "/" + eventName + " #" + (eventIndex + 1) + $" · {source.Time:F3}s"),
                                current == eventName && clip == name && occurrence == eventIndex, () => Select(eventName, name, eventIndex));
                        }
                        found = true;
                    }
                }
            }
            if (!found) menu.AddDisabledItem(new GUIContent("当前场景未找到此动作的 Mapping 片段"));
            menu.ShowAsContext();
        }

        internal static void AddEntry(UnityEngine.Object owner, string listPath, Type type, Action changed)
        {
            if (!owner) return;
            using var so = new SerializedObject(owner);
            var entries = so.FindProperty(listPath);
            int index = entries.arraySize++;
            entries.GetArrayElementAtIndex(index).managedReferenceValue = (ActionEffectEntry)Activator.CreateInstance(type);
            if (so.ApplyModifiedProperties()) changed?.Invoke();
        }
    }
}
