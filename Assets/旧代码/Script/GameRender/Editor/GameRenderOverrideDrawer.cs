#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Match the shader's >= 0.5 gate, including old negative Float values.
public sealed class GameRenderOverrideDrawer : MaterialPropertyDrawer
{
    public override void OnGUI(Rect position, MaterialProperty prop, string label, MaterialEditor editor)
    {
        bool previousMixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = prop.hasMixedValue;
        EditorGUI.BeginChangeCheck();
        bool enabled = EditorGUI.Toggle(position,
            new GUIContent(label, "勾选：使用本材质下方数值；取消：继承相机的全局角色参数。"),
            prop.floatValue >= 0.5f);
        if (EditorGUI.EndChangeCheck())
        {
            editor.RegisterPropertyChangeUndo(label);
            prop.floatValue = enabled ? 1f : 0f;
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
        EditorGUI.showMixedValue = previousMixed;
    }
}
#endif
