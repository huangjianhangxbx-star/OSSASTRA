/* 脚本：WorldPropAuthoring
 * 功能：明确为作者选中的新场景副本分配稳定ID并验证登记；运行时永不偷偷修复重复身份。
 * 边界：不改模板Prefab/SO耐久状态，不重置已有档案；ID变更只接受作者主动点击。
 */
#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Ossastra.WorldProps.Editor
{
    [CustomEditor(typeof(WorldPropRoot))]
    public sealed class WorldPropInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var prop = (WorldPropRoot)target;
            EditorGUILayout.HelpBox("实例ID属于持久世界身份。只为尚未进入存档的新副本分配新ID；运行态Health/耗尽从共同存档恢复。", MessageType.Info);
            GUI.enabled = !Application.isPlaying && !EditorUtility.IsPersistent(prop);
            if (GUILayout.Button("为选中新副本分配新实例ID"))
            {
                Undo.RecordObject(prop, "Assign new prop identity");
                prop.instanceId = Guid.NewGuid().ToString("N");
                EditorUtility.SetDirty(prop);
                EditorSceneManager.MarkSceneDirty(prop.gameObject.scene);
            }

            GUI.enabled = true;
            if (Application.isPlaying && prop.State != null)
                EditorGUILayout.LabelField("真实状态", prop.State.Phase + " / " + prop.Health.Current + " / " + prop.Health.Maximum);
        }
    }

    public static class WorldPropAuthoring
    {
        [MenuItem("XingHai/World Props/登记当前场景物")]
        public static void RegisterCurrent()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("仅Edit作者登记");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var root = roots.SelectMany(g => g.GetComponentsInChildren<WorldPropsRoot>(true)).Single();
            var props = roots.SelectMany(g => g.GetComponentsInChildren<WorldPropRoot>(true)).ToArray();
            if (props.Any(p => string.IsNullOrWhiteSpace(p.instanceId) || p.definition == null) || props.Select(p => p.instanceId).Distinct().Count() != props.Length)
                throw new InvalidOperationException("实例ID缺失/重复，未自动分配；请明确处理新副本");
            Undo.RecordObject(root, "Register world props");
            root.props = props;
            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
#endif
