#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

[CustomEditor(typeof(GameRenderManager))]
internal sealed class GameRenderManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("新增世界风格位于受光结果与原有后处理之间；颗粒位于后处理之后。原 Volume 和 Pixelate 不由此面板改写。", MessageType.Info);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("profile"), new GUIContent("全局预设"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("sceneVolume"), new GUIContent("现有场景 Volume"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("enableNewStyle"), new GUIContent("启用新增风格"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("previewInSceneView"), new GUIContent("在 Scene 视图预览"));
        bool managerChanged = serializedObject.ApplyModifiedProperties();

        var manager = (GameRenderManager)target;
        if (managerChanged) manager.NotifyPreviewChanged();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("原效果对比")) SetEnabled(manager, false);
            if (GUILayout.Button("启用新增效果")) SetEnabled(manager, true);
        }
        if (manager.Profile != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("未保存的预览参数", EditorStyles.boldLabel);
            var preview = new SerializedObject(manager.Profile);
            preview.Update();
            EditorGUI.BeginChangeCheck();
            DrawPropertiesExcluding(preview, "m_Script", "debugClassification");
            bool previewChanged = EditorGUI.EndChangeCheck();
            bool applied = preview.ApplyModifiedPropertiesWithoutUndo();
            if (previewChanged || applied) manager.NotifyPreviewChanged();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("恢复预设")) manager.ResetPreview();
                if (GUILayout.Button("显式保存到预设")) SavePreviewToProfile(manager);
            }
            EditorGUILayout.HelpBox("修改这里仅影响当前编辑/运行预览。保存按钮才写入预设资产；恢复预设会丢弃未保存的预览值。", MessageType.None);
            if (GUILayout.Button("定位原预设资产")) Selection.activeObject = manager.SourceProfile;
        }
        if (manager.SceneVolume != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("现有后处理 Volume", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("临时副本不会改写原 Volume 资产或场景引用。这里只复用 Bloom、调色、曝光等现有配置，不再造第二套同类后处理。", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("开始 Volume 临时预览")) manager.BeginVolumePreview();
                if (GUILayout.Button("恢复原 Volume")) manager.RestoreVolumeBaseline();
            }
            if (manager.VolumePreview != null)
            {
                EditorGUILayout.ObjectField("临时配置", manager.VolumePreview, typeof(VolumeProfile), false);
                if (GUILayout.Button("显式保存 Volume 调参")) SaveVolumePreviewToSource(manager);
            }
        }
    }

    private static void SetEnabled(GameRenderManager manager, bool value)
    {
        Undo.RecordObject(manager, "Toggle GameRender preview");
        manager.SetPreviewEnabled(value);
        EditorUtility.SetDirty(manager);
        if (manager.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    internal static void SavePreviewToProfile(GameRenderManager manager)
    {
        var source = manager.SourceProfile;
        var preview = manager.Profile;
        if (source == null || preview == null) return;
        Undo.RecordObject(source, "Save GameRender preview to global profile");
        string originalName = source.name;
        EditorUtility.CopySerialized(preview, source);
        source.name = originalName;
        EditorUtility.SetDirty(source);
        if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(source)))
            AssetDatabase.SaveAssetIfDirty(source);
    }

    internal static void SaveVolumePreviewToSource(GameRenderManager manager)
    {
        var source = manager.SceneVolume != null ? manager.SceneVolume.sharedProfile : null;
        var preview = manager.VolumePreview;
        if (source == null || preview == null || source.components.Count != preview.components.Count) return;
        for (int i = 0; i < source.components.Count; i++)
        {
            var destination = source.components[i];
            var edited = preview.components[i];
            if (destination == null || edited == null || destination.GetType() != edited.GetType())
                throw new System.InvalidOperationException("Volume component layout changed during preview");
            if (EditorUtility.IsPersistent(destination))
                Undo.RecordObject(destination, "Save GameRender Volume preview");
            string originalName = destination.name;
            EditorUtility.CopySerialized(edited, destination);
            destination.name = originalName;
            EditorUtility.SetDirty(destination);
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(destination)))
                AssetDatabase.SaveAssetIfDirty(destination);
        }
    }
}
#endif
