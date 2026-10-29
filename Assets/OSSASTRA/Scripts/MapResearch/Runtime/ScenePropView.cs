/*
 * 脚本：ScenePropView
 * 功能：维护研究物件的来源、脚点/占地和显示平面，按场景发布的正常观察基展示。
 * 边界：不修改世界根、Collider或角色；结构观察仍使用被冻结的正常相机基，不追随观察相机。
 * 协作：MapResearchRoot显式调用；帧贴图通过MaterialPropertyBlock切换，不实例化运行时材质。
 */
using Ossastra.Characters.ViewSpace;
using UnityEngine;

namespace Ossastra.MapResearch
{
    public enum PropViewMode { CameraPlane, VerticalBillboard, Fixed, Ground }

    [DisallowMultipleComponent]
    public sealed class ScenePropView : MonoBehaviour
    {
        [Header("只读来源记录；手工样地位置不是原作存档复刻")]
        public string stableId;
        public string sourceArchive;
        public string sourceEntry;
        public string sourceSha256;
        public string evidenceLevel = "UNITY_ADAPTATION";
        [Header("世界根保持位置/碰撞，仅displayPlane转向")]
        public Transform displayPlane;
        public Renderer displayRenderer;
        public PropViewMode mode;
        public Vector2 footprint = new Vector2(0.5f, 0.5f);
        public float pixelsPerUnit = 100f;
        public bool snapsToGrid;
        [Header("完整物件合成帧；静态物件只有一帧")]
        public Texture2D[] frames;
        [Min(0f)] public float framesPerSecond;
        public bool loopAnimation = true;
        [Header("单张完整物件图集：UV矩形与每帧时长；不拆出成百张贴图")]
        public Texture2D sheetTexture;
        public ScenePropFrame[] sheetFrames;

        private MaterialPropertyBlock properties;
        private MaterialPropertyBlock originalProperties;
        private Quaternion originalRotation;
        private bool captured;
        private int shownFrame = -1;
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int MainTexSt = Shader.PropertyToID("_MainTex_ST");

        public void TickView(ICharacterViewContext context, double time)
        {
            if (!isActiveAndEnabled || displayPlane == null || displayPlane == transform || !displayPlane.IsChildOf(transform) || context == null || !context.IsValid) return;
            if (!captured)
            {
                originalRotation = displayPlane.localRotation;
                if (displayRenderer != null)
                {
                    originalProperties = new MaterialPropertyBlock();
                    displayRenderer.GetPropertyBlock(originalProperties);
                }
                captured = true;
            }
            if (mode == PropViewMode.CameraPlane) displayPlane.rotation = context.ViewRotation;
            else if (mode == PropViewMode.VerticalBillboard)
            {
                displayPlane.rotation = Quaternion.LookRotation(context.ViewForward, Vector3.up);
            }
            // Fixed/Ground保留作者设置，不将地面、阴影与立体诊断物件一律Billboard。
            if (displayRenderer == null) return;
            bool sheet = sheetTexture != null && sheetFrames != null && sheetFrames.Length > 0;
            if (!sheet && (frames == null || frames.Length == 0)) return;
            int index = sheet ? ScenePropFrameClock.ResolveFrame(sheetFrames, time, loopAnimation) : 0;
            if (!sheet && framesPerSecond > 0f && time >= 0d)
            {
                double frame = System.Math.Floor(time * framesPerSecond);
                index = loopAnimation ? (int)(frame % frames.Length) : (int)System.Math.Min(frame, frames.Length - 1);
            }
            Texture texture = sheet ? sheetTexture : frames[index];
            if (index == shownFrame || texture == null) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            displayRenderer.GetPropertyBlock(properties);
            properties.SetTexture(BaseMap, texture);
            properties.SetTexture(MainTex, texture);
            Rect uv = sheet ? sheetFrames[index].uv : new Rect(0, 0, 1, 1);
            properties.SetVector(MainTexSt, new Vector4(uv.width, uv.height, uv.x, uv.y));
            displayRenderer.SetPropertyBlock(properties);
            shownFrame = index;
        }

        public void RestoreView()
        {
            if (!captured) return;
            if (displayPlane != null) displayPlane.localRotation = originalRotation;
            if (displayRenderer != null && originalProperties != null) displayRenderer.SetPropertyBlock(originalProperties);
            captured = false;
            shownFrame = -1;
        }

        private void OnDisable() => RestoreView();
        private void OnDestroy() => RestoreView();

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.position, 0.045f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.015f, new Vector3(footprint.x * 2f, 0.03f, footprint.y * 2f));
            if (displayRenderer == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.matrix = displayPlane != null ? displayPlane.localToWorldMatrix : Matrix4x4.identity;
            if (displayPlane != null)
            {
                var mesh = displayPlane.GetComponent<MeshFilter>();
                if (mesh != null && mesh.sharedMesh != null)
                {
                    Bounds bounds = mesh.sharedMesh.bounds;
                    Gizmos.DrawWireCube(bounds.center, bounds.size);
                }
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
