/* 功能：显式材质绑定、C0/候选分离与会话调参作者入口；不创建Canvas，不保存临时材质。
 * 边界：滑条只改会话；保存候选与绑定组默认分开，通过Editor窄桥处理。
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
            EditorGUILayout.PropertyField(serializedObject.FindProperty("shapedBaselineProfile"), new GUIContent("C0 原C独立基线"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("saveCandidateProfile"), new GUIContent("显式保存候选目标"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lightContextCamera"), new GUIContent("灯光身份相机"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("keyLight"), new GUIContent("主方向光身份"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("directionalFill"), new GUIContent("可调方向补光"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("mode"), new GUIContent("启动比较档"));
            serializedObject.ApplyModifiedProperties();
            var comparison = (VoxelSurfaceComparison)target;
            EditorGUILayout.HelpBox("作者材质与C0只读；C滑条为独立会话。保存候选不改变默认；显式设置默认仅影响本绑定组，Play时待退出后固化。", MessageType.Info);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || !comparison.isActiveAndEnabled))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("A 原版")) comparison.ApplyMode(VoxelSurfaceComparisonMode.Original);
                if (GUILayout.Button("B 校准PBR")) comparison.ApplyMode(VoxelSurfaceComparisonMode.Calibrated);
                if (GUILayout.Button("C 轻分面")) comparison.ApplyMode(VoxelSurfaceComparisonMode.Faceted);
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("刷新当前档参数")) comparison.RefreshParameters();
            }
            var settings = comparison.SessionSettings;
            using (new EditorGUI.DisabledScope(comparison.mode != VoxelSurfaceComparisonMode.Faceted || settings == null))
            {
                if (settings != null)
                {
                    EditorGUI.BeginChangeCheck();
                    float lit = EditorGUILayout.Slider("亮面增强 %", settings.litFaceBoost * 100, 0, 50);
                    float back = EditorGUILayout.Slider("背光压暗 %", settings.backFaceDarkening * 100, 0, 50);
                    if (EditorGUI.EndChangeCheck()) { comparison.SetSessionAdjustments(Mathf.Round(lit) * .01f, Mathf.Round(back) * .01f); if (!Application.isPlaying) comparison.FlushPendingParameters(); }
                    if (GUILayout.Button("复位原C0")) { comparison.ResetSessionToC0(); if (!Application.isPlaying) comparison.FlushPendingParameters(); }
                }
            }
            if (comparison.mode != VoxelSurfaceComparisonMode.Faceted) EditorGUILayout.LabelField("A/B：C参数暂不应用");
            using (new EditorGUI.DisabledScope(settings == null))
            {
                if (GUILayout.Button("保存独立候选Profile")) comparison.RequestSaveCandidate();
                if (GUILayout.Button("设为本绑定组默认")) comparison.RequestSetBindingDefault();
            }
            EditorGUILayout.HelpBox(comparison.SaveStatus, MessageType.None);
            if (GUILayout.Button("归还原材质并释放私有缓存")) comparison.RestoreOriginal();
            EditorGUILayout.HelpBox(comparison.Status, MessageType.None);
        }
    }
}
#endif
