/* 功能：明确材质绑定与Play期A/B/C作者入口；不创建Canvas，不在Edit预览或保存临时材质。
 * 边界：场景/作者资产仍由用户明确保存；按钮只调用比较组件公开接口。
 */
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Ossastra.WorldRendering.VoxelSurface
{
    [CustomEditor(typeof(VoxelSurfaceComparison))]
    public sealed class VoxelSurfaceComparisonEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bindings"), new GUIContent("明确参与比较的Renderer"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("calibratedProfile"), new GUIContent("B 校准PBR配置"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("shapedProfile"), new GUIContent("C 轻分面配置"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("mode"), new GUIContent("启动比较档"));
            serializedObject.ApplyModifiedProperties();
            var comparison = (VoxelSurfaceComparison)target;
            EditorGUILayout.HelpBox("作者材质只读；每个Renderer须登记完整且等长的A/B/C材质槽。编辑时只保存设置；进入Play自动应用，运行时改配置后点刷新。", MessageType.Info);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || !comparison.isActiveAndEnabled))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("A 原版")) comparison.ApplyMode(VoxelSurfaceComparisonMode.Original);
                if (GUILayout.Button("B 校准PBR")) comparison.ApplyMode(VoxelSurfaceComparisonMode.Calibrated);
                if (GUILayout.Button("C 轻分面")) comparison.ApplyMode(VoxelSurfaceComparisonMode.Faceted);
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("刷新当前档参数")) comparison.RefreshParameters();
            }
            if (GUILayout.Button("归还原材质并释放私有缓存")) comparison.RestoreOriginal();
            EditorGUILayout.HelpBox(comparison.Status, MessageType.None);
        }
    }
}
#endif
