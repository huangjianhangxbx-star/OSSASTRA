/*
 * 脚本：CharacterFollowEditor
 * 功能：在角色Inspector中显示Follow真实只读运行状态，参数仍由原序列化字段配置。
 * 边界：仅编辑器显示；不采样/重置策略，不在角色上新增可写状态镜像。
 * 协作：读取本体Observe，普通游戏运行不依赖本编辑器脚本。
 */
using Ossastra.Characters;
using UnityEditor;
using UnityEngine;

namespace Ossastra.Development.Editor
{
    [CustomEditor(typeof(CharacterFollow))]
    public sealed class CharacterFollowEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (!Application.isPlaying) return;
            var follow=target as CharacterFollow;
            if (follow == null) return;
            var observation=follow.Observe();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("本体运行状态（只读）",EditorStyles.boldLabel);
            EditorGUILayout.LabelField("状态",observation.State.ToString());
            EditorGUILayout.LabelField("目标",observation.TargetId);
            EditorGUILayout.LabelField("距离",observation.Distance.ToString("F2"));
            EditorGUILayout.LabelField("原因",observation.Reason);
        }
        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
}
