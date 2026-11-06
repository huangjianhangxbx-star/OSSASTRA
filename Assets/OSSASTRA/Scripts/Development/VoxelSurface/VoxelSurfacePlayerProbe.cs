/* 功能：仅Windows Player显式命令行启用的主场烟测；绝不挂入或保存源场景。
 * 计帧：只认可真实gameplayCamera的endCameraRendering，同一Unity帧去重；EndOfFrame仅用于屏幕捕获。
 * 边界：读取保存主场的22个比较绑定与Shader引用；只临时切材质/时刻/观察中心，不写作者档或业务。
 */
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Ossastra.CharacterLighting;
using Ossastra.Commerce.Runtime;
using Ossastra.EnvironmentSimulation;
using Ossastra.Inventory;
using Ossastra.MapResearch;
using Ossastra.SceneVisibility.Rooms.Rendering;
using Ossastra.WorldRendering.Rendering;
using Ossastra.WorldRendering.VoxelSurface;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Ossastra.Development.VoxelSurface
{
    public sealed class VoxelSurfacePlayerProbe : MonoBehaviour
    {
        const string Flag = "-voxel-light-smoke";
        const string MainScene = "Assets/OSSASTRA/GroundWorkshop/Samples/KT-VIS-04/SC_RoomVisibilityLab.unity";
        const double TimeoutSeconds = 60;
        static bool booted;
        string directory;
        Stopwatch clock;
        Camera mainCamera;
        VoxelSurfaceComparison comparison;
        EnvironmentRoot environment;
        MapResearchRoot map;
        ProfilerRecorder drawCalls, setPassCalls;
        string drawUnavailable = "unavailable: recorder not started", setPassUnavailable = "unavailable: recorder not started";
        int cameraEvents, renderedFrames, lastRenderedFrame = -1;
        bool finished, subscribed, snapshotTaken, logsSubscribed;
        double originalHours;
        bool originalClockRunning, originalManualSun, originalFollow;
        EnvironmentTimeMode originalTimeMode;
        VoxelSurfaceComparisonMode originalMode;
        Vector3 originalFixedTarget;
        float originalFollowSmooth;
        string environmentBefore, charactersBefore, profilesBefore, sourceAuthorsBefore;
        readonly object logGate = new object();
        readonly List<string> startupErrors = new List<string>();
        int startupErrorCount;
        bool smokeAuthorized, sceneHookSubscribed, commerceIsolated, commerceBaselineCaptured;
        CommerceService isolatedCommerce;
        int isolatedSceneHandle;
        string originalCommerceDirectory, isolatedCommerceDirectory, originalCommerceProfile;
        CommerceFile[] originalCommerceFiles;
        readonly Summary report = new Summary();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            var arguments = System.Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, Flag);
            if (index < 0 || booted) return;
            booted = true;
            try
            {
                if (index + 1 >= arguments.Length || !Path.IsPathFullyQualified(arguments[index + 1]))
                    throw new ArgumentException("-voxel-light-smoke必须携带绝对报告目录");
                string path = Path.GetFullPath(arguments[index + 1]);
                Directory.CreateDirectory(path);
                var host = new GameObject("VoxelLightSmoke_CommandLineOnly") { hideFlags = HideFlags.HideAndDontSave };
                host.SetActive(false); DontDestroyOnLoad(host);
                var probe = host.AddComponent<VoxelSurfacePlayerProbe>();
                probe.directory = path; probe.clock = Stopwatch.StartNew(); probe.smokeAuthorized = true;
                SceneManager.sceneLoaded += probe.SmokeSceneLoaded; probe.sceneHookSubscribed = true;
                Application.logMessageReceivedThreaded += probe.LogReceived; probe.logsSubscribed = true;
                host.SetActive(true);
            }
            catch (Exception error)
            {
                // Quit可能等到本帧结束；失败也须在Start前阻断库存恢复，不能让它落到原档。
                SceneManager.sceneLoaded += AbortSmokeStartup;
                Debug.LogError("VOXEL_PLAYER_RESULT " + JsonUtility.ToJson(new Summary { pass = false, reason = error.Message }));
                Application.Quit(1);
            }
        }

        static List<(InventorySession Session, bool Enabled)> BlockInventoryStart(Scene scene)
        {
            var states = new List<(InventorySession, bool)>();
            foreach (var root in scene.GetRootGameObjects()) foreach (var inventory in root.GetComponentsInChildren<InventorySession>(true))
            { states.Add((inventory, inventory.enabled)); inventory.enabled = false; }
            return states;
        }
        static void AbortSmokeStartup(Scene scene, LoadSceneMode mode)
        { SceneManager.sceneLoaded -= AbortSmokeStartup; BlockInventoryStart(scene); }
        void SmokeSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                if (!IsolateCommerceBeforeStart(scene, false)) throw new InvalidOperationException("Smoke隔离上下文缺失");
            }
            catch (Exception error)
            { BlockInventoryStart(scene); Finish(false, "Commerce隔离失败，库存Start保持禁用：" + error.Message); }
        }
        bool IsolateCommerceBeforeStart(Scene scene, bool allowPreviewFixture)
        {
            if (!smokeAuthorized || string.IsNullOrEmpty(directory)) return false;
            if (commerceIsolated)
                return scene.handle == isolatedSceneHandle && isolatedCommerce != null && isolatedCommerce.saveDirectory == isolatedCommerceDirectory;
            var inventories = BlockInventoryStart(scene);
            if (!scene.IsValid() || !scene.isLoaded || !allowPreviewFixture && scene.path != MainScene)
                throw new InvalidOperationException("拒绝非主场隔离：" + scene.path);
            var service = Single(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CommerceService>(true)), "主场CommerceService");
            if (service.inventory == null || service.inventory.gameObject.scene != scene) throw new InvalidOperationException("Commerce缺少本Scene明确InventorySession");
            if (service.SavePath != null || service.SaveStatus != null) throw new InvalidOperationException("隔离过晚：Commerce Store已经建立");
            string source = service.saveDirectory;
            isolatedCommerceDirectory = Path.GetFullPath(Path.Combine(directory, "isolated-commerce"));
            // 先定向内存字段，再做任何可能失败的源目录I/O；失败时Inventory不会重新启用。
            service.saveDirectory = isolatedCommerceDirectory;
            isolatedCommerce = service; isolatedSceneHandle = scene.handle; originalCommerceProfile = service.profileId;
            originalCommerceDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(source) ? Path.Combine(Application.persistentDataPath, "Commerce") : source);
            if (Within(originalCommerceDirectory, isolatedCommerceDirectory) || Within(isolatedCommerceDirectory, originalCommerceDirectory))
                throw new InvalidOperationException("原档与隔离目录必须分离且不能互相包含");
            if (Directory.Exists(originalCommerceDirectory) && (File.GetAttributes(originalCommerceDirectory) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("原档目录是重解析点，拒绝不明复制范围");
            Directory.CreateDirectory(isolatedCommerceDirectory);
            if ((File.GetAttributes(isolatedCommerceDirectory) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("隔离目录不能是重解析点");
            originalCommerceFiles = CommerceFiles(originalCommerceDirectory); commerceBaselineCaptured = true;
            report.originalCommerceDirectory = originalCommerceDirectory; report.isolatedCommerceDirectory = isolatedCommerceDirectory;
            report.commerceProfileId = originalCommerceProfile; report.originalCommerceBefore = originalCommerceFiles;
            report.originalCommerceFingerprintBefore = CommerceFingerprint(originalCommerceFiles);
            foreach (var file in originalCommerceFiles)
            {
                if (file.name == "<directory-missing>") continue;
                string target = Path.Combine(isolatedCommerceDirectory, file.name);
                if (File.Exists(target))
                {
                    if ((File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0) throw new IOException("隔离目标不能是重解析文件：" + target);
                    ++report.commerceExistingFilesPreserved; continue;
                }
                File.Copy(Path.Combine(originalCommerceDirectory, file.name), target, false); ++report.commerceFilesCopied;
            }
            if (report.originalCommerceFingerprintBefore != CommerceFingerprint(CommerceFiles(originalCommerceDirectory)))
                throw new InvalidOperationException("复制期间原目录指纹发生变化");
            if (service.saveDirectory != isolatedCommerceDirectory || service.profileId != originalCommerceProfile || service.SavePath != null)
                throw new InvalidOperationException("隔离路由读回不一致或Store提前建立");
            commerceIsolated = report.commerceIsolationReady = true;
            foreach (var state in inventories) if (state.Session != null) state.Session.enabled = state.Enabled;
            return true;
        }
        static bool Within(string path, string parent)
        {
            path = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            parent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return path.Equals(parent, StringComparison.OrdinalIgnoreCase) || path.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        static CommerceFile[] CommerceFiles(string path)
        {
            if (!Directory.Exists(path)) return new[] { new CommerceFile { name = "<directory-missing>", sha256 = "MISSING" } };
            return Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly).OrderBy(f => f, StringComparer.Ordinal).Select(file =>
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("拒绝原目录重解析文件：" + file);
                using (var hash = SHA256.Create()) using (var stream = File.OpenRead(file))
                    return new CommerceFile { name = Path.GetFileName(file), length = stream.Length, sha256 = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "") };
            }).ToArray();
        }
        static string CommerceFingerprint(CommerceFile[] files)
        { return string.Join("\n", files.Select(f => f.name + "|" + f.length + "|" + f.sha256)); }
        void DetachSceneHook()
        { if (sceneHookSubscribed) { SceneManager.sceneLoaded -= SmokeSceneLoaded; sceneHookSubscribed = false; } }
        void OnDestroy()
        {
            DetachSceneHook();
            if (logsSubscribed) { Application.logMessageReceivedThreaded -= LogReceived; logsSubscribed = false; }
            // 不恢复Commerce目录：其Quit/Disable/Destroy保存仍必须指向本进程隔离Store。
        }

        IEnumerator Start()
        {
            // 即使误把组件加到场景，没有Bootstrap注入的显式命令行上下文也完全不运行。
            if (clock == null || string.IsNullOrEmpty(directory)) { enabled = false; yield break; }
            var run = Run();
            while (!finished)
            {
                bool next;
                try { next = run.MoveNext(); }
                catch (Exception error) { Finish(false, error.ToString()); yield break; }
                if (!next) yield break;
                yield return run.Current;
            }
        }

        void Update()
        {
            if (!finished && clock != null && clock.Elapsed.TotalSeconds >= TimeoutSeconds)
                Finish(false, "真实60秒超时；当前真实主相机帧=" + renderedFrames + "，已完成阶段=" + report.phases.Count);
        }

        IEnumerator Run()
        {
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("需要可见图形Player，拒绝batchmode或Null图形设备");
            Scene scene;
            do { yield return null; scene = SceneManager.GetActiveScene(); }
            while (!scene.IsValid() || !scene.isLoaded || scene.GetRootGameObjects().Length == 0);
            var roots = scene.GetRootGameObjects();
            comparison = Single(roots.SelectMany(r => r.GetComponentsInChildren<VoxelSurfaceComparison>(true)), "主场comparison");
            environment = Single(roots.SelectMany(r => r.GetComponentsInChildren<EnvironmentRoot>(true)), "主场EnvironmentRoot");
            map = Single(roots.SelectMany(r => r.GetComponentsInChildren<MapResearchRoot>(true)), "主场MapResearchRoot");
            if (!scene.path.EndsWith("/SC_RoomVisibilityLab.unity", StringComparison.Ordinal))
                throw new InvalidOperationException("拒绝非保存主场：" + scene.path);
            if (comparison.bindings == null || comparison.bindings.Length != 22 ||
                comparison.bindings.Any(b => b == null || b.renderer == null || !ValidAuthorArrays(b)) || comparison.bindings.Select(b => b.renderer).Distinct().Count() != 22)
                throw new InvalidOperationException("主场必须明确保存22个唯一有效Renderer比较绑定");
            if (!comparison.isActiveAndEnabled || comparison.calibratedProfile == null || comparison.shapedProfile == null)
                throw new InvalidOperationException("保存主场缺少启用comparison或B/C Profile引用");
            mainCamera = environment.gameplayCamera;
            if (mainCamera == null || mainCamera != map.gameplayCamera || !mainCamera.isActiveAndEnabled)
                throw new InvalidOperationException("主场Environment/Map未共同指向实际启用的gameplayCamera");
            while (!map.IsValid) yield return null;
            if (!commerceIsolated || isolatedCommerce == null || isolatedCommerce.profileId != originalCommerceProfile ||
                !Within(isolatedCommerce.SavePath ?? "", isolatedCommerceDirectory)) throw new InvalidOperationException("真实Commerce Store未指向烟测隔离目录");
            report.scene = scene.path; report.camera = mainCamera.name;
            RenderPipelineManager.endCameraRendering += CameraEnded; subscribed = true;
            StartRecorder("Draw Calls Count", out drawCalls, out drawUnavailable);
            StartRecorder("SetPass Calls Count", out setPassCalls, out setPassUnavailable);
            while (renderedFrames < 60) yield return null;
            report.coldStartActualCameraFrames = renderedFrames;
            Snapshot();

            map.followParticipant = false; map.fixedTarget = new Vector3(24, 0, -9); map.followSmoothTime = 0;
            environment.timeMode = EnvironmentTimeMode.Manual; environment.clockRunning = false; environment.manualSun = false;
            var modes = new[] { VoxelSurfaceComparisonMode.Original, VoxelSurfaceComparisonMode.Calibrated, VoxelSurfaceComparisonMode.Faceted };
            foreach (var mode in modes) foreach (double hours in new double[] { 12, 18, 0 })
            {
                comparison.ApplyMode(mode); environment.SetTimeHours(hours);
                int startFrame = renderedFrames;
                while (renderedFrames - startFrame < 20) yield return null;
                // 只在已经满足实际渲染帧之后等待屏幕完成；不以EOF或图片数量代替绘制成功。
                yield return new WaitForEndOfFrame();
                var phase = Capture(mode, hours, startFrame);
                report.phases.Add(phase);
                File.WriteAllText(Path.Combine(directory, phase.name + ".json"), JsonUtility.ToJson(phase, true));
                Debug.Log("VOXEL_PLAYER_PHASE " + JsonUtility.ToJson(phase));
                if (!phase.pass) throw new InvalidOperationException("阶段未通过：" + phase.name + "；" + phase.reason);
            }
            map.followParticipant = true;
            comparison.ApplyMode(VoxelSurfaceComparisonMode.Calibrated); environment.SetTimeHours(12);
            int witnessStart = renderedFrames;
            while (renderedFrames - witnessStart < 20) yield return null;
            yield return new WaitForEndOfFrame();
            CaptureCharacterWitness(witnessStart);
            if (!report.characterWitnessPassed) throw new InvalidOperationException(report.characterWitnessReason);
            Finish(true, "A/B/C九阶段及原主控跟随角色见证均经过至少20真实主相机帧；冷启至少60真实帧");
        }

        void CaptureCharacterWitness(int startFrame)
        {
            report.characterWitnessActualCameraFrames = renderedFrames - startFrame;
            report.characterWitnessScreenshot = Path.Combine(directory, "CharacterWitness.png");
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            if (image == null) throw new InvalidOperationException("角色见证Native ScreenCapture未返回纹理");
            try { File.WriteAllBytes(report.characterWitnessScreenshot, ImageConversion.EncodeToPNG(image)); }
            finally { Destroy(image); }
            if (CharacterLightingSystem.TryGet(mainCamera, out var lighting))
            {
                var receivers = lighting.receivers;
                report.characterWitnessProfilesPresent = receivers != null && receivers.Length == 2 &&
                    receivers.All(r => r != null && r.profile != null && r.profile.settings != null && r.profile.settings.outerGlow) &&
                    receivers.Select(r => r.profile).Distinct().Count() == 2 && charactersBefore == CharacterAuthors();
                report.characterWitnessProfiles = receivers == null ? Array.Empty<string>() :
                    receivers.Select(r => r == null || r.profile == null ? "MISSING" : r.profile.name).ToArray();
                report.characterWitnessCoverageActors = lighting.CoverageActors;
                report.characterWitnessCoverageDraws = lighting.CoverageDraws; report.characterWitnessCoverageBytes = lighting.CoverageBytes;
                report.characterWitnessOuterDraws = lighting.OuterGlowDraws; report.characterWitnessOuterBytes = lighting.OuterGlowBytes;
            }
            report.characterWitnessPassed = report.characterWitnessActualCameraFrames >= 20 && report.characterWitnessProfilesPresent &&
                report.characterWitnessCoverageDraws > 0 && report.characterWitnessCoverageBytes > 0 &&
                report.characterWitnessOuterDraws > 0 && report.characterWitnessOuterBytes > 0;
            report.characterWitnessReason = report.characterWitnessPassed ? "PASS: original follow camera and both approved Profiles; real coverage/outer draws and bytes" :
                "角色跟随见证失败：两个原批准Profile/实际覆盖/外缘指标未全部有效；未移动角色或修改源Profile";
            Debug.Log("VOXEL_PLAYER_CHARACTER_WITNESS " + report.characterWitnessReason);
        }

        void CameraEnded(ScriptableRenderContext context, Camera renderedCamera)
        {
            if (renderedCamera != mainCamera) return;
            ++cameraEvents;
            if (lastRenderedFrame == Time.frameCount) return;
            lastRenderedFrame = Time.frameCount; ++renderedFrames;
        }

        void Snapshot()
        {
            originalMode = comparison.mode; originalHours = environment.timeHours; originalTimeMode = environment.timeMode;
            report.startingMode = originalMode.ToString();
            originalClockRunning = environment.clockRunning; originalManualSun = environment.manualSun;
            originalFollow = map.followParticipant; originalFixedTarget = map.fixedTarget; originalFollowSmooth = map.followSmoothTime;
            environmentBefore = JsonUtility.ToJson(environment); charactersBefore = CharacterAuthors(); profilesBefore = VoxelAuthors();
            sourceAuthorsBefore = SourceAuthorsFingerprint(); report.sourceAuthorsBefore = sourceAuthorsBefore;
            report.scene = comparison.gameObject.scene.path; report.camera = mainCamera.name;
            report.graphicsDevice = SystemInfo.graphicsDeviceName; report.graphicsApi = SystemInfo.graphicsDeviceType.ToString();
            report.bindingCount = comparison.bindings.Length; report.reportDirectory = directory;
            report.environmentAuthorsBefore = environmentBefore; report.characterProfileAuthorsBefore = charactersBefore;
            snapshotTaken = true;
        }

        Phase Capture(VoxelSurfaceComparisonMode mode, double hours, int startFrame)
        {
            var phase = new Phase { name = mode + "_" + hours.ToString("00", System.Globalization.CultureInfo.InvariantCulture),
                mode = mode.ToString(), requestedHours = hours, actualCameraFrames = renderedFrames - startFrame,
                totalActualCameraFrames = renderedFrames, actualCameraEndEvents = cameraEvents,
                comparisonStatus = comparison.Status, elapsedSeconds = clock.Elapsed.TotalSeconds };
            phase.expectedVoxelProfile = mode == VoxelSurfaceComparisonMode.Original ? "A: exact original material references" :
                JsonUtility.ToJson(mode == VoxelSurfaceComparisonMode.Calibrated ? comparison.calibratedProfile.settings : comparison.shapedProfile.settings);
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            if (image == null) throw new InvalidOperationException("Native ScreenCapture未返回屏幕纹理");
            try
            {
                phase.screenWidth = image.width; phase.screenHeight = image.height;
                phase.screenshot = Path.Combine(directory, phase.name + ".png");
                File.WriteAllBytes(phase.screenshot, ImageConversion.EncodeToPNG(image));
            }
            finally { Destroy(image); }
            var families = new Dictionary<string, int>(); var sourceMaterials = new HashSet<Material>();
            bool materialRoutesValid = true;
            foreach (var binding in comparison.bindings)
            {
                var current = binding.renderer.sharedMaterials;
                var authored = mode == VoxelSurfaceComparisonMode.Original ? binding.original : mode == VoxelSurfaceComparisonMode.Calibrated ? binding.calibrated : binding.shaped;
                if (authored == null || authored.Length == 0 || current.Length != authored.Length) { materialRoutesValid = false; continue; }
                for (int slot = 0; slot < current.Length; ++slot)
                {
                    var material = current[slot]; var shader = material == null ? null : material.shader;
                    string family = shader == null ? "MISSING" : shader.name;
                    families[family] = families.TryGetValue(family, out var count) ? count + 1 : 1;
                    if (material != null) sourceMaterials.Add(material);
                    materialRoutesValid &= shader != null && shader.isSupported && authored[slot] != null && shader == authored[slot].shader;
                    if (mode == VoxelSurfaceComparisonMode.Original) materialRoutesValid &= material == authored[slot];
                    else materialRoutesValid &= family == "Ossastra/WorldRendering/VoxelLit" &&
                        ControlsMatch(material, mode == VoxelSurfaceComparisonMode.Calibrated ? comparison.calibratedProfile.settings : comparison.shapedProfile.settings);
                }
            }
            phase.sourceFamilies = families.Select(p => new Family { shader = p.Key, slots = p.Value }).ToArray();
            phase.selectedSourceMaterials = sourceMaterials.Count; phase.materialRoutesValid = materialRoutesValid;
            phase.loadedMaterialObjects = Resources.FindObjectsOfTypeAll<Material>().Length;
            phase.loadedRenderTextureObjects = Resources.FindObjectsOfTypeAll<RenderTexture>().Length;
            phase.comparisonOwnedCacheMaterials = ComparisonCacheCount();
            phase.comparisonCacheMetric = phase.comparisonOwnedCacheMaterials < 0 ? "unavailable: cache field not exposed" : "actual private cache count";
            if (RoomMaskCameraRegistry.TryGetStatistics(mainCamera, out var stats) && stats != null)
            {
                phase.positiveActive = stats.Active; phase.positiveDraws = stats.SurfaceDraws; phase.positiveError = stats.Error;
                phase.positiveOwnedMaterials = stats.OwnedMaterialCount; phase.positiveOwnedTextures = stats.OwnedTextureCount;
                phase.positiveOwnedBuffers = stats.OwnedBufferCount; phase.positiveBufferBytes = stats.EstimatedBufferBytes;
                phase.positiveTextureBytes = stats.EstimatedTextureBytes; phase.sourceBuckets = stats.SourceMaterialBucketCount;
                phase.positivePropertyBlocks = stats.OwnedPropertyBlockCount; phase.retainedMaterialConflicts = stats.RetainedMaterialConflicts;
            }
            else phase.positiveError = "unavailable: no real main-camera statistics";
            foreach (var feature in Resources.FindObjectsOfTypeAll<WorldLookRendererFeature>())
            { phase.worldLookOwnedMaterials += feature.OwnedMaterialCount; phase.worldLookOwnedTextures += feature.OwnedTextureCount; }
            if (CharacterLightingSystem.TryGet(mainCamera, out var lighting))
            { phase.characterCoverageDraws = lighting.CoverageDraws; phase.characterCoverageBytes = lighting.CoverageBytes;
                phase.characterOuterDraws = lighting.OuterGlowDraws; phase.characterOuterBytes = lighting.OuterGlowBytes; }
            var frame = environment.Current;
            phase.actualHours = frame == null ? -1 : frame.Hours; phase.sunIntensity = frame == null ? -1 : frame.SunIntensity;
            phase.sunDirection = frame == null ? Vector3.zero : frame.SunDirection; phase.ambientIntensity = frame == null ? -1 : frame.AmbientIntensity;
            phase.weather = frame == null ? "unavailable" : frame.WeatherName;
            phase.drawCalls = ReadRecorder(drawCalls, drawUnavailable, out phase.drawCallsStatus);
            phase.setPassCalls = ReadRecorder(setPassCalls, setPassUnavailable, out phase.setPassStatus);
            phase.profilerScope = "global last complete frame; not per-camera or GPU timing";
            phase.pass = phase.actualCameraFrames >= 20 && materialRoutesValid && phase.positiveActive && phase.positiveDraws > 0
                && string.IsNullOrEmpty(phase.positiveError) && Math.Abs(phase.actualHours - hours) < .001 && phase.screenWidth > 0 && phase.screenHeight > 0;
            phase.reason = phase.pass ? "PASS" : "real frames/material routes/positive draw/environment hour/screen capture check failed";
            return phase;
        }

        void Finish(bool pass, string reason)
        {
            if (finished) return; finished = true;
            DetachSceneHook();
            if (subscribed) { RenderPipelineManager.endCameraRendering -= CameraEnded; subscribed = false; }
            try { drawCalls.Dispose(); setPassCalls.Dispose(); }
            catch (Exception error) { pass = false; reason += "; recorder dispose: " + error.Message; }
            try
            {
                if (snapshotTaken)
                {
                    comparison.ApplyMode(originalMode);
                    environment.timeMode = originalTimeMode; environment.clockRunning = originalClockRunning; environment.manualSun = originalManualSun;
                    environment.SetTimeHours(originalHours);
                    map.followParticipant = originalFollow; map.fixedTarget = originalFixedTarget; map.followSmoothTime = originalFollowSmooth;
                    report.environmentAuthorsUnchanged = environmentBefore == JsonUtility.ToJson(environment);
                    report.characterProfileAuthorsUnchanged = charactersBefore == CharacterAuthors();
                    report.voxelProfileAuthorsUnchanged = profilesBefore == VoxelAuthors();
                    report.sourceAuthorsAfter = SourceAuthorsFingerprint();
                    report.sourceMaterialAuthorsUnchanged = sourceAuthorsBefore == report.sourceAuthorsAfter;
                    report.restoredCacheMaterials = ComparisonCacheCount(); report.authorCacheLimit = AuthorCacheLimit();
                    report.privateCacheBounded = report.restoredCacheMaterials >= 0 && report.restoredCacheMaterials <= report.authorCacheLimit;
                    report.runtimeSettingsRestored = comparison.mode == originalMode && environment.timeHours == originalHours &&
                        map.followParticipant == originalFollow && map.fixedTarget == originalFixedTarget && map.followSmoothTime == originalFollowSmooth && SelectionRestored();
                    pass &= report.environmentAuthorsUnchanged && report.characterProfileAuthorsUnchanged && report.voxelProfileAuthorsUnchanged &&
                        report.sourceMaterialAuthorsUnchanged && report.privateCacheBounded && report.runtimeSettingsRestored;
                }
                else pass = false;
            }
            catch (Exception error) { pass = false; reason += "; restore: " + error.Message; }
            try
            {
                if (commerceBaselineCaptured)
                {
                    report.originalCommerceAfter = CommerceFiles(originalCommerceDirectory);
                    report.originalCommerceFingerprintAfter = CommerceFingerprint(report.originalCommerceAfter);
                    report.originalCommerceUnchanged = report.originalCommerceFingerprintBefore == report.originalCommerceFingerprintAfter;
                }
                report.commerceActualSavePath = isolatedCommerce == null ? null : isolatedCommerce.SavePath;
                pass &= commerceIsolated && report.originalCommerceUnchanged && isolatedCommerce != null && isolatedCommerce.profileId == originalCommerceProfile &&
                    isolatedCommerce.saveDirectory == isolatedCommerceDirectory && !string.IsNullOrEmpty(report.commerceActualSavePath) && Within(report.commerceActualSavePath, isolatedCommerceDirectory);
            }
            catch (Exception error) { pass = false; reason += "; commerce verification: " + error.Message; }
            if (logsSubscribed) { Application.logMessageReceivedThreaded -= LogReceived; logsSubscribed = false; }
            lock (logGate) { report.runtimeErrorCount = startupErrorCount; report.runtimeErrors = startupErrors.ToArray(); }
            if (report.runtimeErrorCount > 0) { pass = false; reason += "; Player Error/Assert/Exception=" + report.runtimeErrorCount; }
            if (clock != null && clock.Elapsed.TotalSeconds >= TimeoutSeconds)
            { pass = false; reason = "真实60秒超时；" + reason; }
            report.pass = pass; report.reason = reason; report.totalActualCameraFrames = renderedFrames;
            report.actualCameraEndEvents = cameraEvents; report.elapsedSeconds = clock == null ? 0 : clock.Elapsed.TotalSeconds;
            try { File.WriteAllText(Path.Combine(directory, "result.json"), JsonUtility.ToJson(report, true)); }
            catch (Exception error) { report.pass = false; report.reason += "; report write: " + error.Message; }
            Debug.Log("VOXEL_PLAYER_RESULT " + JsonUtility.ToJson(report));
            Application.Quit(report.pass ? 0 : 1);
        }

        string CharacterAuthors()
        {
            if (!CharacterLightingSystem.TryGet(mainCamera, out var lighting)) throw new InvalidOperationException("真实主相机角色光感上下文不可读，拒绝声称作者值保持");
            return string.Join("\n", lighting.receivers.Select(r => r == null || r.profile == null ? "MISSING" : JsonUtility.ToJson(r.profile.settings)));
        }
        string VoxelAuthors()
        { return JsonUtility.ToJson(comparison.calibratedProfile.settings) + "\n" + JsonUtility.ToJson(comparison.shapedProfile.settings); }
        bool SelectionRestored()
        {
            var profile = originalMode == VoxelSurfaceComparisonMode.Calibrated ? comparison.calibratedProfile : comparison.shapedProfile;
            foreach (var binding in comparison.bindings)
            {
                var authored = originalMode == VoxelSurfaceComparisonMode.Original ? binding.original :
                    originalMode == VoxelSurfaceComparisonMode.Calibrated ? binding.calibrated : binding.shaped;
                var current = binding.renderer.sharedMaterials;
                if (authored == null || current.Length != authored.Length) return false;
                for (int slot = 0; slot < current.Length; ++slot)
                {
                    if (originalMode == VoxelSurfaceComparisonMode.Original) { if (current[slot] != authored[slot]) return false; }
                    else if (current[slot] == null || authored[slot] == null || current[slot].shader != authored[slot].shader ||
                        !current[slot].shader.isSupported || profile == null || !ControlsMatch(current[slot], profile.settings)) return false;
                }
            }
            return true;
        }
        int ComparisonCacheCount()
        {
            var field = typeof(VoxelSurfaceComparison).GetField("cache", BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null && field.GetValue(comparison) is IDictionary cache ? cache.Count : -1;
        }
        int AuthorCacheLimit()
        {
            var keys = new HashSet<(int Material, int Profile)>();
            foreach (var binding in comparison.bindings)
            {
                if (comparison.calibratedProfile != null) foreach (var material in binding.calibrated)
                    if (material != null) keys.Add((material.GetInstanceID(), comparison.calibratedProfile.GetInstanceID()));
                if (comparison.shapedProfile != null) foreach (var material in binding.shaped)
                    if (material != null) keys.Add((material.GetInstanceID(), comparison.shapedProfile.GetInstanceID()));
            }
            return keys.Count;
        }
        string SourceAuthorsFingerprint()
        {
            var text = new StringBuilder(); var sources = new HashSet<Material>();
            foreach (var binding in comparison.bindings)
            {
                text.Append(binding.renderer.GetInstanceID()).Append(':');
                foreach (var array in new[] { binding.original, binding.calibrated, binding.shaped })
                {
                    text.Append('[');
                    foreach (var material in array) { text.Append(material == null ? 0 : material.GetInstanceID()).Append(','); if (material != null) sources.Add(material); }
                    text.Append(']');
                }
            }
            foreach (var material in sources.OrderBy(m => m.GetInstanceID()))
            {
                var shader = material.shader;
                text.Append(material.GetInstanceID()).Append('|').Append(material.name).Append('|').Append(shader == null ? 0 : shader.GetInstanceID())
                    .Append('|').Append(material.renderQueue).Append('|').Append(material.enableInstancing).Append('|').Append(material.doubleSidedGI)
                    .Append('|').Append((int)material.globalIlluminationFlags).Append('|').Append(string.Join(",", material.shaderKeywords.OrderBy(k => k)));
                if (shader == null) continue;
                for (int property = 0; property < shader.GetPropertyCount(); ++property)
                {
                    string name = shader.GetPropertyName(property); text.Append('|').Append(name).Append('=');
                    switch (shader.GetPropertyType(property))
                    {
                        case ShaderPropertyType.Color: var color = material.GetColor(name); AppendVector(text, new Vector4(color.r, color.g, color.b, color.a)); break;
                        case ShaderPropertyType.Vector: AppendVector(text, material.GetVector(name)); break;
                        case ShaderPropertyType.Float:
                        case ShaderPropertyType.Range: text.Append(material.GetFloat(name).ToString("R", System.Globalization.CultureInfo.InvariantCulture)); break;
                        case ShaderPropertyType.Int: text.Append(material.GetInt(name)); break;
                        case ShaderPropertyType.Texture:
                            var texture = material.GetTexture(name); var scale = material.GetTextureScale(name); var offset = material.GetTextureOffset(name);
                            text.Append(texture == null ? 0 : texture.GetInstanceID()); AppendVector(text, new Vector4(scale.x, scale.y, offset.x, offset.y)); break;
                    }
                }
            }
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "");
        }
        static void AppendVector(StringBuilder text, Vector4 value)
        {
            foreach (float number in new[] { value.x, value.y, value.z, value.w }) text.Append(',').Append(number.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }
        void LogReceived(string condition, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Assert && type != LogType.Exception) return;
            lock (logGate)
            {
                ++startupErrorCount;
                if (startupErrors.Count < 16) startupErrors.Add(type + ": " + Trim(condition, 1000) + "\n" + Trim(stack, 2000));
            }
        }
        static string Trim(string value, int maximum)
        { return string.IsNullOrEmpty(value) || value.Length <= maximum ? value : value.Substring(0, maximum); }
        static bool ControlsMatch(Material material, VoxelSurfaceSettings profile)
        {
            if (profile == null) return false;
            var expected = profile.Clone(); expected.Sanitize();
            string[] names = { "_VoxelMainShape", "_VoxelIndirectDiffuse", "_VoxelEnvironmentSpecular", "_VoxelDirectSpecular",
                "_VoxelHighlightStrength", "_VoxelHighlightKnee", "_VoxelHighlightCeiling", "_VoxelDiagnostic", "_VoxelVertexColors" };
            float[] values = { expected.mainShape, expected.indirectDiffuse, expected.environmentSpecular, expected.directSpecular,
                expected.highlightStrength, expected.highlightKnee, expected.highlightCeiling, expected.diagnostic, expected.vertexColors ? 1 : 0 };
            for (int i = 0; i < names.Length; ++i)
            {
                if (!material.HasProperty(names[i])) return false;
                float actual = material.GetFloat(names[i]);
                if (float.IsNaN(actual) || float.IsInfinity(actual) || Mathf.Abs(actual - values[i]) > .0001f) return false;
            }
            return true;
        }
        static T Single<T>(IEnumerable<T> items, string label)
        { var all = items.ToArray(); if (all.Length != 1) throw new InvalidOperationException(label + "数量必须为1，实际" + all.Length); return all[0]; }
        static bool ValidAuthorArrays(VoxelSurfaceComparison.Binding binding)
        {
            return binding.original != null && binding.original.Length > 0 && binding.calibrated != null && binding.shaped != null &&
                binding.calibrated.Length == binding.original.Length && binding.shaped.Length == binding.original.Length &&
                binding.original.All(m => m != null && m.shader != null) && binding.calibrated.All(m => m != null && m.shader != null) && binding.shaped.All(m => m != null && m.shader != null);
        }
        static void StartRecorder(string name, out ProfilerRecorder recorder, out string unavailable)
        {
            recorder = default; unavailable = "unavailable: " + name;
            try { recorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, name, 1); if (recorder.Valid) unavailable = ""; }
            catch (Exception error) { unavailable += "; " + error.Message; }
        }
        static long ReadRecorder(ProfilerRecorder recorder, string unavailable, out string status)
        {
            if (!recorder.Valid || recorder.Count == 0) { status = string.IsNullOrEmpty(unavailable) ? "unavailable: no recorder sample" : unavailable; return -1; }
            status = "available: actual ProfilerRecorder sample"; return recorder.LastValue;
        }

        [Serializable] sealed class Family { public string shader; public int slots; }
        [Serializable] sealed class CommerceFile { public string name, sha256; public long length; }
        [Serializable] sealed class Summary
        {
            public bool pass, runtimeSettingsRestored, environmentAuthorsUnchanged, characterProfileAuthorsUnchanged, voxelProfileAuthorsUnchanged, sourceMaterialAuthorsUnchanged, privateCacheBounded;
            public string reason, scene, camera, startingMode, graphicsDevice, graphicsApi, reportDirectory, environmentAuthorsBefore, characterProfileAuthorsBefore, sourceAuthorsBefore, sourceAuthorsAfter;
            public int bindingCount, coldStartActualCameraFrames, totalActualCameraFrames, actualCameraEndEvents, runtimeErrorCount, restoredCacheMaterials, authorCacheLimit;
            public string[] runtimeErrors;
            public bool commerceIsolationReady, originalCommerceUnchanged;
            public string originalCommerceDirectory, isolatedCommerceDirectory, commerceProfileId, commerceActualSavePath, originalCommerceFingerprintBefore, originalCommerceFingerprintAfter;
            public int commerceFilesCopied, commerceExistingFilesPreserved;
            public CommerceFile[] originalCommerceBefore, originalCommerceAfter;
            public bool characterWitnessPassed, characterWitnessProfilesPresent;
            public string characterWitnessScreenshot, characterWitnessReason;
            public string[] characterWitnessProfiles;
            public int characterWitnessActualCameraFrames, characterWitnessCoverageActors, characterWitnessCoverageDraws, characterWitnessOuterDraws;
            public long characterWitnessCoverageBytes, characterWitnessOuterBytes;
            public double elapsedSeconds;
            public List<Phase> phases = new List<Phase>();
        }
        [Serializable] sealed class Phase
        {
            public string name, mode, screenshot, reason, comparisonStatus, comparisonCacheMetric, positiveError, weather, drawCallsStatus, setPassStatus, profilerScope, expectedVoxelProfile;
            public bool pass, materialRoutesValid, positiveActive;
            public int actualCameraFrames, totalActualCameraFrames, actualCameraEndEvents, screenWidth, screenHeight, selectedSourceMaterials,
                loadedMaterialObjects, loadedRenderTextureObjects, comparisonOwnedCacheMaterials, positiveDraws, positiveOwnedMaterials,
                positiveOwnedTextures, positiveOwnedBuffers, sourceBuckets, positivePropertyBlocks, retainedMaterialConflicts,
                worldLookOwnedMaterials, worldLookOwnedTextures, characterCoverageDraws, characterOuterDraws;
            public long positiveBufferBytes, positiveTextureBytes, characterCoverageBytes, characterOuterBytes, drawCalls, setPassCalls;
            public double requestedHours, actualHours, elapsedSeconds;
            public float sunIntensity, ambientIntensity;
            public Vector3 sunDirection;
            public Family[] sourceFamilies;
        }
    }
}
#endif
