/* 脚本：GroundSurfaceModelReader
 * 功能：从平台显式登记的已保存侧壁Mesh只读构造快照、源内容指纹及开放上沿派生域。
 * 负责：真实GUID/子资产身份、全子网格Triangle拓扑、层级正缩放、水平TRS与显式高度校验。
 * 不负责：保存/修改Mesh、提交Boundary、创建缓存/碰撞或猜测最高环；失败交由作者事务保留旧状态。
 * 协作：GroundSurfaceGeometry处理焊接/拓扑/凹域；作者服务统一提交环、指纹和资源版本。
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ossastra.GroundSurfaces.Editor
{
    public static class GroundSurfaceModelReader
    {
        const string FingerprintVersion="Ossastra.GroundSurfaceModelReader/1";
        const int MaximumFilters=256,MaximumVertices=262144,MaximumTriangles=524288;

        sealed class Source
        {
            public string guid;
            public long localId;
            public Vector3[] vertices;
            public int[][] indices;
            public Matrix4x4 toSurface;
        }
        sealed class Input
        {
            public float topY;
            public GeometryOptions options;
            public Matrix4x4 surfaceToWorld;
            public readonly List<Source> sources=new List<Source>();
            public readonly List<MeshSnapshot> snapshots=new List<MeshSnapshot>();
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static bool Finite(Vector3 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z);
        static InvalidOperationException Error(string code,string detail,string source="")=>
            new InvalidOperationException("模型读回 ["+code+"] "+detail+(string.IsNullOrEmpty(source)?"":"；来源="+source));

        static void RequireHorizontal(Matrix4x4 matrix,string source)
        {
            for(int row=0;row<4;row++)for(int column=0;column<4;column++)
                if(!Finite(matrix[row,column]))throw Error("NonFiniteTransform","变换矩阵包含非有限数值。",source);
            var x=new Vector3(matrix[0,0],matrix[1,0],matrix[2,0]);
            var y=new Vector3(matrix[0,1],matrix[1,1],matrix[2,1]);
            var z=new Vector3(matrix[0,2],matrix[1,2],matrix[2,2]);
            if(Math.Abs(matrix[3,0])>1e-6||Math.Abs(matrix[3,1])>1e-6||Math.Abs(matrix[3,2])>1e-6||Math.Abs(matrix[3,3]-1)>1e-6||
               x.sqrMagnitude<1e-16||y.sqrMagnitude<1e-16||z.sqrMagnitude<1e-16||y.y<=0||!Finite(matrix.determinant)||matrix.determinant<=0||
               Math.Abs(x.y)>x.magnitude*1e-5||Math.Abs(z.y)>z.magnitude*1e-5||Math.Abs(y.x)>y.magnitude*1e-5||Math.Abs(y.z)>y.magnitude*1e-5||
               Math.Abs(Vector3.Dot(x,z))>x.magnitude*z.magnitude*1e-5)
                throw Error("UnsupportedTransform","仅支持水平平移、Yaw和正缩放，不支持倾斜、剪切或反射。",source);
        }
        static void RequirePositiveHierarchy(Transform transform,string source)
        {
            // 两个负轴可等价于Yaw旋转；仅检查合成矩阵行列式不能识别这种作者输入。
            for(var current=transform;current!=null;current=current.parent)
            {
                var scale=current.localScale;
                if(!Finite(scale)||scale.x<=0||scale.y<=0||scale.z<=0)
                    throw Error("NonPositiveHierarchyScale","侧壁至场景根的localScale必须全正；问题节点="+current.name,source);
            }
        }

        static Input Collect(GroundSurfaceBinding binding,Mesh overrideFirstMesh)
        {
            if(binding==null)throw Error("MissingBinding","没有指定平台Binding。");
            if(binding.boundary==null)throw Error("MissingBoundary","需要Boundary中的显式localTopY及容差，不自动选择最高点。");
            if(binding.sideWalls==null||binding.sideWalls.Length==0||binding.sideWalls.Length>MaximumFilters)
                throw Error("InvalidWallCount","需要1—256个明确登记的侧壁MeshFilter。");
            var boundary=binding.boundary;
            if(!Finite(boundary.localTopY)||!Finite(boundary.weldTolerance)||boundary.weldTolerance<=0||!Finite(boundary.planeTolerance)||boundary.planeTolerance<0)
                throw Error("InvalidBoundarySettings","显式高度和容差必须有限，焊接为正，平面容差非负。");
            var input=new Input{topY=boundary.localTopY,options=new GeometryOptions{weldTolerance=boundary.weldTolerance,planeTolerance=boundary.planeTolerance},surfaceToWorld=binding.transform.localToWorldMatrix};
            RequirePositiveHierarchy(binding.transform,binding.name);
            RequireHorizontal(input.surfaceToWorld,binding.name+"/平台世界变换");
            var seen=new HashSet<MeshFilter>();bool usedOverride=false;long vertexCount=0,triangleCount=0;
            foreach(var filter in binding.sideWalls)
            {
                if(filter==null)throw Error("MissingWallFilter","sideWalls包含丢失的MeshFilter，未静默跳过。");
                if(!filter.transform.IsChildOf(binding.transform))throw Error("ForeignWallFilter","侧壁必须属于当前Binding层级，未读取外部对象。",filter.name);
                if(!seen.Add(filter))throw Error("DuplicateWallFilter","同一MeshFilter被重复登记，未重复消费。",filter.name);
                RequirePositiveHierarchy(filter.transform,filter.name);
                RequireHorizontal(filter.transform.localToWorldMatrix,filter.name+"/侧壁世界变换");
                var toSurface=binding.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                RequireHorizontal(toSurface,filter.name+"/表面局部变换");
                Mesh mesh=overrideFirstMesh!=null&&!usedOverride?overrideFirstMesh:filter.sharedMesh;usedOverride=true;
                if(mesh==null)throw Error("MissingSourceMesh","侧壁没有sharedMesh。",filter.name);
                string path=AssetDatabase.GetAssetPath(mesh);
                if(string.IsNullOrEmpty(path)||!AssetDatabase.Contains(mesh)||AssetDatabase.IsValidFolder(path)||
                   !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out string guid,out long localId)||string.IsNullOrEmpty(guid)||
                   AssetDatabase.AssetPathToGUID(path)!=guid)
                    throw Error("UnsavedSourceMesh","源Mesh必须先保存为可由AssetDatabase定位的实际资产，不能以瞬态网格冒充模型接入。",filter.name);
                string identity=path+" | GUID="+guid+" localId="+localId;
                if(!mesh.isReadable)throw Error("UnreadableSourceMesh","源Mesh不可读；Reader不修改Importer或自动重导。",identity);
                if(mesh.vertexCount==0||mesh.subMeshCount==0)throw Error("EmptySourceMesh","源Mesh没有可提取的几何。",identity);
                var vertices=mesh.vertices;
                vertexCount+=vertices.Length;if(vertexCount>MaximumVertices)throw Error("InputLimitExceeded","读取顶点超过本轮有界容量。",identity);
                for(int i=0;i<vertices.Length;i++)if(!Finite(vertices[i]))throw Error("NonFiniteSourceVertex","源Mesh顶点非有限，顶点="+i,identity);
                var source=new Source{guid=guid,localId=localId,vertices=vertices,toSurface=toSurface,indices=new int[mesh.subMeshCount][]};
                for(int submesh=0;submesh<mesh.subMeshCount;submesh++)
                {
                    string location=identity+" #submesh="+submesh;
                    if(mesh.GetTopology(submesh)!=MeshTopology.Triangles)throw Error("UnsupportedTopology","仅支持Triangle子网格，不静默转换Lines/Quads等。",location);
                    var indices=mesh.GetTriangles(submesh);source.indices[submesh]=indices;
                    if(indices.Length==0||indices.Length%3!=0)throw Error("EmptyOrInvalidSubmesh","子网格没有完整三角形，需明确指定有效侧壁。",location);
                    triangleCount+=indices.Length/3;if(triangleCount>MaximumTriangles)throw Error("InputLimitExceeded","读取三角形超过本轮有界容量。",location);
                    for(int i=0;i<indices.Length;i++)if(indices[i]<0||indices[i]>=vertices.Length)throw Error("InvalidSourceIndex","三角索引越界，索引项="+i,location);
                    input.snapshots.Add(new MeshSnapshot{vertices=vertices,triangles=indices,meshToSurface=toSurface,sourceId=location,submesh=submesh});
                }
                input.sources.Add(source);
            }
            return input;
        }

        /// <summary>哈希真实原生顶点/索引/变换及接合设置；不以InstanceID或顶点计数代替内容。</summary>
        public static string Fingerprint(GroundSurfaceBinding binding,Mesh overrideFirstMesh=null)
        {
            var input=Collect(binding,overrideFirstMesh);
            using(var stream=new MemoryStream())
            {
                using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
                {
                    writer.Write(FingerprintVersion);writer.Write(input.topY);writer.Write(input.options.weldTolerance);writer.Write(input.options.planeTolerance);writer.Write(input.options.areaTolerance);
                    WriteMatrix(writer,input.surfaceToWorld);writer.Write(input.sources.Count);
                    foreach(var source in input.sources)
                    {
                        // GUID与localId兼容一个导入模型中的多个Mesh子资产；不哈希可重建场景物件指针。
                        writer.Write(source.guid);writer.Write(source.localId);WriteMatrix(writer,source.toSurface);writer.Write(source.vertices.Length);
                        foreach(var p in source.vertices){writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}
                        writer.Write(source.indices.Length);
                        for(int submesh=0;submesh<source.indices.Length;submesh++)
                        {writer.Write(submesh);writer.Write(source.indices[submesh].Length);foreach(int index in source.indices[submesh])writer.Write(index);}
                    }
                }
                stream.Position=0;using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
            }
        }
        static void WriteMatrix(BinaryWriter writer,Matrix4x4 matrix)
        {for(int row=0;row<4;row++)for(int column=0;column<4;column++)writer.Write(matrix[row,column]);}

        /// <summary>只读保存的模型来源，返回派生域；不写Boundary或MeshFilter，不选择最高/最大环。</summary>
        public static SurfaceDomain ReadDomain(GroundSurfaceBinding binding,Mesh overrideFirstMesh=null)
        {
            var input=Collect(binding,overrideFirstMesh);
            if(!GroundSurfaceGeometry.TryExtractTopRim(input.snapshots.ToArray(),input.topY,input.options,out var domain,out var issue))
            {
                string location=issue==null?input.snapshots[0].sourceId:string.IsNullOrEmpty(issue.sourceId)?input.snapshots[0].sourceId:issue.sourceId;
                throw Error(issue==null?"UnknownGeometryFailure":issue.code,
                    (issue==null?"几何入口没有返回定位原因。":issue.message+" element="+issue.elementIndex)+"；显式localTopY="+input.topY,location);
            }
            return domain;
        }
    }
}
#endif
