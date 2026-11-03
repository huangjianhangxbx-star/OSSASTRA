/* 功能：验证同一相机同帧重绘的合法输出失效与真正资源释放分离。
 * 边界：只使用临时相机/两RT；不改变生产注册，所有登记和临时对象在finally撤销。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Ossastra.WorldRendering.Rendering;
using Funplay.Editor.Tools.Scripting;

public sealed class RenderingBridgeContract : IFunplayCommand
{
    public void Execute(ExecutionContext context)
    {
        if (Application.dataPath != "E:/WORLDCREATOR/new/Project_ANKALUTE/Assets") throw new InvalidOperationException("wrong project");
        var host = new GameObject("Isolated legal frame bridge contract") { hideFlags = HideFlags.HideAndDontSave };
        var camera = host.AddComponent<Camera>(); camera.enabled = false;
        var permissionTexture = new RenderTexture(8, 8, 0, RenderTextureFormat.ARGB32);
        var depthTexture = new RenderTexture(8, 8, 24, RenderTextureFormat.Depth);
        permissionTexture.Create(); depthTexture.Create();
        var permission = RTHandles.Alloc(permissionTexture); var depth = RTHandles.Alloc(depthTexture);
        int releases = 0, id = camera.GetInstanceID();
        Action<int> onRelease = value => { if (value == id) ++releases; };
        var failures = new List<string>();
        WorldLookLegalFrameRegistry.Released += onRelease;
        try
        {
            WorldLookLegalFrameRegistry.Publish(camera, permission, depth, Time.frameCount);
            if (!WorldLookLegalFrameRegistry.TryGet(camera, Time.frameCount, out var firstPermission, out var firstDepth) || firstPermission != permission || firstDepth != depth) failures.Add("fresh publish not readable");
            WorldLookLegalFrameRegistry.Invalidate(camera);
            if (WorldLookLegalFrameRegistry.TryGet(camera, Time.frameCount, out _, out _) || releases != 0) failures.Add("invalidation kept feed or emitted release");
            WorldLookLegalFrameRegistry.Publish(camera, permission, depth, Time.frameCount);
            if (!WorldLookLegalFrameRegistry.TryGet(camera, Time.frameCount, out _, out _) || releases != 0) failures.Add("same-frame republish rejected or released consumer");
            WorldLookLegalFrameRegistry.Invalidate(camera); WorldLookLegalFrameRegistry.Revoke(camera);
            if (WorldLookLegalFrameRegistry.TryGet(camera, Time.frameCount, out _, out _) || releases != 1) failures.Add("revoking invalidated owner did not release exactly once");
            WorldLookLegalFrameRegistry.Revoke(camera);
            if (releases != 1) failures.Add("repeated revoke emitted duplicate release");
            WorldLookLegalFrameRegistry.Publish(camera, permission, depth, Time.frameCount - 1);
            if (WorldLookLegalFrameRegistry.TryGet(camera, Time.frameCount, out _, out _)) failures.Add("stale producer frame granted access");
            context.ReturnValue = new { pass = failures.Count == 0, assertions = 6, releases, failures = failures.ToArray() };
        }
        finally
        {
            WorldLookLegalFrameRegistry.Revoke(camera); WorldLookLegalFrameRegistry.Released -= onRelease;
            permission.Release(); depth.Release(); permissionTexture.Release(); depthTexture.Release();
            UnityEngine.Object.DestroyImmediate(permissionTexture); UnityEngine.Object.DestroyImmediate(depthTexture); UnityEngine.Object.DestroyImmediate(host);
        }
    }
}
