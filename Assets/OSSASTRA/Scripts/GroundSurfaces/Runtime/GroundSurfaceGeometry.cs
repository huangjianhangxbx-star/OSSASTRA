/* 脚本：GroundSurfaceGeometry
 * 功能：由真实模型快照临时焊接并提取显式高度上沿，验证单环凹域、耳切及裁剪Sprite三角形。
 * 边界：纯数组运算；不修源模型、不取凸包、不访问Unity资产/场景、不创建另一套地面作者数据。
 * 约定：ring为二维CCW；输出三角索引映射到XZ时朝+Y。UV、颜色与层高只从输入三角重心插值。
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.GroundSurfaces
{
    public static class GroundSurfaceGeometry
    {
        const int MaximumRingVertices=4096,MaximumMeshVertices=262144,MaximumTriangles=524288;

        struct Edge : IEquatable<Edge>
        {
            public int a,b;
            public Edge(int x,int y){a=Math.Min(x,y);b=Math.Max(x,y);}
            public bool Equals(Edge other)=>a==other.a&&b==other.b;
            public override bool Equals(object value)=>value is Edge other&&Equals(other);
            public override int GetHashCode()=>unchecked(a*397^b);
        }
        struct Face : IEquatable<Face>
        {
            int a,b,c;
            public Face(int x,int y,int z){a=Math.Min(x,Math.Min(y,z));c=Math.Max(x,Math.Max(y,z));b=x+y+z-a-c;}
            public bool Equals(Face other)=>a==other.a&&b==other.b&&c==other.c;
            public override bool Equals(object value)=>value is Face other&&Equals(other);
            public override int GetHashCode()=>unchecked((a*397^b)*397^c);
        }
        struct Bucket : IEquatable<Bucket>
        {
            public long x,y,z;
            public Bucket(long x,long y,long z){this.x=x;this.y=y;this.z=z;}
            public bool Equals(Bucket other)=>x==other.x&&y==other.y&&z==other.z;
            public override bool Equals(object value)=>value is Bucket other&&Equals(other);
            public override int GetHashCode()=>unchecked((x.GetHashCode()*397^y.GetHashCode())*397^z.GetHashCode());
        }
        sealed class EdgeUse
        {public int count;public string sourceId;public int element;}
        struct Welded
        {public Vector3 point;public string sourceId;public int element;}
        struct WorkVertex
        {
            public double x,z,a,b,c;
            public WorkVertex(double x,double z,double a,double b,double c){this.x=x;this.z=z;this.a=a;this.b=b;this.c=c;}
            public static WorkVertex Lerp(WorkVertex p,WorkVertex q,double t)=>new WorkVertex(p.x+(q.x-p.x)*t,p.z+(q.z-p.z)*t,p.a+(q.a-p.a)*t,p.b+(q.b-p.b)*t,p.c+(q.c-p.c)*t);
        }

        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
        static bool Finite(Vector2 p)=>Finite(p.x)&&Finite(p.y);
        static bool Finite(Vector3 p)=>Finite(p.x)&&Finite(p.y)&&Finite(p.z);
        static double Cross(Vector2 a,Vector2 b,Vector2 c)=>((double)b.x-a.x)*(c.y-a.y)-((double)b.y-a.y)*(c.x-a.x);
        static double DistanceSquared(Vector2 a,Vector2 b)=>((double)a.x-b.x)*(a.x-b.x)+((double)a.y-b.y)*(a.y-b.y);
        static bool Fail(out GeometryIssue issue,string code,string message,string source="",int element=-1)
        {issue=new GeometryIssue(code,message,source,element);return false;}
        static bool ValidOptions(GeometryOptions options,out GeometryIssue issue)
        {
            issue=null;
            if(options==null||!Finite(options.weldTolerance)||options.weldTolerance<=0||!Finite(options.planeTolerance)||options.planeTolerance<0||!Finite(options.areaTolerance)||options.areaTolerance<=0)
                return Fail(out issue,"InvalidTolerance","容差必须有限，焊接/面积为正，平面容差非负。");
            return true;
        }
        static bool OnSegment(Vector2 p,Vector2 a,Vector2 b,double epsilon)
        {
            double dx=(double)b.x-a.x,dz=(double)b.y-a.y,length=Math.Sqrt(dx*dx+dz*dz);
            if(length==0)return DistanceSquared(p,a)<=epsilon*epsilon;
            if(Math.Abs(Cross(a,b,p))>epsilon*length)return false;
            double dot=((double)p.x-a.x)*dx+((double)p.y-a.y)*dz;
            return dot>=-epsilon*length&&dot<=length*length+epsilon*length;
        }
        static bool SegmentsMeet(Vector2 a,Vector2 b,Vector2 c,Vector2 d,double epsilon)
        {
            if(Math.Max(a.x,b.x)+epsilon<Math.Min(c.x,d.x)||Math.Max(c.x,d.x)+epsilon<Math.Min(a.x,b.x)||
               Math.Max(a.y,b.y)+epsilon<Math.Min(c.y,d.y)||Math.Max(c.y,d.y)+epsilon<Math.Min(a.y,b.y))return false;
            if(OnSegment(a,c,d,epsilon)||OnSegment(b,c,d,epsilon)||OnSegment(c,a,b,epsilon)||OnSegment(d,a,b,epsilon))return true;
            double abC=Cross(a,b,c),abD=Cross(a,b,d),cdA=Cross(c,d,a),cdB=Cross(c,d,b);
            return (abC>0)!=(abD>0)&&(cdA>0)!=(cdB>0);
        }
        static bool InTriangle(Vector2 p,Vector2 a,Vector2 b,Vector2 c,double epsilon)
        {
            double x=Cross(a,b,p),y=Cross(b,c,p),z=Cross(c,a,p);
            return (x>=-epsilon&&y>=-epsilon&&z>=-epsilon)||(x<=epsilon&&y<=epsilon&&z<=epsilon);
        }

        public static bool TryBuildDomain(Vector2[] ring,GeometryOptions options,out SurfaceDomain domain,out GeometryIssue issue)
        {
            domain=null;issue=null;
            if(!ValidOptions(options,out issue))return false;
            if(ring==null||ring.Length<3||ring.Length>MaximumRingVertices)return Fail(out issue,"InvalidRingSize","单环需要3—4096个点，末点不重复起点。");
            var points=(Vector2[])ring.Clone();double tolerance=options.weldTolerance;
            for(int i=0;i<points.Length;i++)
            {
                if(!Finite(points[i]))return Fail(out issue,"NonFinitePoint","连接环有非有限坐标。","",i);
                for(int j=0;j<i;j++)if(DistanceSquared(points[i],points[j])<=tolerance*tolerance)
                    return Fail(out issue,"RepeatedRingVertex","连接环包含重复或小于焊接容差的顶点；不自动合并轮廓。","",i);
            }
            for(int i=0;i<points.Length;i++)
            {
                int next=(i+1)%points.Length;
                for(int j=i+1;j<points.Length;j++)
                {
                    int other=(j+1)%points.Length;if(j==next||other==i)continue;
                    if(SegmentsMeet(points[i],points[next],points[j],points[other],tolerance))
                        return Fail(out issue,"SelfIntersection","连接环存在自交、自触或非相邻重叠边。","",i);
                }
                // 相邻边可共线向前，不能沿原路折返而制造重叠。
                Vector2 previous=points[(i+points.Length-1)%points.Length];
                if(Math.Abs(Cross(previous,points[i],points[next]))<=options.areaTolerance&&Vector2.Dot(previous-points[i],points[next]-points[i])>0)
                    return Fail(out issue,"OverlappingAdjacentEdges","相邻边沿原路折返重叠。","",i);
            }
            double signed=SignedArea(points);
            if(Math.Abs(signed)<=options.areaTolerance)return Fail(out issue,"ZeroAreaRing","连接环面积为零或低于登记阈值。");
            if(signed<0)Array.Reverse(points);
            var active=new List<int>(points.Length);for(int i=0;i<points.Length;i++)active.Add(i);
            bool removed=true;
            while(removed&&active.Count>3)
            {
                removed=false;
                for(int i=0;i<active.Count;i++)
                {
                    int a=active[(i+active.Count-1)%active.Count],b=active[i],c=active[(i+1)%active.Count];
                    if(Math.Abs(Cross(points[a],points[b],points[c]))<=options.areaTolerance&&OnSegment(points[b],points[a],points[c],1e-10))
                    {active.RemoveAt(i);removed=true;break;}
                }
            }
            var triangles=new List<int>((active.Count-2)*3);
            while(active.Count>3)
            {
                bool ear=false;
                for(int i=0;i<active.Count;i++)
                {
                    int a=active[(i+active.Count-1)%active.Count],b=active[i],c=active[(i+1)%active.Count];
                    if(Cross(points[a],points[b],points[c])<=options.areaTolerance)continue;
                    bool occupied=false;
                    for(int j=0;j<active.Count;j++)
                    {int p=active[j];if(p!=a&&p!=b&&p!=c&&InTriangle(points[p],points[a],points[b],points[c],options.areaTolerance)){occupied=true;break;}}
                    if(occupied)continue;
                    triangles.Add(a);triangles.Add(c);triangles.Add(b);active.RemoveAt(i);ear=true;break;
                }
                if(!ear)return Fail(out issue,"TriangulationFailed","找不到合法耳片；没有生成中心扇形或部分顶盖。");
            }
            if(Cross(points[active[0]],points[active[1]],points[active[2]])<=options.areaTolerance)
                return Fail(out issue,"DegenerateFinalTriangle","最终耳片退化；没有输出半成品域。");
            triangles.Add(active[0]);triangles.Add(active[2]);triangles.Add(active[1]);
            double area=Math.Abs(signed),sum=0;
            var triangleBounds=new Rect[triangles.Count/3];
            for(int i=0;i<triangles.Count;i+=3)
            {
                Vector2 a=points[triangles[i]],b=points[triangles[i+1]],c=points[triangles[i+2]];
                sum+=Math.Abs(Cross(a,b,c))*.5;triangleBounds[i/3]=Bounds(a,b,c);
            }
            if(Math.Abs(sum-area)>Math.Max(options.areaTolerance*points.Length,area*1e-10))
                return Fail(out issue,"AreaMismatch","耳切面积与连接环不一致，拒绝该结果。");
            Vector2 min=points[0],max=points[0];for(int i=1;i<points.Length;i++){min=Vector2.Min(min,points[i]);max=Vector2.Max(max,points[i]);}
            domain=new SurfaceDomain{ring=points,triangles=triangles.ToArray(),bounds=Rect.MinMaxRect(min.x,min.y,max.x,max.y),area=area,
                tolerances=options.Copy(),triangleBounds=triangleBounds,validated=true};return true;
        }
        static double SignedArea(Vector2[] ring)
        {
            // 相对首点求面积，避免大平移产生两个巨大乘积再相减。
            Vector2 origin=ring[0];double value=0;for(int i=1;i+1<ring.Length;i++)value+=Cross(origin,ring[i],ring[i+1]);return value*.5;
        }
        static Rect Bounds(Vector2 a,Vector2 b,Vector2 c)=>Rect.MinMaxRect(Math.Min(a.x,Math.Min(b.x,c.x)),Math.Min(a.y,Math.Min(b.y,c.y)),Math.Max(a.x,Math.Max(b.x,c.x)),Math.Max(a.y,Math.Max(b.y,c.y)));

        static bool ValidTransform(Matrix4x4 m)
        {
            for(int row=0;row<4;row++)for(int col=0;col<4;col++)if(!Finite(m[row,col]))return false;
            if(Math.Abs(m[3,0])>1e-6||Math.Abs(m[3,1])>1e-6||Math.Abs(m[3,2])>1e-6||Math.Abs(m[3,3]-1)>1e-6)return false;
            var x=new Vector3(m[0,0],m[1,0],m[2,0]);var y=new Vector3(m[0,1],m[1,1],m[2,1]);var z=new Vector3(m[0,2],m[1,2],m[2,2]);
            if(x.sqrMagnitude<1e-16||y.sqrMagnitude<1e-16||z.sqrMagnitude<1e-16||y.y<=0||m.determinant<=0)return false;
            return Math.Abs(x.y)<=x.magnitude*1e-5&&Math.Abs(z.y)<=z.magnitude*1e-5&&Math.Abs(y.x)<=y.magnitude*1e-5&&Math.Abs(y.z)<=y.magnitude*1e-5&&Math.Abs(Vector3.Dot(x,z))<=x.magnitude*z.magnitude*1e-5;
        }
        static bool TryBucket(Vector3 p,double tolerance,out Bucket bucket)
        {
            double x=Math.Floor(p.x/tolerance),y=Math.Floor(p.y/tolerance),z=Math.Floor(p.z/tolerance);
            const double limit=9e18;
            if(Math.Abs(x)>=limit||Math.Abs(y)>=limit||Math.Abs(z)>=limit){bucket=default;return false;}
            bucket=new Bucket((long)x,(long)y,(long)z);return true;
        }
        static int Weld(Vector3 point,Bucket bucket,double tolerance,List<Welded> vertices,Dictionary<Bucket,List<int>> buckets,string source,int element)
        {
            int nearest=-1;double nearestDistance=tolerance*tolerance;
            for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)
            {
                if(!buckets.TryGetValue(new Bucket(bucket.x+x,bucket.y+y,bucket.z+z),out var candidates))continue;
                foreach(int candidate in candidates)
                {
                    Vector3 p=vertices[candidate].point;double dx=(double)p.x-point.x,dy=(double)p.y-point.y,dz=(double)p.z-point.z;double distance=dx*dx+dy*dy+dz*dz;
                    if(distance<=nearestDistance&&(nearest<0||distance<nearestDistance||candidate<nearest)){nearest=candidate;nearestDistance=distance;}
                }
            }
            if(nearest>=0)return nearest;
            int index=vertices.Count;vertices.Add(new Welded{point=point,sourceId=source,element=element});
            if(!buckets.TryGetValue(bucket,out var own)){own=new List<int>();buckets.Add(bucket,own);}own.Add(index);return index;
        }
        static void AddEdge(Dictionary<Edge,EdgeUse> edges,int a,int b,string source,int element)
        {var key=new Edge(a,b);if(edges.TryGetValue(key,out var value))value.count++;else edges.Add(key,new EdgeUse{count=1,sourceId=source,element=element});}

        public static bool TryExtractTopRim(MeshSnapshot[] snapshots,float topY,GeometryOptions options,out SurfaceDomain domain,out GeometryIssue issue)
        {
            domain=null;issue=null;if(!ValidOptions(options,out issue))return false;
            if(snapshots==null||snapshots.Length==0||!Finite(topY))return Fail(out issue,"MissingMeshInput","需要真实Mesh快照与有限的显式接合高度。");
            var vertices=new List<Welded>();var buckets=new Dictionary<Bucket,List<int>>();var edges=new Dictionary<Edge,EdgeUse>();var faces=new HashSet<Face>();
            long inputVertices=0,inputTriangles=0;
            foreach(var snapshot in snapshots)
            {
                if(snapshot==null||snapshot.vertices==null||snapshot.triangles==null||snapshot.vertices.Length==0||snapshot.triangles.Length==0||snapshot.triangles.Length%3!=0)
                    return Fail(out issue,"InvalidMeshInput","Mesh顶点/三角索引为空或不完整。",snapshot==null?"":snapshot.sourceId);
                inputVertices+=snapshot.vertices.Length;inputTriangles+=snapshot.triangles.Length/3;
                if(inputVertices>MaximumMeshVertices||inputTriangles>MaximumTriangles)return Fail(out issue,"InputLimitExceeded","输入超过本轮有界提边容量。",snapshot.sourceId);
                if(!ValidTransform(snapshot.meshToSurface))return Fail(out issue,"UnsupportedTransform","仅支持水平平移、Yaw与正缩放；倾斜/反射/剪切/非有限变换拒绝。",snapshot.sourceId);
                var mapping=new int[snapshot.vertices.Length];
                for(int i=0;i<mapping.Length;i++)
                {
                    Vector3 p=snapshot.meshToSurface.MultiplyPoint3x4(snapshot.vertices[i]);
                    if(!Finite(p)||!TryBucket(p,options.weldTolerance,out var bucket))return Fail(out issue,"NonFiniteOrUnboundedVertex","顶点非有限或超出容差哈希的数值范围。",snapshot.sourceId,i);
                    mapping[i]=Weld(p,bucket,options.weldTolerance,vertices,buckets,snapshot.sourceId,i);
                }
                for(int i=0;i<snapshot.triangles.Length;i+=3)
                {
                    int x=snapshot.triangles[i],y=snapshot.triangles[i+1],z=snapshot.triangles[i+2];
                    if(x<0||y<0||z<0||x>=mapping.Length||y>=mapping.Length||z>=mapping.Length)return Fail(out issue,"InvalidTriangleIndex","三角形引用不存在的顶点。",snapshot.sourceId,i/3);
                    int a=mapping[x],b=mapping[y],c=mapping[z];
                    if(a==b||b==c||c==a)return Fail(out issue,"CollapsedTriangle","三角形在登记焊接容差内塌缩，不静默删除。",snapshot.sourceId,i/3);
                    Vector3 p=vertices[a].point,q=vertices[b].point,r=vertices[c].point;
                    double ux=(double)q.x-p.x,uy=(double)q.y-p.y,uz=(double)q.z-p.z,vx=(double)r.x-p.x,vy=(double)r.y-p.y,vz=(double)r.z-p.z;
                    double cx=uy*vz-uz*vy,cy=uz*vx-ux*vz,cz=ux*vy-uy*vx;
                    if(cx*cx+cy*cy+cz*cz<=4*options.areaTolerance*options.areaTolerance)return Fail(out issue,"ZeroAreaTriangle","侧壁包含零面积三角形。",snapshot.sourceId,i/3);
                    if(!faces.Add(new Face(a,b,c)))return Fail(out issue,"DuplicateTriangle","侧壁包含重复三角形，不能当作内部闭合边。",snapshot.sourceId,i/3);
                    AddEdge(edges,a,b,snapshot.sourceId,i/3);AddEdge(edges,b,c,snapshot.sourceId,i/3);AddEdge(edges,c,a,snapshot.sourceId,i/3);
                }
            }
            var adjacent=new Dictionary<int,List<int>>();
            foreach(var entry in edges)
            {
                if(entry.Value.count>2)return Fail(out issue,"NonManifoldEdge","同一焊接边被超过两片三角形使用。",entry.Value.sourceId,entry.Value.element);
                if(entry.Value.count!=1)continue;
                if(!adjacent.TryGetValue(entry.Key.a,out var a)){a=new List<int>();adjacent.Add(entry.Key.a,a);}a.Add(entry.Key.b);
                if(!adjacent.TryGetValue(entry.Key.b,out var b)){b=new List<int>();adjacent.Add(entry.Key.b,b);}b.Add(entry.Key.a);
            }
            if(adjacent.Count==0)return Fail(out issue,"NoOpenRim","没有开放边；请明确选择侧壁子网格，不能自动删掉源顶盖。");
            foreach(var pair in adjacent)if(pair.Value.Count!=2)return Fail(out issue,"OpenOrBranchedBoundary","开放边不是degree2闭环，存在断口或分叉。",vertices[pair.Key].sourceId,vertices[pair.Key].element);
            var visited=new HashSet<int>();List<int> selected=null;bool crossesHeight=false;
            for(int start=0;start<vertices.Count;start++)
            {
                if(!adjacent.ContainsKey(start)||visited.Contains(start))continue;
                var loop=new List<int>();int previous=-1,current=start;
                do
                {
                    if(visited.Contains(current)||loop.Count>MaximumRingVertices)return Fail(out issue,"InvalidBoundaryLoop","闭环重复、过长或遇到其他已访问环。",vertices[current].sourceId,vertices[current].element);
                    visited.Add(current);loop.Add(current);var neighbors=adjacent[current];int next=neighbors[0]==previous?neighbors[1]:neighbors[0];previous=current;current=next;
                }while(current!=start);
                float minY=float.PositiveInfinity,maxY=float.NegativeInfinity;double residual=0;
                foreach(int index in loop){float y=vertices[index].point.y;minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);residual=Math.Max(residual,Math.Abs((double)y-topY));}
                crossesHeight|=minY<=topY+options.planeTolerance&&maxY>=topY-options.planeTolerance;
                if(residual>options.planeTolerance)continue;
                if(selected!=null)return Fail(out issue,"MultipleTopRims","显式高度存在多个闭环；本轮不支持内孔或分离平台，未擅取最大环。",vertices[start].sourceId,vertices[start].element);
                selected=loop;
            }
            if(selected==null)return Fail(out issue,crossesHeight?"NonPlanarTopRim":"NoRimAtHeight",crossesHeight?"候选开放环超过平面残差阈值，不投影修正。":"显式高度没有匹配的开放环，未擅选最高环。");
            var ring=new Vector2[selected.Count];for(int i=0;i<ring.Length;i++){Vector3 p=vertices[selected[i]].point;ring[i]=new Vector2(p.x,p.z);}
            if(!TryBuildDomain(ring,options,out domain,out issue))
            {if(issue!=null&&string.IsNullOrEmpty(issue.sourceId))issue.sourceId=vertices[selected[0]].sourceId;return false;}return true;
        }

        public static bool ContainsXZ(SurfaceDomain domain,Vector2 point)
        {
            if(domain==null||!domain.validated||!Finite(point))return false;
            double epsilon=domain.tolerances.weldTolerance;
            if(point.x<domain.bounds.xMin-epsilon||point.x>domain.bounds.xMax+epsilon||point.y<domain.bounds.yMin-epsilon||point.y>domain.bounds.yMax+epsilon)return false;
            bool inside=false;var ring=domain.ring;
            for(int i=0,j=ring.Length-1;i<ring.Length;j=i++)
            {
                Vector2 a=ring[j],b=ring[i];if(OnSegment(point,a,b,epsilon))return true;
                if((a.y>point.y)!=(b.y>point.y)&&point.x<(double)(b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
            }
            return inside;
        }
        static bool Overlap(Rect a,Rect b,double epsilon)=>a.xMin<=b.xMax+epsilon&&a.xMax>=b.xMin-epsilon&&a.yMin<=b.yMax+epsilon&&a.yMax>=b.yMin-epsilon;
        static bool ValidVertex(ClipVertex p)=>Finite(p.position)&&Finite(p.uv)&&Finite(p.color.r)&&Finite(p.color.g)&&Finite(p.color.b)&&Finite(p.color.a);
        static bool FullyInside(SurfaceDomain domain,Vector2 a,Vector2 b,Vector2 c,Rect triangleBounds)
        {
            if(!ContainsXZ(domain,a)||!ContainsXZ(domain,b)||!ContainsXZ(domain,c))return false;
            double epsilon=domain.tolerances.weldTolerance;var ring=domain.ring;
            for(int i=0;i<ring.Length;i++)
            {
                Vector2 p=ring[i],q=ring[(i+1)%ring.Length];
                if(!Overlap(triangleBounds,Rect.MinMaxRect(Math.Min(p.x,q.x),Math.Min(p.y,q.y),Math.Max(p.x,q.x),Math.Max(p.y,q.y)),epsilon))continue;
                if(SegmentsMeet(a,b,p,q,epsilon)||SegmentsMeet(b,c,p,q,epsilon)||SegmentsMeet(c,a,p,q,epsilon)||InTriangle(p,a,b,c,domain.tolerances.areaTolerance))return false;
            }
            return true;
        }
        static void AddTriangle(List<int> indices,int a,int b,int c,double cross)
        {indices.Add(a);if(cross>0){indices.Add(c);indices.Add(b);}else{indices.Add(b);indices.Add(c);}}

        public static void ClipTriangle(ClipVertex a,ClipVertex b,ClipVertex c,SurfaceDomain domain,List<ClipVertex> outputVertices,List<int> outputIndices)
        {
            if(domain==null||!domain.validated)throw new ArgumentException("裁剪需要通过TryBuildDomain/TryExtractTopRim验证的域。",nameof(domain));
            if(outputVertices==null||outputIndices==null)throw new ArgumentNullException("裁剪输出列表不能为空。");
            if(!ValidVertex(a)||!ValidVertex(b)||!ValidVertex(c))throw new ArgumentException("输入三角位置/UV/颜色必须有限。");
            Vector2 pa=new Vector2(a.position.x,a.position.z),pb=new Vector2(b.position.x,b.position.z),pc=new Vector2(c.position.x,c.position.z);
            double cross=Cross(pa,pb,pc),areaTolerance=domain.tolerances.areaTolerance;
            if(Math.Abs(cross)*.5<=areaTolerance)return;
            Rect bounds=Bounds(pa,pb,pc);if(!Overlap(bounds,domain.bounds,0))return;
            // 凹域的三个点均在内部不够：跨过凹口的边必须继续裁剪。
            if(FullyInside(domain,pa,pb,pc,bounds))
            {int n=outputVertices.Count;outputVertices.Add(a);outputVertices.Add(b);outputVertices.Add(c);AddTriangle(outputIndices,n,n+1,n+2,cross);return;}
            for(int t=0;t<domain.triangles.Length;t+=3)
            {
                if(!Overlap(bounds,domain.triangleBounds[t/3],0))continue;
                var polygon=new List<WorkVertex>{new WorkVertex(pa.x,pa.y,1,0,0),new WorkVertex(pb.x,pb.y,0,1,0),new WorkVertex(pc.x,pc.y,0,0,1)};
                // 域三角输出朝+Y，二维索引为CW；裁剪器统一临时转为CCW。
                Vector2 x=domain.ring[domain.triangles[t]],y=domain.ring[domain.triangles[t+2]],z=domain.ring[domain.triangles[t+1]];
                polygon=ClipEdge(polygon,x,y,areaTolerance);polygon=ClipEdge(polygon,y,z,areaTolerance);polygon=ClipEdge(polygon,z,x,areaTolerance);
                CleanPolygon(polygon,areaTolerance);if(polygon.Count<3)continue;
                double area=0;for(int i=1;i+1<polygon.Count;i++)area+=WorkCross(polygon[0],polygon[i],polygon[i+1]);
                if(Math.Abs(area)*.5<=areaTolerance)continue;
                int start=outputVertices.Count;
                foreach(var p in polygon)
                {
                    // 从源三角一次插值，避免多轮浮点UV裁剪累积；不按输出Bounds重新铺图。
                    outputVertices.Add(new ClipVertex(
                        new Vector3((float)p.x,(float)(a.position.y*p.a+b.position.y*p.b+c.position.y*p.c),(float)p.z),
                        new Vector2((float)(a.uv.x*p.a+b.uv.x*p.b+c.uv.x*p.c),(float)(a.uv.y*p.a+b.uv.y*p.b+c.uv.y*p.c)),
                        new Color((float)(a.color.r*p.a+b.color.r*p.b+c.color.r*p.c),(float)(a.color.g*p.a+b.color.g*p.b+c.color.g*p.c),(float)(a.color.b*p.a+b.color.b*p.b+c.color.b*p.c),(float)(a.color.a*p.a+b.color.a*p.b+c.color.a*p.c))));
                }
                for(int i=1;i+1<polygon.Count;i++)
                {double value=WorkCross(polygon[0],polygon[i],polygon[i+1]);if(Math.Abs(value)*.5>areaTolerance)AddTriangle(outputIndices,start,start+i,start+i+1,value);}
            }
        }
        static double WorkCross(WorkVertex a,WorkVertex b,WorkVertex c)=>(b.x-a.x)*(c.z-a.z)-(b.z-a.z)*(c.x-a.x);
        static List<WorkVertex> ClipEdge(List<WorkVertex> input,Vector2 a,Vector2 b,double epsilon)
        {
            var output=new List<WorkVertex>(input.Count+1);if(input.Count==0)return output;
            WorkVertex previous=input[input.Count-1];double pd=((double)b.x-a.x)*(previous.z-a.y)-((double)b.y-a.y)*(previous.x-a.x);bool previousInside=pd>=-epsilon;
            foreach(var current in input)
            {
                double cd=((double)b.x-a.x)*(current.z-a.y)-((double)b.y-a.y)*(current.x-a.x);bool currentInside=cd>=-epsilon;
                if(currentInside!=previousInside)
                {double denominator=pd-cd;if(Math.Abs(denominator)>1e-30)output.Add(WorkVertex.Lerp(previous,current,Math.Max(0,Math.Min(1,pd/denominator))));}
                if(currentInside)output.Add(current);previous=current;pd=cd;previousInside=currentInside;
            }
            return output;
        }
        static void CleanPolygon(List<WorkVertex> polygon,double areaTolerance)
        {
            bool removed=true;
            while(removed&&polygon.Count>=3)
            {
                removed=false;
                for(int i=0;i<polygon.Count;i++)
                {
                    var p=polygon[(i+polygon.Count-1)%polygon.Count];var q=polygon[i];var r=polygon[(i+1)%polygon.Count];
                    double dx=p.x-q.x,dz=p.z-q.z;
                    if(dx*dx+dz*dz<=1e-20||Math.Abs(WorkCross(p,q,r))<=areaTolerance)
                    {polygon.RemoveAt(i);removed=true;break;}
                }
            }
        }
    }
}
