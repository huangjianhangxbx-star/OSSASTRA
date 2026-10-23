#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public static class DRPGBattleActorHitProxyProbe
{
    public static string Run()
    {
        VerifyProjection();
        VerifyPointerRouting();
        return "PASS DRPG battle actor hit proxies";
    }

    private static void VerifyProjection()
    {
        var cameraObject = new GameObject("ProjectionCamera");
        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 60f;
            camera.aspect = 16f / 9f;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            AssertTrue(BattleActorScreenProjector.TryProjectToViewport(
                    camera,
                    new Bounds(Vector3.zero, new Vector3(2f, 4f, 1f)),
                    out Rect rect,
                    out float depth),
                "Visible world bounds must project into the battle viewport");
            AssertTrue(rect.width > 0f && rect.height > 0f,
                "Projected hit targets need a non-zero screen area");
            AssertNear(0.5f, rect.center.x,
                "A centered world actor must remain centered on screen");
            AssertTrue(depth > 0f,
                "Projected depth must remain available for overlap ordering");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    private static void VerifyPointerRouting()
    {
        var hitObject = new GameObject(
            "HitProxy",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(BattleActorScreenHitProxy));
        try
        {
            BattleActorScreenHitProxy proxy =
                hitObject.GetComponent<BattleActorScreenHitProxy>();
            proxy.Bind(42, sortingOrder: 200, projectedDepth: 4f);
            int hovered = 0;
            int clicked = 0;
            proxy.HoverChanged += (runtimeId, value) =>
            {
                if (value)
                    hovered = runtimeId;
            };
            proxy.Clicked += runtimeId => clicked = runtimeId;

            proxy.OnPointerEnter(new PointerEventData(null));
            proxy.OnPointerClick(new PointerEventData(null));
            AssertEqual(42, hovered,
                "Hover must route the bound runtime ID");
            AssertEqual(42, clicked,
                "Click must route the bound runtime ID");
            AssertTrue(proxy.raycastTarget,
                "The transparent proxy must remain a UGUI raycast target");

            proxy.ResetBinding();
            AssertEqual(0, proxy.RuntimeId,
                "Pooled proxies must clear stale actor identity");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(hitObject);
        }
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }

    private static void AssertNear(float expected, float actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.02f)
            throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new InvalidOperationException(message);
    }
}
#endif
