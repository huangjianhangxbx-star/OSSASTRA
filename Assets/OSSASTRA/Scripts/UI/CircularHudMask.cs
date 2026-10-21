/* 脚本：CircularHudMask
 * 功能：对应原型CSS border-radius:50%的原生uGUI裁剪几何。
 * 边界：只裁剪头像；不修改用户画像纹理或生成替代头像。
 */
using UnityEngine;
using UnityEngine.UI;
namespace Ossastra.UserInterface
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CircularHudMask:MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var rect=GetPixelAdjustedRect();var center=rect.center;var radius=rect.size*.5f;
            mesh.AddVert(center,color,Vector2.one*.5f);
            const int count=64;
            for(int i=0;i<=count;i++){var angle=i*Mathf.PI*2/count;var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));mesh.AddVert(center+Vector2.Scale(direction,radius),color,direction*.5f+Vector2.one*.5f);if(i>0)mesh.AddTriangle(0,i,i+1);}
        }
    }
}
