/*
 * 脚本：GroundSurfaceMeshGenerator
 * 功能：读取原生Tilemap的实际Sprite/UV/颜色/显示矩阵，生成按层、作者块和真实纹理绑定分组的薄Mesh。
 * 负责：完整父TRS换算、作者四角与平台域相交、边缘UV/颜色插值、逐三角来源和有界统计。
 * 不负责：资产创建、材质实例、Renderer状态、作者格修改、角色/碰撞或每帧自动更新。
 * 协作：root桥提供有效原层或预览Tilemap；Geometry模块处理自由凹环；调用方持久化/替换/释放返回Mesh。
 * 关键约束：禁止按rect拼假几何或重跑规则；未知/动画/实例物件Tile拒绝；矩阵路线仍须Native像素A/B验证。
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Ossastra.GroundResearch;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace Ossastra.GroundSurfaces.Editor
{
    public static class GroundSurfaceMeshGenerator
    {
        private const int MaxInputCells=262144;
        private const int MaxInputTriangles=1000000;
        private const int MaxOutputVertices=4000000;

        private sealed class CellSource
        {
            public SurfaceLayerInput input;
            public Vector3Int cell;
            public Sprite sprite;
            public Texture texture;
            public Vector2[] vertices,uv;
            public ushort[] triangles;
            public Matrix4x4 spriteToSurface;
            public Color color;
        }
        private struct GroupKey : IEquatable<GroupKey>
        {
            public int source,material,texture,layer,x,y;
            public bool Equals(GroupKey other)=>source==other.source&&material==other.material&&texture==other.texture&&layer==other.layer&&x==other.x&&y==other.y;
            public override bool Equals(object value)=>value is GroupKey other&&Equals(other);
            public override int GetHashCode(){unchecked{return (((((source*397)^material)*397^texture)*397^layer)*397^x)*397^y;}}
        }
        private sealed class Group
        {
            public GroupKey key;
            public CellSource source;
            public readonly List<Vector3> positions=new List<Vector3>();
            public readonly List<Vector2> uv=new List<Vector2>();
            public readonly List<Color32> colors=new List<Color32>();
            public readonly List<int> indices=new List<int>();
            public readonly List<Vector3Int> cells=new List<Vector3Int>();
            public readonly List<Sprite> sprites=new List<Sprite>();
        }

        /// <summary>只构造新的派生Mesh；无源数据写入。异常时释放本次已创建Mesh，旧缓存由调用方原子保留。</summary>
        public static SurfaceMeshBuildResult Build(GroundSurfaceBinding binding,SurfaceDomain domain,SurfaceLayerInput[] inputs,bool clipToBoundary)
        {
            var watch=Stopwatch.StartNew();
            var result=new SurfaceMeshBuildResult();
            Validate(binding,domain,inputs,clipToBoundary);
            var sources=ReadSources(binding,inputs,result);
            var authorQuad=AuthorQuad(binding);
            var groups=new Dictionary<GroupKey,Group>();
            var meshes=new List<Mesh>();
            int emittedVertices=0;
            float tolerance=Mathf.Max(1e-7f,binding.boundary.planeTolerance);
            try
            {
                foreach(var source in sources)
                {
                    var key=new GroupKey{source=source.input.tilemap.GetInstanceID(),material=source.input.material.GetInstanceID(),texture=source.texture.GetInstanceID(),
                        layer=source.input.layerIndex,x=FloorDiv(source.cell.x,binding.chunkSize),y=FloorDiv(source.cell.y,binding.chunkSize)};
                    Group group=null;
                    for(int index=0;index<source.triangles.Length;index+=3)
                    {
                        ClipVertex a=Vertex(source,source.triangles[index]),b=Vertex(source,source.triangles[index+1]),c=Vertex(source,source.triangles[index+2]);
                        if(Math.Abs(Cross(a.position,b.position,c.position))<1e-12){result.discardedTriangles++;continue;}
                        if(binding.flattenDisplay&&Mathf.Max(a.position.y,Mathf.Max(b.position.y,c.position.y))-Mathf.Min(a.position.y,Mathf.Min(b.position.y,c.position.y))>tolerance)
                            throw Invalid(source,"Sprite经实际变换后不在水平平面，不能静默压平倾斜作者几何。");
                        var vertices=new List<ClipVertex>();var indices=new List<int>();
                        if(!clipToBoundary)AppendTriangle(a,b,c,vertices,indices);
                        else
                        {
                            // 作者范围是Grid真实四角，不能用旋转后的世界AABB替代。
                            var polygon=ClipAuthorQuad(a,b,c,authorQuad,tolerance);
                            for(int fan=1;fan+1<polygon.Count;fan++)
                                GroundSurfaceGeometry.ClipTriangle(polygon[0],polygon[fan],polygon[fan+1],domain,vertices,indices);
                        }
                        if(indices.Count==0){result.discardedTriangles++;continue;}
                        if(SameTriangle(a,b,c,vertices,indices,tolerance))result.whollyInsideTriangles++;else result.clippedTriangles++;
                        if(group==null&&!groups.TryGetValue(key,out group)){group=new Group{key=key,source=source};groups.Add(key,group);}
                        for(int triangle=0;triangle<indices.Count;triangle+=3)
                        {
                            if(emittedVertices+3>MaxOutputVertices)throw new InvalidOperationException("薄Mesh输出超过本轮400万顶点预算；旧缓存未替换。");
                            int start=group.positions.Count;
                            for(int corner=0;corner<3;corner++)
                            {
                                var value=vertices[indices[triangle+corner]];var position=value.position;
                                if(binding.flattenDisplay)position.y=binding.boundary.localTopY;
                                group.positions.Add(position);group.uv.Add(value.uv);group.colors.Add(value.color);group.indices.Add(start+corner);
                            }
                            group.cells.Add(source.cell);group.sprites.Add(source.sprite);emittedVertices+=3;
                        }
                    }
                }
                var chunks=new List<SurfaceMeshChunk>();
                foreach(var group in groups.Values.OrderBy(g=>g.key.layer).ThenBy(g=>g.key.x).ThenBy(g=>g.key.y).ThenBy(g=>g.key.source).ThenBy(g=>g.key.material).ThenBy(g=>g.key.texture))
                {
                    if(group.indices.Count==0)continue;
                    var mesh=new Mesh{name="ThinTop_"+group.key.layer+"_"+group.key.x+"_"+group.key.y};meshes.Add(mesh);
                    mesh.indexFormat=group.positions.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
                    mesh.SetVertices(group.positions);mesh.SetUVs(0,group.uv);mesh.SetColors(group.colors);mesh.SetTriangles(group.indices,0,true);
                    mesh.RecalculateBounds();
                    chunks.Add(new SurfaceMeshChunk{mesh=mesh,mainTexture=group.source.texture,sourceMaterial=group.source.input.material,
                        layerIndex=group.key.layer,chunk=new Vector2Int(group.key.x,group.key.y),sourceTilemap=group.source.input.tilemap,
                        cells=group.cells.ToArray(),sprites=group.sprites.ToArray()});
                }
                result.chunks=chunks.ToArray();result.outputVertices=emittedVertices;result.outputTriangles=chunks.Sum(c=>c.mesh.triangles.Length/3);
                result.materialTextureGroups=groups.Keys.Select(key=>new{key.source,key.material,key.texture,key.layer}).Distinct().Count();
                watch.Stop();result.elapsedMilliseconds=watch.Elapsed.TotalMilliseconds;return result;
            }
            catch
            {
                foreach(var mesh in meshes)if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);
                throw;
            }
        }

        private static void Validate(GroundSurfaceBinding binding,SurfaceDomain domain,SurfaceLayerInput[] inputs,bool clip)
        {
            if(binding==null||binding.map==null||binding.map.surface==null||binding.map.surface.grid==null||binding.boundary==null||
                binding.map.surface.WorldArea<=0||!Finite(binding.boundary.localTopY)||binding.chunkSize<1||binding.chunkSize>32)
                throw new InvalidOperationException("薄顶面缺明确Binding/Map/Grid/Boundary，或chunkSize不在1–32。");
            if(inputs==null||inputs.Length==0||inputs.Length>32||inputs.Any(input=>input==null||input.tilemap==null||input.material==null||input.layerIndex<0)||
                inputs.Select(input=>input.tilemap).Distinct().Count()!=inputs.Length)
                throw new InvalidOperationException("薄顶面输入必须为1–32个不同实际Tilemap及对应材质/层号。");
            if(inputs.Any(input=>input.tilemap.gameObject.scene!=binding.map.gameObject.scene||input.tilemap.layoutGrid!=binding.map.surface.grid))
                throw new InvalidOperationException("薄顶面有效层或预览层不属于当前Scene和实际Grid。");
            if(clip&&(domain==null||domain.ring==null||domain.ring.Length<3||domain.triangles==null||domain.triangles.Length<3||domain.area<=0))
                throw new InvalidOperationException("裁剪需Geometry模块成功构造的完整平台域。");
            if(!Finite(binding.transform.worldToLocalMatrix)||!Finite(binding.boundary.planeTolerance)||binding.boundary.planeTolerance<=0)
                throw new InvalidOperationException("表面变换或平面容差无效。");
        }

        private static List<CellSource> ReadSources(GroundSurfaceBinding binding,SurfaceLayerInput[] inputs,SurfaceMeshBuildResult stats)
        {
            var result=new List<CellSource>();
            var surface=binding.map.surface;
            if((long)surface.EditableCells.size.x*surface.EditableCells.size.y*inputs.Length>MaxInputCells)
                throw new InvalidOperationException("薄顶面输入超过本轮262144格上限。");
            foreach(var input in inputs)
            {
                var map=input.tilemap;
                foreach(var cell in surface.EditableCells.allPositionsWithin)
                {
                    TileBase tile=map.GetTile(cell);if(tile==null)continue;
                    ValidateStaticTile(map,cell,tile);
                    var sprite=map.GetSprite(cell);
                    if(sprite==null||sprite.texture==null)throw new InvalidOperationException("有Tile但缺实际Sprite/纹理："+map.name+" "+cell);
                    var vertices=sprite.vertices;var uv=sprite.uv;var triangles=sprite.triangles;
                    if(vertices==null||uv==null||vertices.Length!=uv.Length||vertices.Length<3||triangles==null||triangles.Length==0||triangles.Length%3!=0||
                        triangles.Any(index=>index>=vertices.Length)||vertices.Any(value=>!Finite(value.x)||!Finite(value.y))||uv.Any(value=>!Finite(value.x)||!Finite(value.y)))
                        throw new InvalidOperationException("实际Sprite几何/UV/索引无效："+map.name+" "+cell);
                    Matrix4x4 tileMatrix=map.GetTransformMatrix(cell);
                    if(!Finite(tileMatrix)||Mathf.Abs(tileMatrix.m30)>1e-6f||Mathf.Abs(tileMatrix.m31)>1e-6f||Mathf.Abs(tileMatrix.m32)>1e-6f||Mathf.Abs(tileMatrix.m33-1)>1e-6f)
                        throw new InvalidOperationException("只支持有限仿射的原生Tile显示矩阵："+map.name+" "+cell);
                    // GetCellCenterLocal自身处理Grid swizzle与tileAnchor。Sprite.vertices已包含PPU/pivot语义。
                    // 先在Sprite平面应用原生格矩阵，再取Tilemap朝向；此链需Native非交换哨兵A/B确认。
                    Matrix4x4 transform=binding.transform.worldToLocalMatrix*map.transform.localToWorldMatrix*
                        Matrix4x4.Translate(map.GetCellCenterLocal(cell))*map.orientationMatrix*tileMatrix;
                    if(!Finite(transform))throw new InvalidOperationException("Sprite→表面变换无效："+map.name+" "+cell);
                    Color color=map.GetColor(cell)*map.color;
                    if(!Finite(color.r)||!Finite(color.g)||!Finite(color.b)||!Finite(color.a))throw new InvalidOperationException("Tile实际颜色无效："+map.name+" "+cell);
                    // Native Tilemap在Linear项目中先转换最终顶点RGB，再以UNorm8传给Shader。
                    // 先把gamma色量化、或直接上传Float4，都不能重现原生Tint；Gamma项目保留原色值。
                    if(QualitySettings.activeColorSpace==ColorSpace.Linear)color=color.linear;
                    stats.inputCells++;stats.inputTriangles+=triangles.Length/3;
                    if(stats.inputTriangles>MaxInputTriangles)throw new InvalidOperationException("薄顶面输入超过本轮100万三角上限。");
                    result.Add(new CellSource{input=input,cell=cell,sprite=sprite,texture=sprite.texture,vertices=vertices,uv=uv,triangles=triangles,spriteToSurface=transform,color=color});
                }
            }
            return result;
        }
        private static void ValidateStaticTile(Tilemap map,Vector3Int cell,TileBase tile)
        {
            Type type=tile.GetType();
            if(type!=typeof(Tile)&&type!=typeof(RuleTile)&&type!=typeof(GroundAuthorTile)&&type!=typeof(GroundAuthorRuleTile))
                throw new InvalidOperationException("未知自定义Tile能力尚未适配，不能静默降级第一帧："+type.FullName+" "+cell);
            var rule=tile as RuleTile;
            if(map.GetAnimationFrameCount(cell)>1||rule!=null&&rule.m_TilingRules.Any(row=>row.m_Output.ToString()=="Animation"))
                throw new InvalidOperationException("动画Tile不属于本轮静态薄顶面输入："+map.name+" "+cell);
            if(map.GetObjectToInstantiate(cell)!=null||map.GetInstantiatedObject(cell)!=null)
                throw new InvalidOperationException("Tile含实例物件输出，不能仅用Sprite冒充完整表现："+map.name+" "+cell);
        }
        private static ClipVertex Vertex(CellSource source,int index)
        {
            Vector2 value=source.vertices[index];Vector3 position=source.spriteToSurface.MultiplyPoint3x4(new Vector3(value.x,value.y,0));
            if(!Finite(position.x)||!Finite(position.y)||!Finite(position.z))throw Invalid(source,"变换后顶点无效。");
            return new ClipVertex(position,source.uv[index],source.color);
        }
        private static Vector2[] AuthorQuad(GroundSurfaceBinding binding)
        {
            var surface=binding.map.surface;var b=surface.EditableCells;var grid=surface.grid;var frame=binding.transform.worldToLocalMatrix;
            Vector3[] corners={grid.CellToWorld(new Vector3Int(b.xMin,b.yMin,0)),grid.CellToWorld(new Vector3Int(b.xMax,b.yMin,0)),
                grid.CellToWorld(new Vector3Int(b.xMax,b.yMax,0)),grid.CellToWorld(new Vector3Int(b.xMin,b.yMax,0))};
            var result=corners.Select(point=>frame.MultiplyPoint3x4(point)).Select(point=>new Vector2(point.x,point.z)).ToArray();
            if(Math.Abs(SignedArea(result))<1e-10)throw new InvalidOperationException("作者Grid实际四角在表面空间退化。");
            return result;
        }
        private static List<ClipVertex> ClipAuthorQuad(ClipVertex a,ClipVertex b,ClipVertex c,Vector2[] quad,float tolerance)
        {
            var polygon=new List<ClipVertex>{a,b,c};double sign=SignedArea(quad)>0?1:-1;
            for(int edge=0;edge<quad.Length&&polygon.Count>0;edge++)
            {
                Vector2 start=quad[edge],end=quad[(edge+1)%quad.Length];double length=(end-start).magnitude;
                var next=new List<ClipVertex>();ClipVertex previous=polygon[polygon.Count-1];double previousDistance=Side(start,end,previous.position)*sign;
                foreach(var current in polygon)
                {
                    double distance=Side(start,end,current.position)*sign;bool inside=distance>=-tolerance*length,previousInside=previousDistance>=-tolerance*length;
                    if(inside!=previousInside)
                    {
                        double denominator=previousDistance-distance;
                        if(Math.Abs(denominator)>1e-16)next.Add(Interpolate(previous,current,(float)Math.Max(0,Math.Min(1,previousDistance/denominator))));
                    }
                    if(inside)next.Add(current);previous=current;previousDistance=distance;
                }
                polygon=next;
            }
            return polygon;
        }
        private static ClipVertex Interpolate(ClipVertex a,ClipVertex b,float t)=>new ClipVertex(Vector3.LerpUnclamped(a.position,b.position,t),Vector2.LerpUnclamped(a.uv,b.uv,t),Color.LerpUnclamped(a.color,b.color,t));
        private static void AppendTriangle(ClipVertex a,ClipVertex b,ClipVertex c,List<ClipVertex> vertices,List<int> indices)
        {int start=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);indices.Add(start);if(Cross(a.position,b.position,c.position)<0){indices.Add(start+1);indices.Add(start+2);}else{indices.Add(start+2);indices.Add(start+1);}}
        private static bool SameTriangle(ClipVertex a,ClipVertex b,ClipVertex c,List<ClipVertex> vertices,List<int> indices,float tolerance)
        {
            if(indices.Count!=3)return false;
            var source=new[]{a.position,b.position,c.position};var found=new bool[3];
            foreach(int index in indices)
            {
                int match=Array.FindIndex(source,point=>Vector3.Distance(point,vertices[index].position)<=tolerance);
                if(match<0||found[match])return false;found[match]=true;
            }
            return true;
        }
        private static double SignedArea(Vector2[] ring)
        {double twice=0;for(int i=0;i<ring.Length;i++){var a=ring[i];var b=ring[(i+1)%ring.Length];twice+=(double)a.x*b.y-(double)a.y*b.x;}return twice*.5;}
        private static double Side(Vector2 a,Vector2 b,Vector3 p)=>(double)(b.x-a.x)*(p.z-a.y)-(double)(b.y-a.y)*(p.x-a.x);
        private static double Cross(Vector3 a,Vector3 b,Vector3 c)=>(double)(b.x-a.x)*(c.z-a.z)-(double)(b.z-a.z)*(c.x-a.x);
        private static int FloorDiv(int value,int size){int quotient=value/size;return value<0&&value%size!=0?quotient-1:quotient;}
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        private static bool Finite(Matrix4x4 matrix){for(int i=0;i<16;i++)if(!Finite(matrix[i]))return false;return true;}
        private static InvalidOperationException Invalid(CellSource source,string message)=>new InvalidOperationException(message+" Tilemap="+source.input.tilemap.name+" cell="+source.cell);
    }
}
#endif
