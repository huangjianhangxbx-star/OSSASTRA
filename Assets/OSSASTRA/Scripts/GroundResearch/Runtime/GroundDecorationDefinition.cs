/* 脚本：GroundDecorationDefinition
 * 功能：定义独立花草Sprite、图像脚点、摆放/朝向策略和有限变体范围。
 * 边界：Sprite像素大小不代表碰撞占地；默认无玩法碰撞，不从图片名字推断原作Billboard。
 */
using UnityEngine;
namespace Ossastra.GroundResearch
{
    [CreateAssetMenu(menuName="OSSASTRA/地表/装饰定义")]
    public sealed class GroundDecorationDefinition : ScriptableObject
    {
        public int schemaVersion = 1;
        public string assetId, revision = "1", displayName;
        [TextArea(2,5)] public string evidence;
        public Sprite sprite;
        public GroundDecorationMode mode;
        public Vector2 footpointNormalized = new Vector2(.5f,0f);
        public Vector2 scaleRange = new Vector2(.8f,1.2f);
        public float groundOffset = .03f;
        public bool allowMirror = true;
    }
}
