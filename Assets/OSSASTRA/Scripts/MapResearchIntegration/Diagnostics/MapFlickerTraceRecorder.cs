/* 脚本：MapFlickerTraceRecorder
 * 功能：显式启停研究场角色的正式Spine网格/姿态与渲染相机阶段环形追踪，支持手动标记导出。
 * 边界：默认关闭；不推进角色、Spine或相机，不修改显隐/材质/输入，不以CPU摘要认证GPU无闪烁。
 * 协作：外层明确配置map/actors；每个身体独立订阅正式事件，停止精确退订；仅用户导出写文件。
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Ossastra.MapResearch;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering;
using C = Ossastra.Characters;
using A = Ossastra.CharacterAnimation;

namespace Ossastra.MapResearchIntegration.Diagnostics
{
    [DisallowMultipleComponent]
    public sealed class MapFlickerTraceRecorder : MonoBehaviour
    {
        [Header("研究对象 / 由独立场装配，不自动扫描其它场景")]
        public MapResearchRoot map;
        public C.CharacterRoot[] actors = Array.Empty<C.CharacterRoot>();
        [Header("诊断默认关闭 / 固定样本条数，不承诺固定秒数")]
        [SerializeField] private bool tracingEnabled;
        [SerializeField, Range(1, 4096)] private int capacity = 2048;
        [Tooltip("手动标记文字；仅保留最新32个标记。")]
        public string markerLabel = "user-mark";
        [Tooltip("留空则手动导出到persistentDataPath；不会自动写盘。")]
        public string exportDirectory;

        private readonly List<BodyBinding> bindings = new List<BodyBinding>();
        private readonly List<MapFlickerTraceMarker> markers = new List<MapFlickerTraceMarker>(32);
        private MapFlickerTraceBuffer buffer;
        private bool running;
        private long cpuCandidates;
        public bool IsTracing => running;
        public int HookedBodyCount => bindings.Count;
        public long ExportCount { get; private set; }
        public long MeshEventCount { get; private set; }
        public int SampleCount => buffer?.Count ?? 0;
        public long DroppedSampleCount => buffer?.Dropped ?? 0;
        public string LastExportPath { get; private set; } = "";
        public string LastError { get; private set; } = "";
        public string Status => running ? "记录中（CPU与相机阶段；不证明GPU可见）" : "已停止；保留最近一轮记录";

        public void Configure(MapResearchRoot researchMap, C.CharacterRoot[] characters)
        {
            if (running) throw new InvalidOperationException("请先停止追踪再变更诊断对象。");
            map = researchMap;
            actors = characters == null ? Array.Empty<C.CharacterRoot>() : (C.CharacterRoot[])characters.Clone();
        }

        // 表现控制器可保留上一已结束动作的相位；只有仍活动且身份吻合的动作才有诊断关联相位。
        internal static Guid AssociatedPhase(bool actionActive, Guid actionId, Guid capturedActionId, Guid capturedPhaseId)
        {
            return actionActive && actionId != Guid.Empty && capturedActionId == actionId ? capturedPhaseId : Guid.Empty;
        }

        public bool BeginTracing()
        {
            if (running) return true;
            LastError = "";
            if (!Application.isPlaying || !isActiveAndEnabled || map == null)
                return Refuse("需要Play中的研究场、活动Recorder及明确map引用。");
            var seen = new HashSet<SkeletonAnimation>();
            if (actors != null) foreach (var actor in actors)
            {
                if (actor == null) continue;
                foreach (var controller in actor.GetComponentsInChildren<A.CharacterAnimationController>(true))
                {
                    var body = controller.Skeleton;
                    if (controller.GetComponentInParent<C.CharacterRoot>() != actor || body == null ||
                        !body.transform.IsChildOf(actor.transform) || !seen.Add(body)) continue;
                    // 沿正式控制器公开的身体端口，而非子树中任意Spine；独立装备不会混入身体记录。
                    var direction = controller.GetComponent<C.CharacterDirectionPresentation>();
                    bindings.Add(new BodyBinding(this, actor, body, direction, controller));
                }
            }
            if (bindings.Count == 0) return Refuse("配置角色中没有可订阅的SkeletonAnimation身体。");
            buffer = new MapFlickerTraceBuffer(capacity);
            markers.Clear();
            MeshEventCount = 0;
            cpuCandidates = 0;
            running = true;
            tracingEnabled = true;
            try
            {
                foreach (var binding in bindings) binding.Attach();
                RenderPipelineManager.beginCameraRendering += CameraBegin;
                RenderPipelineManager.endCameraRendering += CameraEnd;
                return true;
            }
            catch (Exception error)
            {
                StopTracing();
                LastError = "订阅失败，已释放本追踪器订阅：" + error.Message;
                return false;
            }
        }

        private bool Refuse(string reason) { tracingEnabled = false; LastError = reason; return false; }

        public void StopTracing()
        {
            running = false;
            tracingEnabled = false;
            RenderPipelineManager.beginCameraRendering -= CameraBegin;
            RenderPipelineManager.endCameraRendering -= CameraEnd;
            foreach (var binding in bindings) binding.Detach();
            bindings.Clear();
        }

        private void Update()
        {
            if (tracingEnabled && !running) BeginTracing();
            else if (!tracingEnabled && running) StopTracing();
        }
        private void OnDisable() => StopTracing();
        private void OnDestroy() => StopTracing();
        private void OnValidate() { capacity = Mathf.Clamp(capacity, 1, 4096); }

        private Camera ExpectedCamera => map == null ? null :
            map.observerCamera != null && map.observerCamera.isActiveAndEnabled ? map.observerCamera : map.GameplayCamera;
        private bool IsResearchCamera(Camera camera) => map != null && camera != null &&
            (camera == map.GameplayCamera || camera == map.observerCamera);
        private void CameraBegin(ScriptableRenderContext context, Camera camera) => CaptureCamera(camera, "camera-begin");
        private void CameraEnd(ScriptableRenderContext context, Camera camera) => CaptureCamera(camera, "camera-end");
        private void CaptureCamera(Camera camera, string provenance)
        {
            if (!running || !IsResearchCamera(camera)) return;
            foreach (var binding in bindings)
            {
                if (!binding.IsAlive || !binding.HasMeshSample) continue;
                var sample = binding.LastMeshSample;
                sample.provenance = provenance;
                sample.frame = Time.frameCount;
                sample.realtime = Time.realtimeSinceStartupAsDouble;
                sample.renderCameraId = camera.GetInstanceID();
                binding.ReadContext(ref sample);
                buffer.Append(sample);
            }
        }

        public void Mark(string label)
        {
            // 标记本身只写内存。停止后也可标记，但frame并不伪装成仍在采样的帧。
            if (markers.Count == 32) markers.RemoveAt(0);
            var bounded = string.IsNullOrWhiteSpace(label) ? "user-mark" : label.Trim();
            if (bounded.Length > 160) bounded = bounded.Substring(0, 160);
            markers.Add(new MapFlickerTraceMarker { frame = Time.frameCount, realtime = Time.realtimeSinceStartupAsDouble, label = bounded });
        }

        public string CreateReportJson()
        {
            return JsonUtility.ToJson(new Report
            {
                status = cpuCandidates > 0 ? "CPU_EMPTY_OR_ZERO_ALPHA_CANDIDATE" : "UNREPRODUCED_CPU_TRACE",
                gpuVisibilityVerified = false,
                evidenceBoundary = "正式Spine生成网格摘要和URP相机阶段；非GPU画面、非遮挡证明，未判定闪烁根因。相机阶段沿用最近mesh样本，以meshFrame区分跨帧。",
                frame = Time.frameCount, tracing = running, capacity = buffer?.Capacity ?? Mathf.Clamp(capacity, 1, 4096),
                meshEventCount = MeshEventCount, dropped = buffer?.Dropped ?? 0, cpuCandidates = cpuCandidates,
                markers = markers.ToArray(), samples = buffer?.Snapshot() ?? Array.Empty<MapFlickerTraceSample>()
            }, true);
        }

        public string ExportReport(string directory = null)
        {
            try
            {
                var chosen = string.IsNullOrWhiteSpace(directory) ? Path.Combine(Application.persistentDataPath, "OSSASTRA", "MapResearchDiagnostics") : directory;
                var folder = Path.GetFullPath(chosen);
                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, "DS02-trace-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json");
                File.WriteAllText(path, CreateReportJson(), new UTF8Encoding(false));
                LastExportPath = path;
                LastError = "";
                ExportCount++;
                return path;
            }
            catch (Exception error) { LastError = "导出失败：" + error.Message; return ""; }
        }

        [ContextMenu("诊断/开始有界追踪")]
        public void StartFromButton() => BeginTracing();
        [ContextMenu("诊断/仅标记当前异常")]
        public void MarkFromButton() => Mark(markerLabel);
        [ContextMenu("诊断/手动导出最近记录")]
        public void ExportFromButton() => ExportReport(exportDirectory);
        [ContextMenu("诊断/标记异常并导出最近记录")]
        public void MarkAndExportFromButton() { MarkFromButton(); ExportFromButton(); }

        [Serializable]
        private sealed class Report
        {
            public string status, evidenceBoundary;
            public bool gpuVisibilityVerified, tracing;
            public int frame, capacity;
            public long meshEventCount, dropped, cpuCandidates;
            public MapFlickerTraceMarker[] markers;
            public MapFlickerTraceSample[] samples;
        }

        private sealed class BodyBinding
        {
            private readonly MapFlickerTraceRecorder owner;
            private readonly C.CharacterRoot actor;
            private readonly SkeletonAnimation body;
            private readonly C.CharacterDirectionPresentation direction;
            private readonly A.CharacterAnimationController controller;
            private readonly MeshFilter filter;
            private readonly MeshRenderer renderer;
            private readonly List<Color32> colors = new List<Color32>(512);
            private readonly List<Material> materials = new List<Material>(4);
            // 记录精确订阅目标，释放时不经body.Renderer重新查找，避免重建后退订错实例。
            private ISkeletonRenderer hookedRenderer;
            private bool beforeHooked, poseHooked, meshHooked;
            private int counterFrame = -1, beforeCalls, poseCalls, meshCalls, poseFrame = -1, poseVisible, poseZero;
            private Guid cachedAction, cachedPhase;
            private string actionText = "", phaseText = "";
            public bool HasMeshSample { get; private set; }
            public bool IsAlive => actor != null && body != null;
            public MapFlickerTraceSample LastMeshSample { get; private set; }

            public BodyBinding(MapFlickerTraceRecorder owner, C.CharacterRoot actor, SkeletonAnimation body,
                C.CharacterDirectionPresentation direction, A.CharacterAnimationController controller)
            {
                this.owner = owner; this.actor = actor; this.body = body; this.direction = direction;
                this.controller = controller;
                filter = body.GetComponent<MeshFilter>();
                renderer = body.GetComponent<MeshRenderer>();
            }
            public void Attach()
            {
                hookedRenderer = body.Renderer;
                body.BeforeApply += BeforeApply; beforeHooked = true;
                hookedRenderer.UpdateComplete += PoseComplete; poseHooked = true;
                hookedRenderer.OnMeshAndMaterialsUpdated += MeshComplete; meshHooked = true;
            }
            public void Detach()
            {
                if (beforeHooked && body != null) body.BeforeApply -= BeforeApply;
                if (hookedRenderer != null)
                {
                    if (poseHooked) hookedRenderer.UpdateComplete -= PoseComplete;
                    if (meshHooked) hookedRenderer.OnMeshAndMaterialsUpdated -= MeshComplete;
                }
                beforeHooked = poseHooked = meshHooked = false;
                hookedRenderer = null;
            }
            private void ResetFrameCounters()
            {
                if (counterFrame == Time.frameCount) return;
                counterFrame = Time.frameCount; beforeCalls = poseCalls = meshCalls = 0;
            }
            private void BeforeApply(ISkeletonAnimation component)
            {
                if (!owner.running) return;
                ResetFrameCounters(); beforeCalls++;
            }
            private void PoseComplete(ISkeletonRenderer component)
            {
                if (!owner.running || body == null) return;
                ResetFrameCounters(); poseCalls++; poseFrame = Time.frameCount; poseVisible = poseZero = 0;
                var skeleton = body.Skeleton;
                if (skeleton == null) return;
                // 该阶段位于角色显隐之后、网格后还原之前；此处摘要与生成顶点alpha分开保留。
                foreach (var slot in skeleton.Slots)
                {
                    if (slot.AppliedPose.Attachment == null) continue;
                    if (slot.AppliedPose.GetColor().a > 0) poseVisible++;
                    else poseZero++;
                }
            }
            private void MeshComplete(ISkeletonRenderer component)
            {
                if (!owner.running || body == null || actor == null) return;
                ResetFrameCounters(); meshCalls++; owner.MeshEventCount++;
                var sample = new MapFlickerTraceSample
                {
                    provenance = "mesh", frame = Time.frameCount, meshFrame = Time.frameCount,
                    realtime = Time.realtimeSinceStartupAsDouble, slotPoseFrame = poseFrame,
                    beforeApplyCalls = beforeCalls, poseCompleteCalls = poseCalls, meshCalls = meshCalls,
                    poseVisibleSlotCount = poseVisible, poseZeroAlphaSlotCount = poseZero,
                    renderCameraId = 0, vertexAlphaMin = -1, vertexAlphaMax = -1, vertexAlphaMean = -1,
                    materialCutoff = -1, materialPmaInput = -1
                };
                try
                {
                    ReadContext(ref sample);
                    var mesh = filter == null ? null : filter.sharedMesh;
                    sample.meshAvailable = mesh != null;
                    sample.meshInstanceId = mesh == null ? 0 : mesh.GetInstanceID();
                    if (mesh != null)
                    {
                        sample.vertexCount = mesh.vertexCount; sample.subMeshCount = mesh.subMeshCount;
                        sample.meshBoundsCenter = mesh.bounds.center; sample.meshBoundsExtents = mesh.bounds.extents;
                        colors.Clear(); mesh.GetColors(colors); sample.vertexColorCount = colors.Count;
                        if (colors.Count > 0)
                        {
                            var min = 255; var max = 0; long total = 0;
                            foreach (var color in colors)
                            {
                                var alpha = color.a; min = Math.Min(min, alpha); max = Math.Max(max, alpha); total += alpha;
                                if (alpha == 0) sample.vertexAlphaZeroCount++; else sample.vertexAlphaPositiveCount++;
                                if (alpha > 15) sample.vertexAlphaAbove15Count++;
                            }
                            sample.vertexAlphaMin = min / 255f; sample.vertexAlphaMax = max / 255f; sample.vertexAlphaMean = total / (255f * colors.Count);
                        }
                    }
                    ReadRenderer(ref sample);
                    // 未提供顶点色与全零顶点色不同；仅后者进入候选，不把未知色流当成透明。
                    sample.cpuEmptyOrAllZeroAlpha = !sample.meshAvailable || sample.vertexCount == 0 ||
                        sample.vertexColorCount > 0 && sample.vertexAlphaPositiveCount == 0;
                    if (sample.cpuEmptyOrAllZeroAlpha) owner.cpuCandidates++;
                }
                catch (Exception error) { sample.samplingError = error.GetType().Name + ": " + error.Message; }
                LastMeshSample = sample; HasMeshSample = true; owner.buffer.Append(sample);
            }
            public void ReadContext(ref MapFlickerTraceSample sample)
            {
                if (actor == null || body == null) return;
                sample.actorId = actor.ActorId; sample.actorInstanceId = actor.GetInstanceID(); sample.bodyInstanceId = body.GetInstanceID();
                var bridge = owner.map == null ? null : owner.map.sessionComponent as Ossastra.MapResearchIntegration.MapResearchPartyBridge;
                // 只读原协调者本次采样的Current/代际，不在追踪器另建可变Current。单人旧场明确标single。
                var session = bridge == null ? null : bridge.coordinator;
                sample.controlSessionKind = bridge == null ? "single" : "party";
                sample.currentActorId = bridge == null ? "single" : session == null || session.Current == null ? "" : session.Current.ActorId;
                sample.controlRevision = session == null ? -1 : session.ControlRevision;
                sample.skeletonInstanceId = body.Skeleton == null ? 0 : RuntimeHelpers.GetHashCode(body.Skeleton);
                var camera = owner.ExpectedCamera; sample.expectedCameraId = camera == null ? 0 : camera.GetInstanceID();
                sample.actualYaw = owner.map == null ? 0 : owner.map.ActualYaw; sample.targetYaw = owner.map == null ? 0 : owner.map.TargetYaw;
                sample.rootPosition = actor.transform.position; sample.rootScale = actor.transform.lossyScale;
                sample.visualPosition = body.transform.position; sample.visualRotation = body.transform.rotation; sample.visualScale = body.transform.lossyScale;
                sample.faceId = direction == null ? "unbound" : direction.FaceId; sample.mirrored = direction != null && direction.Mirrored;
                sample.directionSource = direction == null ? "unbound" : direction.DirectionSource;
                sample.directionRevision = direction == null ? 0 : direction.Revision; sample.hiddenSlotCount = direction == null ? 0 : direction.HiddenSlotCount;
                sample.lifeGeneration = actor.LifeGeneration; sample.attackStage = actor.AttackStage; sample.actionActive = actor.ActionActive;
                var bus = actor.DataBus;
                if (bus != null)
                {
                    sample.worldDirection = bus.PresentationDirection; sample.viewDirection = bus.ViewPresentationDirection;
                    if (cachedAction != bus.ActionId) { cachedAction = bus.ActionId; actionText = cachedAction.ToString(); }
                }
                var phase = controller == null || bus == null ? Guid.Empty :
                    AssociatedPhase(bus.ActionActive, bus.ActionId, controller.CapturedActionId, controller.CapturedPhaseId);
                if (cachedPhase != phase) { cachedPhase = phase; phaseText = phase == Guid.Empty ? "" : phase.ToString(); }
                sample.actionId = actionText; sample.phaseId = phaseText;
                sample.bodyEnabled = body.isActiveAndEnabled; sample.updateMode = body.UpdateMode.ToString(); sample.updateTiming = body.UpdateTiming.ToString();
                sample.skeletonAlpha = body.Skeleton == null ? -1 : body.Skeleton.GetColor().a;
                sample.zSpacing = body.GetComponent<SkeletonRenderer>()?.zSpacing ?? 0;
                var main = body.AnimationState?.GetTrack(0);
                sample.mainAnimation = main?.Animation?.Name ?? ""; sample.mainTime = main?.TrackTime ?? 0; sample.mainLoop = main?.Loop ?? false;
                sample.structureTrack = direction?.Profile == null ? -1 : direction.Profile.structureTrack;
                var structural = sample.structureTrack < 0 ? null : body.AnimationState?.GetTrack(sample.structureTrack);
                sample.structureAnimation = structural?.Animation?.Name ?? ""; sample.structureTime = structural?.TrackTime ?? 0; sample.structureLoop = structural?.Loop ?? false;
            }
            private void ReadRenderer(ref MapFlickerTraceSample sample)
            {
                if (renderer == null) return;
                sample.rendererInstanceId = renderer.GetInstanceID(); sample.rendererEnabled = renderer.enabled;
                sample.rendererIsVisible = renderer.isVisible; sample.rendererBoundsCenter = renderer.bounds.center; sample.rendererBoundsExtents = renderer.bounds.extents;
                materials.Clear(); renderer.GetSharedMaterials(materials); sample.materialCount = materials.Count;
                if (materials.Count == 0 || materials[0] == null) return;
                var material = materials[0]; sample.firstMaterialId = material.GetInstanceID(); sample.firstShader = material.shader == null ? "" : material.shader.name;
                sample.firstTextureId = material.mainTexture == null ? 0 : material.mainTexture.GetInstanceID(); sample.renderQueue = material.renderQueue;
                if (material.HasProperty("_Cutoff")) sample.materialCutoff = material.GetFloat("_Cutoff");
                if (material.HasProperty("_PmaInput")) sample.materialPmaInput = material.GetFloat("_PmaInput");
            }
        }
    }
}
