/*
 * 脚本：GroundSurfaceTechnicalModels
 * 功能：制作本轮自有的开放顶岩壁技术Mesh并原生保存；形状仅用于检验模型接入。
 * 边界：不生成顶盖、不把私有造墙多边形传给封口服务，不冒称任意FBX已适配。
 * 协作：保存后重新从AssetDatabase读取，ModelReader独立从Mesh开放边恢复上沿。
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace Ossastra.GroundSurfaces.Editor
{
    public static class GroundSurfaceTechnicalModels
    {
        public const string Root = "Assets/OSSASTRA/GroundWorkshop/Samples/KT-GROUND-03";
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            if (split <= 0) throw new InvalidOperationException("非法自有资产目录：" + path);
            EnsureFolder(path.Substring(0, split));
            AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }
        public static Mesh[] CreateModels()
        {
            EnsureFolder(Root + "/Models");
            var a = new[] { new Vector2(-7,-7),new Vector2(7,-7),new Vector2(7,-2),new Vector2(1.3f,-2),new Vector2(1.3f,7),new Vector2(-7,7) };
            var aVariant = new[] { new Vector2(-7,-7),new Vector2(7,-7),new Vector2(7,-2),new Vector2(-.5f,-2),new Vector2(-.5f,7),new Vector2(-7,7) };
            var b = new[] { new Vector2(-7,-6),new Vector2(7,-6),new Vector2(7,1),new Vector2(2,6),new Vector2(-7,6) };
            return new[] { SaveWall("CliffA_OpenConcave",a,3f), SaveWall("CliffA_WiderNotch",aVariant,3f), SaveWall("CliffB_Diagonal",b,5f) };
        }
        private static Mesh SaveWall(string name, Vector2[] ring, float height)
        {
            string path = Root + "/Models/" + name + ".asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("首次制作拒绝覆盖模型：" + path);
            var vertices = new List<Vector3>(); var uv = new List<Vector2>();
            var a = new List<int>(); var b = new List<int>();
            // 每段独立顶点模拟真实UV/硬法线缝；提边器必须按位置临时焊接，不能靠共享索引。
            for (int edge = 0; edge < ring.Length; edge++)
            {
                Vector2 p = ring[edge], q = ring[(edge + 1) % ring.Length];
                for (int band = 0; band < 2; band++)
                {
                    float top = band == 0 ? 0f : -height * .53f;
                    float bottom = band == 0 ? -height * .53f : -height;
                    Vector2 pt = p * (band == 0 ? 1f : 1.025f), qt = q * (band == 0 ? 1f : 1.025f);
                    Vector2 pb = p * (band == 0 ? 1.025f : 1.04f), qb = q * (band == 0 ? 1.025f : 1.04f);
                    int start = vertices.Count;
                    vertices.Add(new Vector3(pt.x,top,pt.y)); vertices.Add(new Vector3(qt.x,top,qt.y));
                    vertices.Add(new Vector3(qb.x,bottom,qb.y)); vertices.Add(new Vector3(pb.x,bottom,pb.y));
                    uv.Add(new Vector2(edge,band)); uv.Add(new Vector2(edge+1,band));uv.Add(new Vector2(edge+1,band+1));uv.Add(new Vector2(edge,band+1));
                    var indices = (edge + band) % 2 == 0 ? a : b;
                    indices.AddRange(new[] { start,start+1,start+2,start,start+2,start+3 });
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.subMeshCount=2;mesh.SetTriangles(a,0);mesh.SetTriangles(b,1);
            mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);AssetDatabase.SaveAssetIfDirty(mesh);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null)throw new InvalidOperationException("无法读回已保存侧壁模型："+path);
            return saved;
        }
    }
}
#endif
