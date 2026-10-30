/* 脚本：GroundSurfaceDecorationDiagnostics
 * 功能：只读列出当前平台模型变更后失效的装饰脚点，给出高度、边界或退让原因。
 * 边界：列表为即时派生结果，不写实例标记/位置、不保存第二份落点、不删除/移动/重撒。
 * 协作：原装饰服务Owned精确匹配可管理实例；样板窗口只展示原因和显式选中入口。
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using Ossastra.GroundResearch;
using Ossastra.GroundResearch.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ossastra.GroundSurfaces.Editor
{
    public sealed class GroundSurfaceDecorationIssue
    {
        public GroundDecorationInstance instance;
        public string code,reason;
        public Vector3 worldFootpoint;
    }

    public static class GroundSurfaceDecorationDiagnostics
    {
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static bool Finite(Vector3 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z);
        static string Metres(float value)=>value.ToString("0.####",CultureInfo.InvariantCulture)+"米";
        static void Require(GroundSurfaceBinding binding)
        {
            if(binding==null||!binding.isActiveAndEnabled||EditorUtility.IsPersistent(binding)||binding.gameObject.scene!=SceneManager.GetActiveScene()||binding.gameObject.scene.path!=GroundSurfaceAuthoring.ScenePath)
                throw new InvalidOperationException("装饰落点检查仅用于当前独立高台样板的有效平台。");
            GroundWorkshopOwnership.Require(binding.map);
            if(binding.map.gameObject.scene!=binding.gameObject.scene||binding.boundary==null||
               !GroundWorkshopOwnership.IsUnder(AssetDatabase.GetAssetPath(binding.boundary),binding.map.registration.assetFolder))
                throw new InvalidOperationException("当前平台没有独立、受控的已确认连接环，无法判断装饰落点。");
            if(!Finite(binding.boundary.localTopY)||!Finite(binding.footpointHeightTolerance)||binding.footpointHeightTolerance<=0||!Finite(binding.DecorationInset)||binding.DecorationInset<0)
                throw new InvalidOperationException("装饰检查的高度/脚点容差/边缘退让配置无效。");
        }
        public static GroundSurfaceDecorationIssue[] Inspect(GroundSurfaceBinding binding)
        {
            Require(binding);
            var boundary=binding.boundary;var options=new GeometryOptions{weldTolerance=boundary.weldTolerance,planeTolerance=boundary.planeTolerance};
            if(!GroundSurfaceGeometry.TryBuildDomain(boundary.rim,options,out var domain,out var failure))
                throw new InvalidOperationException("连接环不可用，装饰失效数量未知，未当作0："+failure.code+" "+failure.message);
            var result=new List<GroundSurfaceDecorationIssue>();
            foreach(var instance in binding.map.decorationRoot.GetComponentsInChildren<GroundDecorationInstance>(true))
            {
                // 外部、被人为扩展或不再匹配定义的物件不被自动接管；查询亦不补写工具标记。
                if(!GroundWorkshopDecorations.Owned(binding.map,instance))continue;
                Vector3 world=instance.transform.position;string code=null,reason=null;
                if(!Finite(world)){code="NonFiniteFootpoint";reason="原生脚点包含非有限坐标；保留实例，无法判断有效落点。";}
                else
                {
                    Vector3 local=binding.transform.InverseTransformPoint(world);
                    Vector3 plane=binding.transform.TransformPoint(new Vector3(local.x,boundary.localTopY,local.z));
                    if(Mathf.Abs(local.y-boundary.localTopY)>binding.footpointHeightTolerance)
                    {
                        code="WrongHeight";reason="脚点世界高度="+Metres(world.y)+"，距平台平面="+Metres(Mathf.Abs(world.y-plane.y))+"，超过容差="+
                            Metres(binding.transform.TransformVector(Vector3.up*binding.footpointHeightTolerance).magnitude)+"；保留原位置。";
                    }
                    else if(!GroundSurfaceGeometry.ContainsXZ(domain,new Vector2(local.x,local.z)))
                    {code="OutsideBoundary";reason="脚点已在当前已确认模型边界之外；手摆、锁定和生成实例均保留原位置。";}
                    else
                    {
                        float distance=EdgeDistance(binding,domain,plane);
                        if(distance<binding.DecorationInset)
                        {code="InsufficientInset";reason="脚点距模型边界="+Metres(distance)+"，小于边缘退让="+Metres(binding.DecorationInset)+"；保留原位置。";}
                    }
                }
                if(code!=null)result.Add(new GroundSurfaceDecorationIssue{instance=instance,code=code,reason=reason,worldFootpoint=world});
            }
            return result.ToArray();
        }
        static float EdgeDistance(GroundSurfaceBinding binding,SurfaceDomain domain,Vector3 point)
        {
            float distance=float.PositiveInfinity;
            for(int i=0;i<domain.ring.Length;i++)
            {
                var p=domain.ring[i];var q=domain.ring[(i+1)%domain.ring.Length];
                Vector3 a=binding.transform.TransformPoint(new Vector3(p.x,binding.boundary.localTopY,p.y));
                Vector3 b=binding.transform.TransformPoint(new Vector3(q.x,binding.boundary.localTopY,q.y));
                Vector3 edge=b-a;float t=edge.sqrMagnitude>0?Mathf.Clamp01(Vector3.Dot(point-a,edge)/edge.sqrMagnitude):0;
                distance=Mathf.Min(distance,Vector3.Distance(point,a+edge*t));
            }
            return distance;
        }
        public static void Select(GroundSurfaceBinding binding,GroundDecorationInstance instance)
        {
            Require(binding);
            if(!GroundWorkshopDecorations.Owned(binding.map,instance))throw new InvalidOperationException("所选实例不属于当前平台，未修改选择或接管外部物件。");
            Selection.activeGameObject=instance.gameObject;EditorGUIUtility.PingObject(instance.gameObject);
        }
    }
}
#endif
