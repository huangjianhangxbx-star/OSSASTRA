using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ossastra.OSPhysics.Editor
{
    public static class MatterLabScene
    {
        [MenuItem("OSSASTRA/实验室/打开动态物质实验室")]
        private static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("动态物质实验室", "请在编辑模式中打开实验场。", "确定");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(MatterLabSceneChecks.ScenePath);
        }

        public static string CreateSample()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("The authoring operation requires Edit Mode.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MatterLabSceneChecks.ScenePath) != null)
                return MatterLabSceneChecks.VerifyAsset();

            var catalog = AssetDatabase.LoadAssetAtPath<MatterCatalogAsset>(MatterLabSceneChecks.CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MatterCatalogAsset>();
                catalog.PopulateSample();
                if (catalog.BuildSettings().Count != 6)
                    throw new InvalidOperationException("Expected six independent sample matter definitions.");
                AssetDatabase.CreateAsset(catalog, MatterLabSceneChecks.CatalogPath);
                AssetDatabase.SaveAssetIfDirty(catalog);
            }

            var activeScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var cameraRoot = new GameObject("实验镜头");
                cameraRoot.tag = "MainCamera";
                cameraRoot.transform.position = new Vector3(0, 0, -10);
                var camera = cameraRoot.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 6.10f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.018f, .026f, .043f);
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 48f;

                var root = new GameObject("动态物质实验室");
                root.AddComponent<MatterLab>().Configure(camera, catalog);
                MatterLabSceneChecks.VerifyInstance(scene);
                if (!EditorSceneManager.SaveScene(scene, MatterLabSceneChecks.ScenePath))
                    throw new InvalidOperationException("Could not save the isolated lab scene.");
            }
            finally
            {
                if (activeScene.IsValid() && activeScene.isLoaded)
                    SceneManager.SetActiveScene(activeScene);
                EditorSceneManager.CloseScene(scene, true);
            }
            return MatterLabSceneChecks.VerifyAsset();
        }
    }
}
