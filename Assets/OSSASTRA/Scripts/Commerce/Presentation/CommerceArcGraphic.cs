/* 脚本：CommerceArcGraphic
 * 功能：以原型菜单的两条贝塞尔边界绘制轻量uGUI弧带；只生成本Graphic的网格，无贴图与世界逻辑。
 * 边界：翻转仅改变菜单装饰；不影响角色方向、物理、射线或交易资格。
 */
using UnityEngine;
namespace Ossastra.Commerce.Presentation
{
    public sealed class CommerceArcGraphic:UnityEngine.UI.MaskableGraphic
    {
        private bool flip;
        public bool Flip {get=>flip;set{if(flip==value)return;flip=value;SetVerticesDirty();}}
        private Vector2 Curve(Vector2 a,Vector2 b,Vector2 c,Vector2 d,float t)
        {var u=1-t;var p=u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;return new Vector2(flip?220-p.x:p.x,-p.y);}
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();var fill=new Color32(24,33,40,239);var border=new Color32(129,144,151,255);
            for(var i=0;i<36;i++)
            {
                var t0=i/36f;var t1=(i+1)/36f;
                var a=Curve(new Vector2(208,8),new Vector2(12,32),new Vector2(12,268),new Vector2(208,292),t0);
                var b=Curve(new Vector2(208,8),new Vector2(12,32),new Vector2(12,268),new Vector2(208,292),t1);
                var c=Curve(new Vector2(199,55),new Vector2(84,76),new Vector2(84,224),new Vector2(199,245),t1);
                var d=Curve(new Vector2(199,55),new Vector2(84,76),new Vector2(84,224),new Vector2(199,245),t0);
                Quad(mesh,a,b,c,d,fill);Line(mesh,a,b,border);Line(mesh,d,c,border);
                if(i==0)Line(mesh,a,d,border);if(i==35)Line(mesh,b,c,border);
            }
        }
        private static void Line(UnityEngine.UI.VertexHelper mesh,Vector2 a,Vector2 b,Color32 color)
        {var n=new Vector2(-(b-a).y,(b-a).x).normalized*.7f;Quad(mesh,a+n,b+n,b-n,a-n,color);}
        private static void Quad(UnityEngine.UI.VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color32 color)
        {var start=mesh.currentVertCount;mesh.AddVert(a,color,Vector2.zero);mesh.AddVert(b,color,Vector2.zero);mesh.AddVert(c,color,Vector2.zero);mesh.AddVert(d,color,Vector2.zero);mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);}
    }
}
