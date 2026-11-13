/* 功能：在指定游戏相机的同帧投影下，累积 C 的分组屏幕光图。
 * 边界：不读取角色深度，不写相机颜色或深度，不驱动原 A/B 光照。 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ossastra.WorldRendering.SideView
{
    public sealed class SideViewAceLightMapFeature : ScriptableRendererFeature
    {
        private static readonly GraphicsFormat Format = GraphicsFormat.R16G16B16A16_SFloat;
        private readonly Dictionary<Camera, LightMapPass> passes = new Dictionary<Camera, LightMapPass>();
        private bool subscribed;

        internal static bool SupportsLightMaps => SystemInfo.supportedRenderTargetCount >= 4 &&
            SystemInfo.IsFormatSupported(Format, FormatUsage.Render) &&
            SystemInfo.IsFormatSupported(Format, FormatUsage.Blend) && SystemInfo.IsFormatSupported(Format, FormatUsage.Sample);

        public override void Create()
        {
            ReleaseAll();
            Subscribe();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            Subscribe();
            var camera = data.cameraData.camera;
            if (camera == null || camera.cameraType != CameraType.Game ||
                !SideViewAceLighting.TryGet(camera, out var owner) || owner.Writer == null) return;
            if (!passes.TryGetValue(camera, out var pass) || pass.Writer != owner.Writer)
            {
                Release(camera);
                pass = new LightMapPass(owner, camera);
                passes.Add(camera, pass);
            }
            renderer.EnqueuePass(pass);
        }

        private void Subscribe()
        {
            if (subscribed) return;
            SideViewAceLighting.Released += Release;
            subscribed = true;
        }

        private void Release(Camera camera)
        {
            if (!passes.TryGetValue(camera, out var pass)) return;
            passes.Remove(camera);
            pass.Dispose();
        }

        private void ReleaseAll()
        {
            if (subscribed) SideViewAceLighting.Released -= Release;
            subscribed = false;
            foreach (var pass in passes.Values) pass.Dispose();
            passes.Clear();
        }

        private void OnDisable() => ReleaseAll();
        protected override void Dispose(bool disposing) => ReleaseAll();

        private sealed class LightMapPass : ScriptableRenderPass, IDisposable
        {
            private static readonly int Ready = Shader.PropertyToID("_SideViewAceLightMapsReady");
            private static readonly int EdgeReady = Shader.PropertyToID("_SideViewAceEdgeMapsReady");
            private static readonly int EdgeTarget = Shader.PropertyToID("_AceEdgeTarget");
            private static readonly int ScreenToMap = Shader.PropertyToID("_SideViewAceScreenToMap");
            private static readonly int ViewProjection = Shader.PropertyToID("_AceWriterViewProjection");
            private static readonly int Shape = Shader.PropertyToID("_AceWriterShape");
            private static readonly int LightIntensity = Shader.PropertyToID("_AceWriterIntensity");
            private static readonly int Mask = Shader.PropertyToID("_AceWriterMask");
            private static readonly int Ambient = Shader.PropertyToID("_AceWriterAmbient");
            private static readonly int Bright = Shader.PropertyToID("_AceWriterBright");
            private static readonly int Highlight = Shader.PropertyToID("_AceWriterHighlight");
            private static readonly int AmbientClear = Shader.PropertyToID("_AceWriterAmbientClear");

            private readonly SideViewAceLighting owner;
            private readonly Camera camera;
            private readonly SideViewRenderComparison comparison;
            private readonly Material writer;
            private readonly int writerPass, clearPass, edgePass;
            private readonly Group world = new Group("World", new[] { "Direction", "Highlight", "Bright", "Ambient" });
            private readonly Group character = new Group("Character", new[] { "Direction", "Highlight", "Bright", "Ambient" });
            private readonly Group edges = new Group("", new[] { "EdgeVector", "EdgeEnergy" }, "Character");
            private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
            private bool prepared, edgesPrepared;
            private RTHandle cameraColor, cameraDepth;
            private int width, height;

            internal Shader Writer => writer != null ? writer.shader : null;

            internal LightMapPass(SideViewAceLighting owner, Camera camera)
            {
                this.owner = owner;
                this.camera = camera;
                comparison = camera.GetComponent<SideViewRenderComparison>();
                writer = CoreUtils.CreateEngineMaterial(owner.Writer);
                writerPass = writer.FindPass("AceLightMap");
                clearPass = writer.FindPass("AceClearMaps");
                edgePass = writer.FindPass("AceEdgeMaps");
                renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
            {
                owner.ClearFrame();
                edgesPrepared = false;
                var cameraData = data.cameraData;
                cameraColor = cameraData.renderer.cameraColorTargetHandle;
                cameraDepth = cameraData.renderer.cameraDepthTargetHandle;
                var descriptor = cameraData.cameraTargetDescriptor;
                prepared = writerPass >= 0 && clearPass >= 0 && cameraColor?.rt != null && cameraDepth != null && cameraData.isDefaultViewport &&
                    descriptor.dimension == TextureDimension.Tex2D && descriptor.volumeDepth == 1 && !descriptor.useDynamicScale;
                if (!prepared)
                {
                    edges.Dispose();
                    return;
                }
                width = Mathf.Max(1, Mathf.RoundToInt(descriptor.width * owner.ResolutionScale));
                height = Mathf.Max(1, Mathf.RoundToInt(descriptor.height * owner.ResolutionScale));
                prepared = false;
                try
                {
                    prepared = world.Prepare(width, height) && character.Prepare(width, height);
                }
                finally
                {
                    if (!prepared)
                    {
                        world.Dispose();
                        character.Dispose();
                        edges.Dispose();
                    }
                }
                if (!prepared) return;
                if (edgePass >= 0 && comparison != null && comparison.UsesAceFusionMaterial)
                {
                    try { edgesPrepared = edges.Prepare(width, height); }
                    catch (UnityException exception) { Debug.LogWarning("ACE 方向光图未能分配：" + exception.Message, owner); }
                    finally { if (!edgesPrepared) edges.Dispose(); }
                }
                else edges.Dispose();
                ConfigureTarget(cameraColor, cameraDepth);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData data)
            {
                if (!prepared || !world.Ready || !character.Ready ||
                    !SideViewAceLighting.TryGet(camera, out var active) || active != owner) return;

                var cameraData = data.cameraData;
                var descriptor = cameraData.cameraTargetDescriptor;
                // Match the actual game camera GPU projection and raster orientation, including its final presentation pose.
                var gpuProjection = cameraData.GetGPUProjectionMatrix();
                var projection = gpuProjection * cameraData.GetViewMatrix();
                bool flip = cameraData.IsHandleYFlipped(cameraColor) != cameraData.IsHandleYFlipped(world.Maps[0]);
                if (flip) projection = Matrix4x4.Scale(new Vector3(1f, -1f, 1f)) * projection;
                var cmd = CommandBufferPool.Get("Side view C · projected light maps");
                try
                {
                    int draws = DrawGroup(cmd, world, SideViewAceLightGroups.World, owner.Ambient(false), projection) +
                                DrawGroup(cmd, character, SideViewAceLightGroups.Character, owner.Ambient(true), projection);
                    world.Publish(cmd);
                    character.Publish(cmd);
                    int edgeDraws = 0;
                    if (edgesPrepared)
                    {
                        // The stored vector is in camera raster axes. Remove both projection flips from its clip delta.
                        bool gpuProjectionFlipped = gpuProjection.m11 * cameraData.GetProjectionMatrix().m11 < 0f;
                        float clipToCameraY = gpuProjectionFlipped != flip ? -1f : 1f;
                        edgeDraws = DrawEdges(cmd, projection, descriptor.width, descriptor.height, clipToCameraY);
                    }
                    cmd.SetGlobalVector(ScreenToMap, new Vector4(1f / descriptor.width, 1f / descriptor.height, flip ? -1f : 1f, flip ? 1f : 0f));
                    cmd.SetGlobalFloat(Ready, 1f);
                    cmd.SetGlobalFloat(EdgeReady, edgesPrepared ? 1f : 0f);
                    CoreUtils.SetRenderTarget(cmd, cameraColor, cameraDepth);
                    cmd.SetViewport(new Rect(0f, 0f, descriptor.width, descriptor.height));
                    context.ExecuteCommandBuffer(cmd);
                    owner.RecordFrame(draws, edgeDraws, width, height, edgesPrepared ? 10 : 8, world.Maps[3].rt,
                        edgesPrepared ? edges.Maps[1].rt : null, edgesPrepared ? edges.Maps[0].rt : null);
                }
                finally { CommandBufferPool.Release(cmd); }
            }

            private int DrawGroup(CommandBuffer cmd, Group group, SideViewAceLightGroups groupFlag, Color ambient, Matrix4x4 projection)
            {
                cmd.SetRenderTarget(group.Targets, BuiltinRenderTextureType.None);
                cmd.SetViewport(new Rect(0f, 0f, width, height));
                properties.SetVector(AmbientClear, (Vector4)ambient);
                cmd.DrawProcedural(Matrix4x4.identity, writer, clearPass, MeshTopology.Triangles, 3, 1, properties);
                return DrawLights(cmd, groupFlag, projection, writerPass);
            }

            private int DrawEdges(CommandBuffer cmd, Matrix4x4 projection, int cameraWidth, int cameraHeight, float clipToCameraY)
            {
                // Explicit per-attachment clears preserve the additive zero regardless of MRT clear semantics.
                foreach (var map in edges.Maps) CoreUtils.SetRenderTarget(cmd, map, ClearFlag.Color, Color.clear);
                cmd.SetRenderTarget(edges.Targets, BuiltinRenderTextureType.None);
                cmd.SetViewport(new Rect(0f, 0f, width, height));
                properties.SetVector(EdgeTarget, new Vector4(cameraWidth, cameraHeight, clipToCameraY, 0));
                int draws = DrawLights(cmd, SideViewAceLightGroups.Character, projection, edgePass);
                edges.Publish(cmd);
                return draws;
            }

            private int DrawLights(CommandBuffer cmd, SideViewAceLightGroups groupFlag, Matrix4x4 projection, int pass)
            {
                int draws = 0;
                foreach (var light in owner.Lights)
                {
                    if (light == null || !light.isActiveAndEnabled || light.Intensity <= 0f || !light.HasValidSettings ||
                        (light.Groups & groupFlag) == 0) continue;
                    properties.SetMatrix(ViewProjection, projection);
                    properties.SetVector(Shape, light.Shape);
                    properties.SetFloat(LightIntensity, light.Intensity);
                    properties.SetTexture(Mask, light.Mask);
                    properties.SetVector(Ambient, (Vector4)light.Ambient);
                    properties.SetVector(Bright, (Vector4)light.Bright);
                    properties.SetVector(Highlight, (Vector4)light.Highlight);
                    cmd.DrawProcedural(light.Matrix, writer, pass, MeshTopology.Triangles, 6, 1, properties);
                    draws++;
                }
                return draws;
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                cmd.SetGlobalFloat(Ready, 0f);
                cmd.SetGlobalFloat(EdgeReady, 0f);
                world.Unpublish(cmd);
                character.Unpublish(cmd);
                edges.Unpublish(cmd);
            }

            public void Dispose()
            {
                world.Dispose();
                character.Dispose();
                edges.Dispose();
                CoreUtils.Destroy(writer);
                if (owner != null) owner.ClearFrame();
            }

            private sealed class Group : IDisposable
            {
                private readonly string[] textureNames;
                private readonly int[] names;
                internal readonly RTHandle[] Maps;
                internal readonly RenderTargetIdentifier[] Targets;

                internal Group(string prefix, string[] channels, string displayPrefix = null)
                {
                    names = new int[channels.Length];
                    textureNames = new string[channels.Length];
                    Maps = new RTHandle[channels.Length];
                    Targets = new RenderTargetIdentifier[channels.Length];
                    for (int i = 0; i < channels.Length; i++)
                    {
                        names[i] = Shader.PropertyToID("_SideViewAce" + prefix + channels[i]);
                        textureNames[i] = "ACE " + (displayPrefix ?? prefix) + " " + channels[i];
                    }
                }

                internal bool Ready
                {
                    get
                    {
                        foreach (var map in Maps) if (map?.rt == null || !map.rt.IsCreated()) return false;
                        return true;
                    }
                }

                internal bool Prepare(int width, int height)
                {
                    if (Ready && Maps[0].rt.width == width && Maps[0].rt.height == height) return true;
                    // Own these attachments directly. A resize must not leave old ACE maps in the URP global pool.
                    Dispose();
                    for (int i = 0; i < Maps.Length; i++)
                    {
                        Maps[i] = RTHandles.Alloc(width, height, colorFormat: Format, filterMode: FilterMode.Bilinear,
                            wrapMode: TextureWrapMode.Clamp, autoGenerateMips: false,
                            name: textureNames[i]);
                        if (Maps[i]?.rt == null || !Maps[i].rt.IsCreated()) return false;
                        Targets[i] = Maps[i].nameID;
                    }
                    return true;
                }

                internal void Publish(CommandBuffer cmd)
                {
                    for (int i = 0; i < Maps.Length; i++) cmd.SetGlobalTexture(names[i], Maps[i].nameID);
                }

                internal void Unpublish(CommandBuffer cmd)
                {
                    for (int i = 0; i < Maps.Length; i++) cmd.SetGlobalTexture(names[i], Texture2D.blackTexture);
                }

                public void Dispose()
                {
                    for (int i = 0; i < Maps.Length; i++)
                    {
                        // RTHandle destruction is deferred in Play. Free its backing texture when the lease ends.
                        if (Maps[i]?.rt != null) Maps[i].rt.Release();
                        Maps[i]?.Release();
                        Maps[i] = null;
                    }
                }
            }
        }
    }
}
