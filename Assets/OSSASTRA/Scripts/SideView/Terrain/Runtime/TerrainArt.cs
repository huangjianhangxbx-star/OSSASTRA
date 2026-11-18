/* 地形图件引用本工程的原图或 Sprite，并保留像素裁片和来源。工坊编辑映射时不改写媒体。 */
using UnityEngine;

namespace Ossastra.SideView.TerrainWorkshop
{
    public sealed class TerrainArt : ScriptableObject
    {
        public string theme, purpose, origin;
        public Sprite sprite;
        public Texture2D texture;
        public Rect crop;

        public Texture2D Image => sprite != null ? sprite.texture : texture;
        public Rect Pixels
        {
            get
            {
                if (Image == null) return default;
                if (crop.width > 0 && crop.height > 0)
                    return Rect.MinMaxRect(Mathf.Clamp(crop.xMin, 0, Image.width), Mathf.Clamp(crop.yMin, 0, Image.height),
                        Mathf.Clamp(crop.xMax, 0, Image.width), Mathf.Clamp(crop.yMax, 0, Image.height));
                return sprite != null ? sprite.rect : new Rect(0, 0, Image.width, Image.height);
            }
        }
        public Vector2 PixelSize => Pixels.size;
        public Rect UV
        {
            get
            {
                var image = Image;
                var pixels = Pixels;
                return image == null ? default : new Rect(pixels.x / image.width, pixels.y / image.height,
                    pixels.width / image.width, pixels.height / image.height);
            }
        }
    }
}
