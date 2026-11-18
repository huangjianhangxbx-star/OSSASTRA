/* 渲染图层的视差与 OSLight 响应。碰撞和作者坐标保持固定。 */
using Ossastra.Characters;
using UnityEngine;

namespace Ossastra.SideView.TerrainWorkshop
{
    [DefaultExecutionOrder(2000)]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class TerrainWorkshopCardPolicy : MonoBehaviour
    {
        private static readonly int AceResponse = Shader.PropertyToID("_SideViewAceSurfaceResponse");
        [SerializeField] private float parallax;
        [SerializeField] private float lightResponse;
        private Camera view;
        private Transform mapRoot;
        private Vector3 authorPosition;

#if UNITY_EDITOR
        public void Configure(float authoredParallax, float authoredLight)
            => (parallax, lightResponse) = (authoredParallax, Mathf.Clamp01(authoredLight));
#endif

        private void Awake()
        {
            var renderer = GetComponent<MeshRenderer>();
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetFloat(AceResponse, lightResponse);
            renderer.SetPropertyBlock(block);
            authorPosition = transform.localPosition;
            var root = GetComponentInParent<TerrainWorkshopRoot>();
            mapRoot = root == null ? null : root.transform;
        }

        private void LateUpdate()
        {
            if (Mathf.Approximately(parallax, 0) || mapRoot == null) return;
            if (view == null) view = Camera.main;
            if (view == null || view.GetComponent<SideViewCameraFollow>() == null) return;
            var offset = mapRoot.InverseTransformPoint(view.transform.position);
            transform.localPosition = authorPosition + parallax * new Vector3(offset.x, offset.y, 0);
        }
    }
}
