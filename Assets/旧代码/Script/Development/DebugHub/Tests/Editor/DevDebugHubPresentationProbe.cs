#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class DevDebugHubPresentationProbe
{
    public static string Run()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            DevDebugHubPrefabProbe.PrefabPath);
        Assert(prefab != null, "Debug hub prefab is required");
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        instance.hideFlags = HideFlags.HideAndDontSave;
        instance.SetActive(false);
        IDisposable registration = null;
        try
        {
            var registry = new DevDebugRegistry();
            registration = registry.Register(new DelegateDevDebugAction(
                "probe.presentation",
                "探针",
                "展示探针",
                "验证动态按钮与重复开关。",
                "该动作只服务于自动化展示层验证。",
                0,
                false,
                DevDebugAvailability.Available,
                () => DevDebugActionResult.Succeeded("probe")));

            DevDebugHubController controller =
                instance.GetComponent<DevDebugHubController>();
            Assert(controller != null, "Controller is missing");
            controller.Initialize(registry);
            RectTransform frame = instance.transform.Find(
                "SafeAreaRoot/Frame") as RectTransform;
            Assert(frame != null, "Frame is missing");
            Vector2 restPosition = frame.anchoredPosition;

            for (int i = 0; i < 3; i++)
            {
                controller.Show();
                Assert(controller.IsOpen, "Show must expose open state");
                Transform action = frame.Find(
                    "Body/ActionPanel/ActionScroll/Viewport/Content/" +
                    "Action_probe.presentation");
                Assert(action != null, "Registered action view was not generated");
                controller.HideImmediate();
                Assert(!controller.IsOpen, "Hide must clear open state");
                Assert(frame.anchoredPosition == restPosition,
                    "Repeated open/close drifted the frame position");
                Assert(frame.localScale == Vector3.one,
                    "Repeated open/close drifted the frame scale");
            }

            return "PASS development debug hub presentation lifecycle";
        }
        finally
        {
            registration?.Dispose();
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static void Assert(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }
}
#endif
