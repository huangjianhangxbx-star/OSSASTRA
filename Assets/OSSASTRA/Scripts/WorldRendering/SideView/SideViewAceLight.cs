/* 功能：登记横版二维投影光的形状、颜色、图层和世界锚点。
 * 边界：不拥有相机、角色、物理遮挡或可见叠层。Transform 只用来放置灯片。 */
using System;
using UnityEngine;

namespace Ossastra.WorldRendering.SideView
{
    [Flags]
    public enum SideViewAceLightGroups
    {
        World = 1,
        Character = 2,
        Both = World | Character
    }

    [DisallowMultipleComponent]
    public sealed class SideViewAceLight : MonoBehaviour
    {
        [SerializeField] private SideViewAceLightGroups groups = SideViewAceLightGroups.Both;
        [SerializeField] private Vector2 size = new Vector2(12f, 8f);
        [SerializeField, Range(0f, 4f)] private float intensity = 1f;
        [SerializeField, Range(.1f, 10f)] private float falloff = 2f;
        [SerializeField, Range(.1f, 10f)] private float innerScale = 1f;
        [SerializeField, Range(0f, 2f)] private float edgeOffset = 1f;
        [SerializeField, Range(0f, 1f)] private float emission = 0f;
        [SerializeField, ColorUsage(true, true)] private Color ambient = new Color(.7f, .8f, .9f, .75f);
        [SerializeField, ColorUsage(true, true)] private Color bright = new Color(.35f, .55f, .9f, .65f);
        [SerializeField, ColorUsage(true, true)] private Color highlight = new Color(.8f, .9f, 1f, .75f);
        [SerializeField] private Texture2D mask = null;

        public float Intensity
        {
            get => intensity;
            set => intensity = Mathf.Clamp(value, 0f, 4f);
        }

        internal SideViewAceLightGroups Groups => groups;
        internal Vector2 Size => size;
        internal Color Ambient => ambient;
        internal Color Bright => bright;
        internal Color Highlight => highlight;
        internal Texture2D Mask => mask != null ? mask : Texture2D.whiteTexture;
        internal Vector4 Shape => new Vector4(falloff, innerScale, edgeOffset, emission);
        internal Matrix4x4 Matrix => transform.localToWorldMatrix * Matrix4x4.Scale(new Vector3(size.x, size.y, 1f));

        internal bool HasValidSettings => groups != 0 && (groups & ~SideViewAceLightGroups.Both) == 0 &&
            Valid(size.x, .001f, 1000f) && Valid(size.y, .001f, 1000f) && Valid(intensity, 0f, 4f) &&
            Valid(falloff, .1f, 10f) && Valid(innerScale, .1f, 10f) && Valid(edgeOffset, 0f, 2f) &&
            Valid(emission, 0f, 1f) && ValidColor(ambient) && ValidColor(bright) && ValidColor(highlight);

        internal static bool Valid(float value, float minimum, float maximum) => value >= minimum && value <= maximum;
        internal static bool ValidColor(Color value) => Valid(value.r, 0f, 16f) && Valid(value.g, 0f, 16f) &&
            Valid(value.b, 0f, 16f) && Valid(value.a, 0f, 1f);
    }
}
