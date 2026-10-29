/*
 * 脚本：GroundDecorationOrientation
 * 功能：每地图集中刷新装饰视觉的平铺/固定直立/仅Y向随镜头表现，并补偿Sprite脚点。
 * 负责：缓存本工具实例；为Game/SceneView相机派生visual姿态，不为每株添加Update。
 * 不负责：随机生成、删除、作者根TRS、玩法碰撞、相机控制或运行时材质创建。
 * 协作：Editor装饰事务创建作者实例后显式Refresh；Root Transform始终是唯一脚点作者数据。
 */
using System;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Ossastra.GroundResearch
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class GroundDecorationOrientation : MonoBehaviour
    {
        public GroundWorkshopMap map;
        [Tooltip("可留空：游戏采用MainCamera，SceneView在其实际渲染前单独刷新。")]
        public Camera viewCamera;
        private Entry[] entries = Array.Empty<Entry>();
        private int childCount = -1;
        private struct Entry
        {
            public GroundDecorationInstance instance;
            public Transform visual;
            public SpriteRenderer renderer;
        }

        private void OnEnable()
        {
            Camera.onPreCull += BeforeCamera;
            RenderPipelineManager.beginCameraRendering += BeforePipelineCamera;
            Refresh();
        }
        private void OnDisable()
        {
            Camera.onPreCull -= BeforeCamera;
            RenderPipelineManager.beginCameraRendering -= BeforePipelineCamera;
            entries = Array.Empty<Entry>(); childCount = -1;
        }
        private void LateUpdate() { RefreshForCamera(DefaultCamera()); }
        private void BeforePipelineCamera(ScriptableRenderContext context, Camera camera) { BeforeCamera(camera); }
        private void BeforeCamera(Camera camera)
        {
            if (camera == null || (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView)) return;
            if (camera.cameraType == CameraType.Game && viewCamera != null && camera != viewCamera) return;
            // URP与SceneView可能同帧用不同相机；只改派生视觉，不能为了观察而旋转作者根或相机。
            RefreshForCamera(camera);
        }
        private Camera DefaultCamera()
        {
            if (viewCamera != null) return viewCamera;
            var main = Camera.main;
            if (main != null) return main;
#if UNITY_EDITOR
            if (!Application.isPlaying && SceneView.lastActiveSceneView != null) return SceneView.lastActiveSceneView.camera;
#endif
            return null;
        }
        private void Resolve()
        {
            if (map == null) map = GetComponent<GroundWorkshopMap>();
            if (map == null || map.decorationRoot == null) { entries = Array.Empty<Entry>(); childCount = -1; return; }
            if (childCount == map.decorationRoot.childCount) return;
            var all = map.decorationRoot.GetComponentsInChildren<GroundDecorationInstance>(true);
            var buffer = new System.Collections.Generic.List<Entry>();
            string prefix = map.registration == null ? null : "Ossastra.GroundWorkshop:" + map.registration.mapId + ":";
            foreach (var instance in all)
            {
                if (instance == null || instance.transform.parent != map.decorationRoot || instance.definition == null || instance.visual == null ||
                    instance.visual.parent != instance.transform || string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(instance.generationGroup) ||
                    !instance.generationGroup.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var renderer = instance.visual.GetComponent<SpriteRenderer>();
                if (renderer != null) buffer.Add(new Entry { instance=instance,visual=instance.visual,renderer=renderer });
            }
            entries = buffer.ToArray(); childCount = map.decorationRoot.childCount;
        }

        /// <summary>作者事务结束后重建缓存；重绘和Play只刷新派生表现，不创建或重撒对象。</summary>
        public void Refresh() { childCount = -1; RefreshForCamera(DefaultCamera()); }

        /// <summary>为指定相机刷新视觉，供真实渲染及有界验证使用；不修改根TRS。</summary>
        public void RefreshForCamera(Camera camera)
        {
            Resolve();
            foreach (var entry in entries)
            {
                var instance = entry.instance;
                if (instance == null || entry.visual == null || entry.renderer == null || instance.definition == null ||
                    instance.visual != entry.visual || !entry.visual.IsChildOf(instance.transform)) { childCount = -1; continue; }
                var definition = instance.definition; var visual = entry.visual; var sprite = entry.renderer.sprite;
                if (sprite == null || sprite.pixelsPerUnit <= 0f) continue;
                switch (definition.mode)
                {
                    case GroundDecorationMode.Flat: visual.localRotation = Quaternion.Euler(90f,0f,0f); break;
                    case GroundDecorationMode.FixedUpright: visual.localRotation = Quaternion.identity; break;
                    case GroundDecorationMode.UprightYaw:
                        if (camera != null) visual.rotation = Quaternion.Euler(0f,camera.transform.eulerAngles.y,0f);
                        break;
                }
                // Sprite局部F点=(定义脚点像素-pivot)/PPU。把它经过镜像/旋转后的偏差反向移回根；
                // groundOffset按世界Up换算到父局部，避免作者缩放或转镜头令脚点漂移。
                var foot = new Vector3((definition.footpointNormalized.x*sprite.rect.width-sprite.pivot.x)/sprite.pixelsPerUnit,
                    (definition.footpointNormalized.y*sprite.rect.height-sprite.pivot.y)/sprite.pixelsPerUnit,0f);
                Vector3 offset = instance.transform.InverseTransformVector(Vector3.up*definition.groundOffset);
                Vector3 derived = offset - visual.localRotation*Vector3.Scale(foot,visual.localScale);
                if (Finite(derived)) visual.localPosition = derived;
            }
        }
        private static bool Finite(Vector3 value) => !float.IsNaN(value.x)&&!float.IsNaN(value.y)&&!float.IsNaN(value.z)&&
            !float.IsInfinity(value.x)&&!float.IsInfinity(value.y)&&!float.IsInfinity(value.z);
    }
}
