/* 功能：现有Room正向绘制内提交显式登记的有限3D反壳。
 * 边界：只消费当前相机PositiveSurfaces及UploadOwner；不产生Room/标签/Style许可、不读回历史、不搜索场景。
 * 生命周期：由原RoomPositivePass唯一owner持有/释放，一个共享材质；每槽最多一次附加绘制，不在每帧克隆材质。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Ossastra.WorldRendering.Stylization;

namespace Ossastra.SceneVisibility.Rooms.Rendering
{
    internal sealed class LookDevFiniteOutlineStage : IDisposable
    {
        static readonly ProfilingSampler sampler = new ProfilingSampler("LookDev.FiniteOutline");
        readonly Material material;
        readonly Shader realToonLocalShader;
        sealed class PluginState { public Material material; public int sourceCrc,ownCrc; public Color color; }
        readonly Dictionary<Material,PluginState> pluginMaterials=new Dictionary<Material,PluginState>();
        readonly Plane[] planes = new Plane[6];
        public int SubmittedLastFrame { get; private set; }
        public LookDevFiniteOutlineStage(Shader shader,Shader auditedRealToonLocalShader=null)
        {
            if (shader != null && shader.isSupported)
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            if(auditedRealToonLocalShader!=null&&auditedRealToonLocalShader.isSupported&&
                auditedRealToonLocalShader.name=="Hidden/Ossastra/LookDev/RealToonRoomLocal")realToonLocalShader=auditedRealToonLocalShader;
        }
        // Called after DrawFinalWorld and before labels / StyleCoverage, while the actual legal world depth is bound.
        // Output size is the final camera viewport size, not the scaled intermediate RT size.
        public void Append(CommandBuffer cmd, Camera camera, RoomMaskFrame frame, LookDevFiniteToon controller,
            Color themeDarkLinear, Action<CommandBuffer, RoomPositiveSurface> uploadOwner)
        {
            SubmittedLastFrame = 0;
            if (material == null || camera == null || frame == null || frame.TruthMode || controller == null ||
                !controller.OutlineEnabled || uploadOwner == null || frame.PositiveSurfaces == null) return;
            int width = camera.pixelWidth, height = camera.pixelHeight;
            if (width <= 0 || height <= 0) return;
            var targets = controller.Bindings;
            if (targets == null || targets.Count > 24) return;
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            using (new ProfilingScope(cmd,sampler))
            {
                cmd.SetGlobalVector("_LookDevOutlineOutputSize",new Vector4(width,height,1f/width,1f/height));
                cmd.SetGlobalFloat("_LookDevOutlinePixels",controller.OutlineOutputPixels);
                cmd.SetGlobalColor("_LookDevOutlineColor",themeDarkLinear);
                for (int t=0;t<targets.Count;++t)
                {
                    var target=targets[t];
                    if (target==null || !target.outline || target.renderer==null || !target.renderer.enabled ||
                        !target.renderer.gameObject.activeInHierarchy || !GeometryUtility.TestPlanesAABB(planes,target.renderer.bounds)) continue;
                    bool plugin=controller.Variant==LookDevToonVariant.RealToon;
                    if(plugin&&realToonLocalShader==null)continue;
                    for (int i=0;i<frame.PositiveSurfaces.Length;++i)
                    {
                        var surface=frame.PositiveSurfaces[i];
                        if (surface.Renderer != target.renderer || !surface.WorldLookEnvironment) continue;
                        uploadOwner(cmd,surface);
                        bool cut=frame.ExteriorView && surface.Kind==RoomPositiveSurfaceKind.Roof && frame.PreviewRoofSurfaces!=null &&
                            Array.IndexOf(frame.PreviewRoofSurfaces,surface.Renderer)>=0;
                        float mode=surface.Kind==RoomPositiveSurfaceKind.Roof?2:surface.Kind==RoomPositiveSurfaceKind.Partial&&!surface.EnvironmentFullSurface?1:0;
                        cmd.SetGlobalFloat("_LookDevOutlineRoomMode",mode);
                        cmd.SetGlobalFloat("_LookDevOutlineCutRoof",cut?1:0);
                        cmd.SetGlobalFloat("_LookDevOutlineSmooth",target.smoothOutlineNormalsInUv3?1:0);
                        if(plugin)
                        {
                            var adapter=PluginAdapter(target.realToon,themeDarkLinear);
                            if(adapter==null)continue;
                            int pass=adapter.FindPass("Outline");if(pass<0)continue;
                            cmd.SetGlobalVector("_LookDevRealToonOutputSize",new Vector4(width,height,1f/width,1f/height));
                            cmd.SetGlobalFloat("_LookDevRealToonOutlinePixels",controller.OutlineOutputPixels);
                            cmd.SetGlobalFloat("_LookDevRealToonForceFull",surface.Kind==RoomPositiveSurfaceKind.Full?1:0);
                            cmd.DrawRenderer(target.renderer,adapter,target.materialSlot,pass);
                        }
                        else cmd.DrawRenderer(target.renderer,material,target.materialSlot,0);
                        ++SubmittedLastFrame;
                        break;
                    }
                }
                cmd.SetGlobalFloat("_LookDevOutlinePixels",0);
                cmd.SetGlobalFloat("_LookDevOutlineRoomMode",0);
                cmd.SetGlobalFloat("_LookDevOutlineCutRoof",0);
                cmd.SetGlobalFloat("_LookDevOutlineSmooth",0);
                cmd.SetGlobalFloat("_LookDevRealToonOutlinePixels",0);
                cmd.SetGlobalFloat("_LookDevRealToonForceFull",0);
            }
        }
        Material PluginAdapter(Material source,Color color)
        {
            if(source==null||source.shader==null||source.shader.name!="Universal Render Pipeline/RealToon/Version 5/Default/Default"||
                source.GetTag("LookDevFiniteToon",false,"")!="LOOKDEV-01")return null;
            if(!pluginMaterials.TryGetValue(source,out var state))
            {
                state=new PluginState{material=new Material(realToonLocalShader){hideFlags=HideFlags.HideAndDontSave}};
                pluginMaterials.Add(source,state);
            }
            int sourceCrc=source.ComputeCRC();
            if(sourceCrc!=state.sourceCrc||state.material.ComputeCRC()!=state.ownCrc||state.color!=color)
            {
                state.material.CopyPropertiesFromMaterial(source);
                state.material.SetFloat("_N_F_O",1);state.material.EnableKeyword("N_F_O_ON");
                state.material.SetShaderPassEnabled("SRPDefaultUnlit",true);
                state.material.SetFloat("_OutlineWidth",0);state.material.SetColor("_OutlineColor",color);
                state.sourceCrc=sourceCrc;state.color=color;state.ownCrc=state.material.ComputeCRC();
            }
            return state.material;
        }
        public void Dispose()
        {
            if(material!=null)CoreUtils.Destroy(material);
            foreach(var state in pluginMaterials.Values)if(state.material!=null)CoreUtils.Destroy(state.material);
            pluginMaterials.Clear();
        }
    }
}
