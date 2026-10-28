/* 脚本：CommerceMenuIconGraphic
 * 功能：以原型菜单的四个简单线图标绘制uGUI网格；不引入原作图集或商业媒体。
 * 边界：只读图标种类并生成线段，无菜单业务、物品或角色状态。
 */
using UnityEngine;
namespace Ossastra.Commerce.Presentation
{
    public enum CommerceMenuIcon { Shop,Dialogue,Gift,Equipment }
    public sealed class CommerceMenuIconGraphic:UnityEngine.UI.MaskableGraphic
    {
        public CommerceMenuIcon Kind {get;set;}
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            switch(Kind)
            {
                case CommerceMenuIcon.Shop:
                    Path(mesh,3,10,21,10,19,4,5,4,3,10);Path(mesh,5,11,5,21,19,21,19,11);Path(mesh,9,21,9,14,15,14,15,21);break;
                case CommerceMenuIcon.Dialogue:
                    Path(mesh,3,4,21,4,21,17,9,17,4,21,4,17,3,17,3,4);Path(mesh,7,8,17,8);Path(mesh,7,12,14,12);break;
                case CommerceMenuIcon.Gift:
                    Path(mesh,3,10,21,10,21,14,3,14,3,10);Path(mesh,5,14,5,21,19,21,19,14);Path(mesh,12,10,12,21);Path(mesh,12,10,6,8,5,4,8,3,12,10,16,3,19,4,18,8,12,10);break;
                case CommerceMenuIcon.Equipment:
                    Path(mesh,8,3,12,6,16,3,22,8,18,13,16,11,16,21,8,21,8,11,6,13,2,8,8,3);break;
            }
        }
        private static void Path(UnityEngine.UI.VertexHelper mesh,params float[] coordinates)
        {
            var color=new Color32(238,228,205,255);
            for(var i=0;i+3<coordinates.Length;i+=2)
            {
                var a=new Vector2(coordinates[i],-coordinates[i+1]);var b=new Vector2(coordinates[i+2],-coordinates[i+3]);var n=new Vector2(-(b-a).y,(b-a).x).normalized*.8f;
                var start=mesh.currentVertCount;mesh.AddVert(a+n,color,Vector2.zero);mesh.AddVert(b+n,color,Vector2.zero);mesh.AddVert(b-n,color,Vector2.zero);mesh.AddVert(a-n,color,Vector2.zero);mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
            }
        }
    }
}
