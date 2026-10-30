/* 功能：协调明确目标、登记组、多点检测、平滑状态与单一GameplayCamera的模型透视。
 * 边界：不接管角色控制、动画、碰撞或时间；不扫描全场，不修改原材质alpha。
 * 协作：当前角色只读原CharacterControlCoordinator；正式渲染前投影并显式调用有限捕获后端。
 * 生命周期：关闭、切场和异常时按所有权归还显示位并销毁本模块资源。
 */
using System;
using System.Text;
using System.Collections.Generic;
using C = Ossastra.Characters;
using Ossastra.MapResearch;
using Ossastra.SceneVisibility.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ossastra.SceneVisibility
{
    [DisallowMultipleComponent]
    public sealed class SceneVisibilityRoot : MonoBehaviour
    {
        [Header("明确上下文与配置")]
        public Camera gameplayCamera;
        public MapResearchRoot mapView;
        public C.CharacterControlCoordinator coordinator;
        public VisibilitySettingsSO settings;
        public VisibilityTarget[] targets = Array.Empty<VisibilityTarget>();
        public SceneOccluderGroup[] groups = Array.Empty<SceneOccluderGroup>();
        [Header("运行开关（不会暂停世界）")]
        public bool effectEnabled = true;
        public bool protectCompanion = true;
        public bool detectionOnly;
        public bool showDiagnostics = true;
        [Header("本模块显示资源（只用于本相机）")]
        [Range(1, 31)] public int captureOnlyLayer = 30;
        public Shader opaqueShader;
        public Shader featherShader;
        public Shader copyDepthShader;
        public string Status { get; private set; } = "未启动";
        public int Queries { get; private set; }
        public int ActiveGroups { get; private set; }
        public int BudgetOverflow { get; private set; }
        public int UnsupportedInside { get; private set; }
        public int CaptureCount => capture == null ? 0 : capture.CapturedGroupCount;
        public string RenderError => capture == null ? "" : capture.LastError;
        public double DetectionMilliseconds { get; private set; }
        public double RenderMilliseconds { get; private set; }
        FiniteGroupCapture capture;
        VisibilitySettingsSO activeSettings;
        Camera boundCamera;
        VisibilityProxyScene proxyScene;
        readonly List<SpriteRenderer> registeredSprites = new List<SpriteRenderer>(64);
        SpriteRenderer[] spriteSnapshot = Array.Empty<SpriteRenderer>();
        int proxyRegistrySignature;
        GroupRuntime[] runtime;
        readonly VisibilityTarget[] selected = new VisibilityTarget[2];
        readonly bool[] visible = new bool[2];
        readonly RegisteredGroupFrame[] frames = new RegisteredGroupFrame[4];
        readonly float[] scores = new float[4];
        double nextDetection, lastTick;
        Vector3 lastCameraPosition;
        Quaternion lastCameraRotation;
        int lastFrame = -1;
        bool subscribed;
        string diagnostics = "";
        double nextDiagnostics;

        sealed class GroupRuntime
        {
            public SceneOccluderGroup Group;
            public readonly VisibilityState[] State = new VisibilityState[2];
            public readonly VisibilityDemand[] Demand = new VisibilityDemand[2];
            public readonly CaptureCircle[] Circle = new CaptureCircle[2];
            public readonly VisibilityTarget[] Target = new VisibilityTarget[2];
            public bool Support;
            public bool MainSupport;
            public bool CompanionSupport;
            public CaptureCircle PreserveSupport;
            public Collider[] SpriteProxies = Array.Empty<Collider>();
        }

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            try
            {
                if (!ValidateConfiguration(out string reason)) throw new InvalidOperationException(reason);
                // 配置在一次运行期保持一致；Inspector改SO后显式重接，不能混用旧状态时间和新预算。
                activeSettings = Instantiate(settings); activeSettings.hideFlags = HideFlags.HideAndDontSave;
                runtime = new GroupRuntime[groups.Length];
                for (int i = 0; i < groups.Length; ++i)
                {
                    runtime[i] = new GroupRuntime { Group = groups[i] };
                    for (int t = 0; t < 2; ++t) runtime[i].State[t] = new VisibilityState(activeSettings);
                }
                capture = new FiniteGroupCapture(gameplayCamera, captureOnlyLayer, opaqueShader, featherShader, copyDepthShader);
                boundCamera = gameplayCamera;
                proxyScene = new VisibilityProxyScene(); proxyRegistrySignature = 0;
                RenderPipelineManager.beginCameraRendering += BeginCamera;
                RenderPipelineManager.endCameraRendering += EndCamera;
                subscribed = true; lastFrame = -1; nextDetection = 0; lastTick = Time.unscaledTimeAsDouble;
                Status = "已就绪；仅处理明确GameplayCamera";
            }
            catch (Exception error) { Release(); Status = "配置拒绝：" + error.Message; Debug.LogError(Status, this); }
        }

        public bool ValidateConfiguration(out string reason)
        {
            reason = null;
            if (gameplayCamera == null || coordinator == null || settings == null) reason = "缺少明确相机、原控制协调器或配置";
            else if (!settings.TryValidate(out reason)) return false;
            else if (targets == null || targets.Length == 0 || groups == null || groups.Length == 0) reason = "目标和环境组必须显式登记";
            else if (opaqueShader == null || featherShader == null || copyDepthShader == null) reason = "缺少本模块Shader引用";
            else if (captureOnlyLayer < 1 || captureOnlyLayer > 31 || (gameplayCamera.cullingMask & (1 << captureOnlyLayer)) != 0) reason = "本场GameplayCamera必须明确排除捕获专用层";
            else
            {
                for (int i = 0; i < targets.Length; ++i)
                    if (targets[i] == null || targets[i].actor == null) { reason = "目标引用为空"; break; }
                for (int i = 0; reason == null && i < groups.Length; ++i)
                    if (groups[i] == null) reason = "环境组引用为空";
            }
            return reason == null;
        }

        void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (capture != null && boundCamera != gameplayCamera) { Reconnect(); }
            if (camera != gameplayCamera || capture == null) return;
            try
            {
                if (mapView != null && (!mapView.IsValid || mapView.IsStructureMode))
                { capture.RestoreOwnedState(); lastTick = Time.unscaledTimeAsDouble; lastFrame = -1; nextDetection = 0; return; }
                double now = Time.unscaledTimeAsDouble;
                bool newFrame = lastFrame != Time.frameCount;
                float delta = newFrame ? (float)Math.Max(0d, now - lastTick) : 0f;
                if (newFrame) { lastFrame = Time.frameCount; lastTick = now; }
                SelectTargets();
                RemapTargets();
                for (int t = 0; t < 2; ++t)
                {
                    bool previous = visible[t];
                    visible[t] = selected[t] != null && selected[t].Project(camera, activeSettings.minBodyPadding);
                    if (previous != visible[t]) nextDetection = 0; // 重入画面首个正式draw前重测。
                }
                bool queryEnabled = effectEnabled || detectionOnly;
                if (queryEnabled)
                {
                    RefreshSupport(); // 承载保护逐帧执行，不等待20Hz检测门；完全关闭也不查询支撑。
                    foreach (var item in runtime) if (item.Group != null) item.Group.PrepareForCamera(camera);
                    SynchronizeSpriteProxies();
                }
                bool cameraMoved = Vector3.SqrMagnitude(camera.transform.position - lastCameraPosition) > .16f || Quaternion.Angle(camera.transform.rotation, lastCameraRotation) > 2f;
                if (queryEnabled && (now >= nextDetection || cameraMoved))
                {
                    long start = System.Diagnostics.Stopwatch.GetTimestamp();
                    Detect(); DetectionMilliseconds = ElapsedMilliseconds(start);
                    nextDetection = now + 1d / activeSettings.detectionHz;
                    lastCameraPosition = camera.transform.position; lastCameraRotation = camera.transform.rotation;
                }
                else if (!queryEnabled)
                {
                    // 完全关闭应归还原图且停止查询；重启首帧必须以当前镜头/角色重新检测。
                    Queries = UnsupportedInside = 0; DetectionMilliseconds = 0; nextDetection = 0;
                }
                ActiveGroups = BudgetOverflow = 0;
                for (int i = 0; i < scores.Length; ++i) scores[i] = float.NegativeInfinity;
                for (int i = 0; i < runtime.Length; ++i)
                {
                    var item = runtime[i]; var group = item.Group;
                    bool active = group != null && group.isActiveAndEnabled;
                    float score = 0f, whole = 0f;
                    for (int t = 0; t < 2; ++t)
                    {
                        var demand = active && effectEnabled && visible[t] ? item.Demand[t] : new VisibilityDemand(false, 0f, 0f, false, item.Support);
                        item.State[t].Tick(demand, delta);
                        var state = item.State[t];
                        if (visible[t]) item.Circle[t] = new CaptureCircle { CenterViewport = selected[t].CenterViewport, InnerPixels = selected[t].CorePixels * state.RadiusMultiplier,
                            OuterPixels = selected[t].CorePixels * state.RadiusMultiplier + selected[t].BodyPixels * activeSettings.feather, Strength = state.LocalStrength * (1f - (group == null ? 1f : Mathf.Clamp01(group.minimumOpacity))) };
                        else { var circle = item.Circle[t]; circle.Strength = state.LocalStrength * (1f - (group == null ? 1f : Mathf.Clamp01(group.minimumOpacity))); item.Circle[t] = circle; }
                        whole = Mathf.Max(whole, state.WholeFade);
                        score += state.LocalStrength * (t == 0 ? 100f : 1f);
                    }
                    if (!active || !effectEnabled || score <= 0f || group.Resolved.Length == 0) continue;
                    ++ActiveGroups;
                    var frame = new RegisteredGroupFrame { StableId = group.GetInstanceID(), GeometryVersion = group.GeometryVersion, Members = group.Resolved,
                        CircleA = item.Circle[0], CircleB = item.Circle[1], PreserveSupport = item.PreserveSupport, WholeFade = Mathf.Min(whole, 1f - Mathf.Clamp01(group.minimumOpacity)) };
                    Insert(frame, score);
                }
                int count = Mathf.Min(4, Mathf.Min(ActiveGroups, activeSettings.maxGroups));
                BudgetOverflow = Mathf.Max(0, ActiveGroups - count);
                long renderStart = System.Diagnostics.Stopwatch.GetTimestamp();
                capture.BeginCamera(context, camera, frames, detectionOnly || !effectEnabled ? 0 : count);
                RenderMilliseconds = ElapsedMilliseconds(renderStart);
                if (now >= nextDiagnostics)
                {
                    Status = detectionOnly ? "仅检测；模型保持原样" : effectEnabled ? "运行中；活跃组 " + count + "/" + ActiveGroups : "效果关闭；原模型显示";
                    if (showDiagnostics) UpdateDiagnostics(); nextDiagnostics = now + .25d;
                }
            }
            catch (Exception error)
            {
                capture.RestoreOwnedState(); Status = "本帧保留原模型：" + error.Message;
                Debug.LogError(Status, this); enabled = false;
            }
        }

        void SelectTargets()
        {
            selected[0] = selected[1] = null;
            var current = coordinator.Current;
            if (current == null || !current.IsAlive || !current.isActiveAndEnabled) return;
            foreach (var target in targets) if (target != null && target.actor == current && target.Eligible) { selected[0] = target; break; }
            if (selected[0] == null) return; // 未登记实控不能借伙伴资格产生脱离主控的遮挡需求。
            if (!protectCompanion || activeSettings.maxTargets < 2) return;
            foreach (var target in targets) if (target != null && target != selected[0] && target.Eligible) { selected[1] = target; break; }
        }

        // 先按身份重排历史，再检测本帧需求；否则切控会把新需求与旧槽错配。
        void RemapTargets()
        {
            foreach (var item in runtime)
                for (int t = 0; t < 2; ++t)
                {
                    int old = selected[t] == null ? -1 : item.Target[0] == selected[t] ? 0 : item.Target[1] == selected[t] ? 1 : -1;
                    if (old != t && old >= 0)
                    {
                        var s = item.State[t]; item.State[t] = item.State[old]; item.State[old] = s;
                        var d = item.Demand[t]; item.Demand[t] = item.Demand[old]; item.Demand[old] = d;
                        var c = item.Circle[t]; item.Circle[t] = item.Circle[old]; item.Circle[old] = c;
                        var target = item.Target[t]; item.Target[t] = item.Target[old]; item.Target[old] = target;
                    }
                    if (item.Target[t] != selected[t])
                    {
                        item.State[t].Reset(); item.Demand[t] = default; item.Circle[t] = default; item.Target[t] = selected[t];
                        nextDetection = 0;
                    }
                }
        }
        void Detect()
        {
            Queries = UnsupportedInside = 0;
            for (int i = 0; i < runtime.Length; ++i)
            {
                var item = runtime[i]; var group = item.Group;
                for (int t = 0; t < 2; ++t)
                {
                    if (group == null || !group.isActiveAndEnabled || !visible[t]) { item.Demand[t] = new VisibilityDemand(false, 0, 0, false, item.Support); continue; }
                    var probe = VisibilityProbe.Evaluate(gameplayCamera, selected[t].StablePoints, group.QueryVolumes, selected[t].actor.transform);
                    Queries += probe.Queries; UnsupportedInside += probe.UnsupportedInside;
                    item.Demand[t] = new VisibilityDemand(probe.Eligible, probe.Coverage, probe.InsideProxy > 0 ? 1f : 0f,
                        group.allowWholeFade && !item.Support, item.Support);
                }
            }
        }

        void Insert(RegisteredGroupFrame frame, float score)
        {
            int limit = Mathf.Min(4, activeSettings.maxGroups);
            for (int slot = 0; slot < limit; ++slot)
                if (score > scores[slot])
                {
                    for (int move = limit - 1; move > slot; --move) { frames[move] = frames[move - 1]; scores[move] = scores[move - 1]; }
                    frames[slot] = frame; scores[slot] = score; break;
                }
        }
        void EndCamera(ScriptableRenderContext context, Camera camera) { if (capture != null) capture.EndCamera(context, camera); }
        // 参数变化只重接本模块；不重置角色、地图或原控制会话。
        public void Reconnect() { Release(); if (isActiveAndEnabled && Application.isPlaying) OnEnable(); }
        static double ElapsedMilliseconds(long start) => (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000d / System.Diagnostics.Stopwatch.Frequency;
        void RefreshSupport()
        {
            foreach (var item in runtime)
            {
                var group = item.Group;
                bool main = group != null && visible[0] && group.Supports(selected[0].actor.transform.position);
                bool buddy = group != null && visible[1] && group.Supports(selected[1].actor.transform.position);
                if (item.MainSupport != main || item.CompanionSupport != buddy) nextDetection = 0;
                item.MainSupport = main; item.CompanionSupport = buddy; item.Support = main || buddy;
                // 主控站台：整个承载顶盖保留，仅前景墙局部处理。伙伴站台：保留脚盘，主控仍可局部透视顶盖。
                if (group != null) group.Resolve(main);
                item.PreserveSupport = default;
                if (!main && buddy)
                {
                    Vector3 foot = gameplayCamera.WorldToViewportPoint(selected[1].actor.transform.position);
                    item.PreserveSupport = new CaptureCircle { CenterViewport = new Vector2(foot.x, foot.y),
                        InnerPixels = selected[1].BodyPixels * .28f, OuterPixels = selected[1].BodyPixels * .4f, Strength = 1f };
                }
            }
        }
        void SynchronizeSpriteProxies()
        {
            int signature = 17;
            unchecked { foreach (var item in runtime) if (item.Group != null) signature = signature * 31 + item.Group.GeometryVersion; }
            if (signature != proxyRegistrySignature)
            {
                registeredSprites.Clear();
                foreach (var item in runtime) if (item.Group != null)
                    foreach (var sprite in item.Group.SpriteMembers) if (sprite != null && !registeredSprites.Contains(sprite)) registeredSprites.Add(sprite);
                spriteSnapshot = registeredSprites.ToArray(); proxyRegistrySignature = signature;
            }
            proxyScene.Synchronize(spriteSnapshot, out bool changed);
            if (proxyScene.SyncTransformsIfDirty() || changed) nextDetection = 0;
            foreach (var item in runtime)
            {
                if (item.Group == null) continue;
                var sources = item.Group.SpriteMembers;
                if (item.SpriteProxies.Length != sources.Length) item.SpriteProxies = new Collider[sources.Length];
                for (int i = 0; i < sources.Length; ++i) item.SpriteProxies[i] = proxyScene.GetCollider(sources[i]);
                item.Group.SetTransientProxies(item.SpriteProxies);
            }
        }
        void UpdateDiagnostics()
        {
            var text = new StringBuilder();
            text.Append("原实控：").Append(coordinator.Current == null ? "无" : coordinator.Current.ActorId).Append("\n查询：").Append(Queries).Append("  超预算：").Append(BudgetOverflow)
                .Append("  非凸内部未确定：").Append(UnsupportedInside).Append('\n');
            foreach (var item in runtime)
            {
                if (item.Group == null) continue;
                text.Append(item.Group.groupLabel).Append("：").Append(item.State[0].LocalStrength.ToString("F2")).Append(" / 半径×")
                    .Append(item.State[0].RadiusMultiplier.ToString("F2")).Append(" / 整组")
                    .Append(Mathf.Max(item.State[0].WholeFade, item.State[1].WholeFade).ToString("F2"));
                if (item.MainSupport) text.Append(" [保留主控整块承载面]");
                else if (item.CompanionSupport) text.Append(" [保留伙伴脚盘]");
                text.Append('\n');
            }
            diagnostics = text.ToString();
        }
        void OnGUI()
        {
            if (!Application.isPlaying || !showDiagnostics) return;
            GUILayout.BeginArea(new Rect(Mathf.Max(5, Screen.width - 370), 65, 360, 320), GUI.skin.box);
            GUILayout.Label("模型遮挡透视 · KT-VIS-01");
            effectEnabled = GUILayout.Toggle(effectEnabled, "启用模型柔边透视");
            detectionOnly = GUILayout.Toggle(detectionOnly, "仅检测（保留模型原样）");
            protectCompanion = GUILayout.Toggle(protectCompanion, "保护同屏存活伙伴");
            GUILayout.Label(Status); GUILayout.Label(diagnostics);
            if (!string.IsNullOrEmpty(RenderError)) GUILayout.Label(RenderError);
            GUILayout.EndArea();
        }
        void OnDisable() => Release();
        void OnDestroy() => Release();
        void Release()
        {
            if (subscribed)
            {
                RenderPipelineManager.beginCameraRendering -= BeginCamera;
                RenderPipelineManager.endCameraRendering -= EndCamera;
                subscribed = false;
            }
            capture?.Dispose(); capture = null; boundCamera = null;
            if (runtime != null) foreach (var item in runtime) if (item.Group != null) item.Group.ClearTransientProxies(item.SpriteProxies);
            proxyScene?.Dispose(); proxyScene = null; spriteSnapshot = Array.Empty<SpriteRenderer>(); registeredSprites.Clear();
            runtime = null; ActiveGroups = BudgetOverflow = 0;
            if (activeSettings != null) { Destroy(activeSettings); activeSettings = null; }
        }
    }
}
