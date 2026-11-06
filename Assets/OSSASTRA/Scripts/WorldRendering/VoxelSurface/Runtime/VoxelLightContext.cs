/* 功能：把显式登记的主方向光/补光实例映射到本相机实际URP灯光上传序号，供体素材质区分贡献。
 * 边界：不新增Pass/相机/资源，不遍历场景，不改灯或RenderingLayer；未知方向光、点灯和逐顶点聚合不冒充登记补光。
 * 时序：既有RoomMaskRendererFeature在AddRenderPasses首部Publish；这在普通几何和许可重绘之前共用同一剔除结果。
 * 生命周期：Comparison显式Register/Unregister；相机begin归零、end只归还自身scope。嵌套相机结束恢复外层上下文。
 * 上游合同：本机URP14 ForwardLights按visibleLights顺序跳过main上传额外灯；Forward的逐物体索引由Shader另行转换。
 */
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ossastra.WorldRendering.VoxelSurface
{
    public static class VoxelLightContext
    {
        // keyValid仅证明主光身份；具体表面light-layer/强度/遮挡资格仍由共享Shader判断。
        public readonly struct Resolution
        {
            public readonly bool keyValid;
            public readonly int fillIndex;
            public Resolution(bool keyValid, int fillIndex) { this.keyValid = keyValid; this.fillIndex = fillIndex; }
        }

        sealed class CameraScope
        {
            internal Camera Camera;
            internal VoxelSurfaceComparison Owner;
            internal Resolution Value = new Resolution(false, -1);
        }

        static readonly Dictionary<int, VoxelSurfaceComparison> owners = new Dictionary<int, VoxelSurfaceComparison>();
        static readonly List<int> remove = new List<int>();
        static readonly List<CameraScope> scopes = new List<CameraScope>();
        static readonly int keyValidId = Shader.PropertyToID("_VoxelKeyLightValid");
        static readonly int fillValidId = Shader.PropertyToID("_VoxelFillLightValid");
        static readonly int fillIndexId = Shader.PropertyToID("_VoxelFillLightIndex");
        static bool subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Unsubscribe(); owners.Clear(); remove.Clear(); scopes.Clear(); Write(new Resolution(false, -1));
        }

        public static void Register(VoxelSurfaceComparison owner)
        {
            if (owner == null) return;
            // 显式重新登记允许作者更换相机，但不持续轮询全场或抢走另一个有效owner。
            remove.Clear();
            foreach (var pair in owners)
                if (pair.Value == null || ReferenceEquals(pair.Value, owner)) remove.Add(pair.Key);
            foreach (int id in remove) owners.Remove(id);
            var camera = owner.lightContextCamera;
            if (camera != null)
            {
                int id = camera.GetInstanceID();
                if (!owners.TryGetValue(id, out var current) || current == null || !current.isActiveAndEnabled || ReferenceEquals(current, owner))
                    owners[id] = owner;
            }
            if (owners.Count == 0) { Unsubscribe(); scopes.Clear(); Write(new Resolution(false, -1)); }
            else Subscribe();
        }

        public static void Unregister(VoxelSurfaceComparison owner)
        {
            if (ReferenceEquals(owner, null)) return;
            remove.Clear();
            foreach (var pair in owners)
                if (pair.Value == null || ReferenceEquals(pair.Value, owner)) remove.Add(pair.Key);
            foreach (int id in remove) owners.Remove(id);
            // 不能在内层相机结束时重新发布已经注销owner的外层快照。
            foreach (var scope in scopes)
                if (ReferenceEquals(scope.Owner, owner)) { scope.Owner = null; scope.Value = new Resolution(false, -1); }
            if (owners.Count == 0) { Unsubscribe(); scopes.Clear(); }
            Write(scopes.Count == 0 ? new Resolution(false, -1) : scopes[scopes.Count - 1].Value);
        }

        static void Subscribe()
        {
            if (subscribed) return;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
            subscribed = true;
        }

        static void Unsubscribe()
        {
            if (!subscribed) return;
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            subscribed = false;
        }

        public static void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null) return;
            scopes.Add(new CameraScope { Camera = camera });
            Write(new Resolution(false, -1));
        }

        public static void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (scopes.Count == 0 || !ReferenceEquals(scopes[scopes.Count - 1].Camera, camera)) return;
            scopes.RemoveAt(scopes.Count - 1);
            Write(scopes.Count == 0 ? new Resolution(false, -1) : scopes[scopes.Count - 1].Value);
        }

        public static void Publish(Camera camera, in RenderingData renderingData)
        {
            if (camera == null || scopes.Count == 0 || !ReferenceEquals(scopes[scopes.Count - 1].Camera, camera)) return;
            var scope = scopes[scopes.Count - 1];
            scope.Owner = null; scope.Value = new Resolution(false, -1);
            if (ReferenceEquals(renderingData.cameraData.camera, camera) && owners.TryGetValue(camera.GetInstanceID(), out var owner)
                && owner != null && owner.isActiveAndEnabled && ReferenceEquals(owner.lightContextCamera, camera))
            {
                scope.Owner = owner;
                scope.Value = Resolve(renderingData.lightData, owner.keyLight, owner.directionalFill);
            }
            Write(scope.Value);
        }

        public static Resolution Resolve(in LightData data, Light key, Light fill)
        {
            if (!data.visibleLights.IsCreated) return new Resolution(false, -1);
            int main = data.mainLightIndex;
            bool validKey = IsDirectional(key) && main >= 0 && main < data.visibleLights.Length
                && data.visibleLights[main].lightType == LightType.Directional && ReferenceEquals(data.visibleLights[main].light, key);
            int fillIndex = -1;
            if (IsDirectional(fill) && !ReferenceEquals(fill, key) && data.supportsAdditionalLights
                && !data.shadeAdditionalLightsPerVertex && data.additionalLightsCount > 0)
            {
                int count = 0, maximum = Mathf.Min(data.additionalLightsCount, UniversalRenderPipeline.maxVisibleAdditionalLights);
                for (int i = 0; i < data.visibleLights.Length && count < maximum; ++i)
                {
                    if (i == main) continue;
                    var light = data.visibleLights[i];
                    if (light.lightType == LightType.Directional && ReferenceEquals(light.light, fill)) { fillIndex = count; break; }
                    ++count;
                }
            }
            return new Resolution(validKey, fillIndex);
        }

        static bool IsDirectional(Light light)
        { return light != null && light.isActiveAndEnabled && light.type == LightType.Directional; }

        static void Write(Resolution value)
        {
            Shader.SetGlobalInt(keyValidId, value.keyValid ? 1 : 0);
            Shader.SetGlobalInt(fillValidId, value.fillIndex >= 0 ? 1 : 0);
            Shader.SetGlobalInt(fillIndexId, value.fillIndex);
        }
    }
}
