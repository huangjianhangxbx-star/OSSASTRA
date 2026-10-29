/* 脚本：MapFlickerTraceRecorderEditor
 * 功能：提供研究追踪的Inspector显式按钮和可选本机导出目录。
 * 边界：按钮只在Play可用；不启动自动采样、不自动写盘、不修改角色配置。
 * 协作：调用Recorder公开端口；运行时程序集不引用UnityEditor。
 */
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Ossastra.MapResearchIntegration.Diagnostics
{
    [CustomEditor(typeof(MapFlickerTraceRecorder))]
    public sealed class MapFlickerTraceRecorderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var recorder = (MapFlickerTraceRecorder)target;
            EditorGUILayout.HelpBox("默认关闭。正式网格回调与相机阶段只记录到有界内存；它们不证明GPU无闪烁。导出仅由按钮触发。", MessageType.Info);
            EditorGUILayout.LabelField("状态", recorder.Status);
            EditorGUILayout.LabelField("样本 / 覆盖", recorder.SampleCount + " / " + recorder.DroppedSampleCount);
            EditorGUILayout.LabelField("正式Mesh事件 / 订阅身体", recorder.MeshEventCount + " / " + recorder.HookedBodyCount);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                using (new EditorGUI.DisabledScope(recorder.IsTracing)) if (GUILayout.Button("开始有界追踪")) recorder.StartFromButton();
                using (new EditorGUI.DisabledScope(!recorder.IsTracing)) if (GUILayout.Button("停止追踪并保留记录")) recorder.StopTracing();
                if (GUILayout.Button("仅标记当前异常（不写盘）")) recorder.MarkFromButton();
                if (GUILayout.Button("手动导出最近记录")) recorder.ExportFromButton();
                if (GUILayout.Button("标记异常并导出最近记录")) recorder.MarkAndExportFromButton();
                if (GUILayout.Button("选择本机文件夹并导出"))
                {
                    var chosen = EditorUtility.OpenFolderPanel("导出研究诊断JSON", recorder.exportDirectory, "");
                    if (!string.IsNullOrEmpty(chosen)) recorder.ExportReport(chosen);
                }
            }
            if (!string.IsNullOrEmpty(recorder.LastError)) EditorGUILayout.HelpBox(recorder.LastError, MessageType.Warning);
            if (!string.IsNullOrEmpty(recorder.LastExportPath)) EditorGUILayout.SelectableLabel(recorder.LastExportPath, EditorStyles.textField, GUILayout.Height(38));
        }
    }
}
#endif
