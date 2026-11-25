using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ossastra.OSPhysics.Editor
{
    public static class MatterLabSceneChecks
    {
        public const string Root = "Assets/OSSASTRA/OSPhysics/";
        public const string ScenePath = Root + "Scenes/SC_DynamicMatterLab.unity";
        public const string CatalogPath = Root + "Config/MatterCatalog_SAMPLE.asset";

        public static string VerifyAsset()
        {
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null, "Experiment scene asset missing.");
            var leaks = AssetDatabase.GetDependencies(ScenePath, true)
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal) &&
                               !path.StartsWith(Root, StringComparison.Ordinal))
                .ToArray();
            Require(leaks.Length == 0, "The lab depends on project assets outside its folder: " + string.Join(", ", leaks));
            Require(!EditorBuildSettings.scenes.Any(scene => scene.path == ScenePath),
                "The lab must be an explicit Editor entry.");
            Require(AssetDatabase.LoadMainAssetAtPath(CatalogPath) != null,
                "The lab's own sample matter catalog is missing.");
            return "Independent scene, catalog and build entry verified.";
        }

        public static string VerifyInstance()
        {
            var scene = SceneManager.GetActiveScene();
            Require(scene.path == ScenePath, "Open the dynamic matter lab as the active scene.");
            return VerifyInstance(scene);
        }

        internal static string VerifyInstance(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var components = roots.SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            Require(!components.Any(component => component == null), "Lab contains an unresolved script.");
            Require(components.Count(component => component.GetType().FullName == "Ossastra.OSPhysics.MatterLab") == 1,
                "Lab controller missing or duplicated.");
            Require(!components.Any(component => !component.GetType().Namespace.StartsWith("Ossastra.OSPhysics", StringComparison.Ordinal)),
                "A foreign component was attached to the lab.");
            var camera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).SingleOrDefault();
            Require(camera != null && camera.orthographic, "Expected one orthographic experiment camera.");
            return "Isolated instance with one lab controller and one orthographic camera.";
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
