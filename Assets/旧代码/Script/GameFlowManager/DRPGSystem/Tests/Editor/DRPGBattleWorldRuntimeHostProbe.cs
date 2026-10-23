#if UNITY_EDITOR
using System;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DRPGBattleWorldRuntimeHostProbe
{
    public static string Run()
    {
        const string templateName = "DRPGWorldRuntimeHostProbe_4E91";
        string runtimeName = templateName + " (Runtime)";
        var hostObject = new GameObject("BattleWorldRuntimeHostProbe");
        var template = new GameObject(templateName);
        Scene previewScene = default;
        try
        {
            new GameObject(runtimeName);
            new GameObject(runtimeName);
            previewScene = EditorSceneManager.NewPreviewScene();
            var previewOrphan = new GameObject(runtimeName)
            {
                hideFlags = HideFlags.DontSaveInEditor |
                    HideFlags.DontSaveInBuild
            };
            SceneManager.MoveGameObjectToScene(previewOrphan, previewScene);

            BattleWorldRuntimeHost host =
                hostObject.AddComponent<BattleWorldRuntimeHost>();
            host.Configure(
                template,
                BattleWorldOutputMode.DirectCamera,
                null,
                null);
            host.EnsureWorld();

            AssertEqual(
                1,
                CountAllRuntimeRoots(runtimeName),
                "A runtime host must reclaim stale roots across loaded or orphaned scenes");
            AssertTrue(host.WorldInstance != null,
                "The surviving runtime world must be owned by the host");
            AssertEqual(runtimeName, host.WorldInstance.name,
                "The owned world must retain the deterministic runtime name");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(hostObject);
            UnityEngine.Object.DestroyImmediate(template);
            DestroyAllRuntimeRoots(runtimeName);
            if (previewScene.IsValid())
                EditorSceneManager.ClosePreviewScene(previewScene);
        }

        return "PASS battle world runtime host uniqueness";
    }

    private static int CountAllRuntimeRoots(string exactName)
    {
        GameObject[] roots = Resources.FindObjectsOfTypeAll<GameObject>();
        int count = 0;
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null &&
                roots[i].transform.parent == null &&
                roots[i].name == exactName)
            {
                count++;
            }
        }
        return count;
    }

    private static void DestroyAllRuntimeRoots(string exactName)
    {
        GameObject[] roots = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = roots.Length - 1; i >= 0; i--)
        {
            if (roots[i] != null &&
                roots[i].transform.parent == null &&
                roots[i].name == exactName)
            {
                UnityEngine.Object.DestroyImmediate(roots[i]);
            }
        }
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }
}
#endif
