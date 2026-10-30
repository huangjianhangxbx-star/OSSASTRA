/* 功能：本模块的中文检查、配置应用与独立验证场入口。
 * 边界：不重置角色或重建地图，不自动保存用户原场；运行时只重接本模块。
 * 协作：默认Inspector仍暴露全部配置，按钮调用明确校验/生命周期接口。
 */
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ossastra.SceneVisibility.Editor
{
    [CustomEditor(typeof(SceneVisibilityRoot))]
    public sealed class SceneVisibilityInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var root = (SceneVisibilityRoot)target;
            EditorGUILayout.Space();
            if (GUILayout.Button("检查目标、组与相机登记"))
            {
                bool valid = root.ValidateConfiguration(out string reason);
                if (valid) Debug.Log("模型可见性登记通过；实际遮挡和渲染仍需Play验证。", root);
                else Debug.LogError("模型可见性登记拒绝：" + reason, root);
            }
            if (Application.isPlaying)
            {
                if (GUILayout.Button("应用配置并重接本模块")) root.Reconnect();
                EditorGUILayout.HelpBox(root.Status + "\n" + root.RenderError, MessageType.Info);
                EditorGUILayout.LabelField("检测CPU / 捕获提交CPU（非GPU）", root.DetectionMilliseconds.ToString("F3") + " / " + root.RenderMilliseconds.ToString("F3") + " ms");
                EditorGUILayout.LabelField("组数 / 超预算", root.ActiveGroups + " / " + root.BudgetOverflow);
                Repaint();
            }
            EditorGUILayout.HelpBox("配置在每次启用时复制为一致的运行快照。修改SO后点应用；不会暂停或重置游戏。主控站台时保留整块承载顶盖，伙伴可能仍被其合法遮住。", MessageType.None);
        }

        [MenuItem("OSSASTRA/场景可见性/打开独立遮挡验证场")]
        static void OpenLab()
        {
            if (Application.isPlaying) { Debug.LogWarning("先退出Play再打开独立验证场。"); return; }
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.isDirty) { Debug.LogWarning("当前场景有未保存修改；请先自行处理，再打开独立验证场。"); return; }
            EditorSceneManager.OpenScene("Assets/OSSASTRA/GroundWorkshop/Samples/KT-VIS-01/SC_OcclusionVisibilityLab.unity");
        }
    }
}
#endif
