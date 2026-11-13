using System;
using System.Collections.Generic;
using Ossastra.CharacterLighting;
using Ossastra.EnvironmentSimulation;
using Ossastra.EnvironmentSimulation.Rendering;
using UnityEngine;

namespace Ossastra.Characters.Rendering
{
    [Serializable]
    internal sealed class SideViewComparisonLegacyEnvironment
    {
        [SerializeField] private DayCycleProfileSO dayCycle = null;
        [SerializeField] private EnvironmentLightingBinding nativeLighting = null;
        [SerializeField] private CharacterLightingProfileSO blueProfile = null;
        [SerializeField] private CharacterLightingProfileSO freyciaProfile = null;
        [SerializeField] private Shader skyShader = null;
        [SerializeField] private Shader reflectionSkyShader = null;
        [SerializeField] private Shader rainShader = null;
        [SerializeField] private Shader rainDepthCopyShader = null;

        // Three target scene anchors corresponding to old warm A, cool B and warm room light.
        [SerializeField] private Light[] localLights = Array.Empty<Light>();

        internal bool TryApplyCharacterProfiles(Camera camera, EnvironmentRoot root,
            CharacterLightingReceiver blue, CharacterLightingReceiver freycia, out Action restore, out string reason)
        {
            restore = null;
            if (!ValidateCharacterProfiles(camera, root, blue, freycia) || !HasCurrentFrame(camera, root))
            {
                reason = "缺少同相机的角色 Profile 或有效环境快照。";
                return false;
            }
            var originalBlue = blue.Settings.Clone();
            var originalFreycia = freycia.Settings.Clone();
            blue.Settings = blueProfile.settings;
            freycia.Settings = freyciaProfile.settings;
            restore = () =>
            {
                if (blue != null) blue.Settings = originalBlue;
                if (freycia != null) freycia.Settings = originalFreycia;
            };
            reason = string.Empty;
            return true;
        }

        internal bool TryApply(Camera camera, EnvironmentRoot root, EnvironmentSkyBinding sky,
            CharacterLightingReceiver blue, CharacterLightingReceiver freycia, out Action restore, out string reason)
        {
            restore = null;
            if (!TryValidate(camera, root, sky, blue, freycia, out reason)) return false;

            var previousRoot = RootState.Capture(root);
            var previousSky = SkyState.Capture(sky);
            var previousBlue = blue.Settings.Clone();
            var previousFreycia = freycia.Settings.Clone();
            var previousLights = CaptureLights(previousSky.localLights, localLights);
            bool restored = false;

            void Restore()
            {
                if (restored) return;
                restored = true;
                foreach (var pair in previousLights) pair.Value.Apply(pair.Key);
                if (blue != null) blue.Settings = previousBlue;
                if (freycia != null) freycia.Settings = previousFreycia;
                if (sky != null) previousSky.Apply(sky);
                if (root != null)
                {
                    previousRoot.Apply(root);
                    if (root.isActiveAndEnabled) root.RefreshNow();
                }
            }

            try
            {
                var lights = (Light[])localLights.Clone();
                foreach (var light in previousLights.Keys)
                {
                    if (Array.IndexOf(lights, light) < 0) light.enabled = false;
                }
                ConfigurePoint(lights[0], new Color(1f, .82f, .59f), 2f, 7f);
                ConfigurePoint(lights[1], new Color(.72f, .84f, 1f), 2f, 7f);
                ConfigurePoint(lights[2], new Color(1f, .68f, .38f), 2.5f, 5.5f);

                // Apply the old rendering profile to the same world clock and weather sample.
                // The controller's benchmark may set 12:00 and Clear for both recipes.
                new RootState
                {
                    dayCycle = dayCycle,
                    lighting = nativeLighting,
                    effectsEnabled = true,
                    contribution = EnvironmentContribution.Combined,
                    manualSun = false,
                    sunIntensity = 1f,
                    sunTemperature = 0f,
                    shadowStrength = .85f,
                    ambient = 1.99f,
                    sky = 1f,
                    indoor = 1f,
                    readability = .02f,
                    ground = 2f,
                    decoration = 2f,
                    character = 2f,
                    cloudCoverage = -1f,
                    cloudDensity = -1f,
                    cloudShadow = -1f,
                    windYaw = -1f,
                    cloudSpeed = 1f,
                    wind = 1f,
                    fog = 1f,
                    rain = 1f,
                    wetness = 1f
                }.Apply(root);
                new SkyState
                {
                    localLights = lights,
                    skyShader = skyShader,
                    reflectionSkyShader = reflectionSkyShader,
                    rainShader = rainShader,
                    rainDepthCopyShader = rainDepthCopyShader,
                    reflectionIntensity = 1f,
                    reflectionUpdateSeconds = 1f,
                    reflectionChangeThreshold = .002f,
                    overrideSkyColors = false,
                    syncSkyColorTo2D = true,
                    stylizedSkyTintStrength = .5f,
                    sceneEnvironmentEnabled = false
                }.Apply(sky);
                blue.Settings = blueProfile.settings;
                freycia.Settings = freyciaProfile.settings;

                root.RefreshNow();
                if (!HasCurrentFrame(camera, root) || !nativeLighting.HasLease)
                    throw new InvalidOperationException("A 方案的现有环境根或原生灯光租约未发布。" + root.Status);
                if (!sky.TryPrepareSurfaceData(camera, out var surfaceReason))
                    throw new InvalidOperationException(surfaceReason);

                restore = Restore;
                reason = string.Empty;
                return true;
            }
            catch (Exception error)
            {
                reason = error.Message;
                try { Restore(); }
                catch (Exception restoreError) { reason += "；恢复失败：" + restoreError.Message; }
                return false;
            }
        }

        private bool TryValidate(Camera camera, EnvironmentRoot root, EnvironmentSkyBinding sky,
            CharacterLightingReceiver blue, CharacterLightingReceiver freycia, out string reason)
        {
            if (!ValidateCharacterProfiles(camera, root, blue, freycia) ||
                sky == null || !sky.isActiveAndEnabled || sky.environment != root || dayCycle == null)
            {
                reason = "缺少 A 方案的同相机环境、天空或两个独立角色 Profile。";
                return false;
            }

            var scene = camera.gameObject.scene;
            if (nativeLighting == null || !nativeLighting.isActiveAndEnabled || nativeLighting.gameObject.scene != scene ||
                (nativeLighting.root != null && nativeLighting.root != root) ||
                !IsLight(nativeLighting.sun, LightType.Directional, camera) ||
                !IsLight(nativeLighting.fill, LightType.Directional, camera) || nativeLighting.sun == nativeLighting.fill ||
                !Supported(skyShader) || !Supported(reflectionSkyShader) || !Supported(rainShader) || !Supported(rainDepthCopyShader))
            {
                reason = "缺少 A 方案的同场景太阳、补光或旧天空和降雨 Shader。";
                return false;
            }

            if (localLights == null || localLights.Length != 3 || new HashSet<Light>(localLights).Count != 3 ||
                Array.Exists(localLights, light => !IsLight(light, LightType.Point, camera)))
            {
                reason = "需要为 A 方案明确登记三个不同的同场景点光锚点。";
                return false;
            }

            if (!HasCurrentFrame(camera, root))
            {
                reason = "当前相机的同一环境根尚无可用快照。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private bool ValidateCharacterProfiles(Camera camera, EnvironmentRoot root,
            CharacterLightingReceiver blue, CharacterLightingReceiver freycia) =>
            camera != null && root != null && root.isActiveAndEnabled && root.gameplayCamera == camera &&
            blue != null && freycia != null && blue != freycia && blue.gameplayCamera == camera && freycia.gameplayCamera == camera &&
            blueProfile != null && freyciaProfile != null && blueProfile != freyciaProfile &&
            blueProfile.settings != null && freyciaProfile.settings != null;

        private static bool HasCurrentFrame(Camera camera, EnvironmentRoot root) =>
            EnvironmentCameraRegistry.TryGet(camera, out var frame) && frame.Enabled && ReferenceEquals(frame, root.Current);

        private static bool IsLight(Light light, LightType type, Camera camera) =>
            light != null && light.type == type && light.gameObject.activeInHierarchy && light.gameObject.scene == camera.gameObject.scene;

        private static bool Supported(Shader shader) => shader != null && shader.isSupported;

        private static Dictionary<Light, PointState> CaptureLights(Light[] previous, Light[] next)
        {
            var states = new Dictionary<Light, PointState>();
            void Capture(Light[] lights)
            {
                if (lights == null) return;
                foreach (var light in lights)
                    if (light != null && !states.ContainsKey(light)) states.Add(light, PointState.Capture(light));
            }

            Capture(previous);
            Capture(next);
            return states;
        }

        private static void ConfigurePoint(Light light, Color color, float intensity, float range) => new PointState
        {
            enabled = true,
            color = color,
            intensity = intensity,
            range = range,
            shadows = LightShadows.Soft,
            shadowStrength = 1f,
            shadowBias = .05f,
            shadowNormalBias = .4f
        }.Apply(light);

        private struct PointState
        {
            internal bool enabled, useColorTemperature;
            internal Color color;
            internal float intensity, range, shadowStrength, shadowBias, shadowNormalBias;
            internal LightShadows shadows;

            internal static PointState Capture(Light light) => new PointState
            {
                enabled = light.enabled,
                useColorTemperature = light.useColorTemperature,
                color = light.color,
                intensity = light.intensity,
                range = light.range,
                shadows = light.shadows,
                shadowStrength = light.shadowStrength,
                shadowBias = light.shadowBias,
                shadowNormalBias = light.shadowNormalBias
            };

            internal void Apply(Light light)
            {
                if (light == null) return;
                light.useColorTemperature = useColorTemperature;
                light.color = color;
                light.intensity = intensity;
                light.range = range;
                light.shadows = shadows;
                light.shadowStrength = shadowStrength;
                light.shadowBias = shadowBias;
                light.shadowNormalBias = shadowNormalBias;
                light.enabled = enabled;
            }
        }

        private struct RootState
        {
            internal DayCycleProfileSO dayCycle;
            internal EnvironmentLightingBinding lighting;
            internal bool effectsEnabled, manualSun;
            internal EnvironmentContribution contribution;
            internal float sunIntensity, sunTemperature, shadowStrength, ambient, sky, indoor, readability;
            internal float ground, decoration, character, cloudCoverage, cloudDensity, cloudShadow, windYaw;
            internal float cloudSpeed, wind, fog, rain, wetness;

            internal static RootState Capture(EnvironmentRoot value) => new RootState
            {
                dayCycle = value.dayCycle,
                lighting = value.lightingBinding,
                effectsEnabled = value.effectsEnabled,
                contribution = value.contribution,
                manualSun = value.manualSun,
                sunIntensity = value.sunIntensityMultiplier,
                sunTemperature = value.sunTemperatureOffset,
                shadowStrength = value.shadowStrength,
                ambient = value.ambientMultiplier,
                sky = value.skyMultiplier,
                indoor = value.indoorAmbientScale,
                readability = value.characterReadability,
                ground = value.groundLightResponse,
                decoration = value.decorationLightResponse,
                character = value.characterLightResponse,
                cloudCoverage = value.cloudCoverageOverride,
                cloudDensity = value.cloudDensityOverride,
                cloudShadow = value.cloudShadowStrengthOverride,
                windYaw = value.windYawDegreesOverride,
                cloudSpeed = value.cloudSpeedMultiplier,
                wind = value.windMultiplier,
                fog = value.fogMultiplier,
                rain = value.rainMultiplier,
                wetness = value.wetnessMultiplier
            };

            internal void Apply(EnvironmentRoot value)
            {
                value.dayCycle = dayCycle;
                value.lightingBinding = lighting;
                value.effectsEnabled = effectsEnabled;
                value.contribution = contribution;
                value.manualSun = manualSun;
                value.sunIntensityMultiplier = sunIntensity;
                value.sunTemperatureOffset = sunTemperature;
                value.shadowStrength = shadowStrength;
                value.ambientMultiplier = ambient;
                value.skyMultiplier = sky;
                value.indoorAmbientScale = indoor;
                value.characterReadability = readability;
                value.groundLightResponse = ground;
                value.decorationLightResponse = decoration;
                value.characterLightResponse = character;
                value.cloudCoverageOverride = cloudCoverage;
                value.cloudDensityOverride = cloudDensity;
                value.cloudShadowStrengthOverride = cloudShadow;
                value.windYawDegreesOverride = windYaw;
                value.cloudSpeedMultiplier = cloudSpeed;
                value.windMultiplier = wind;
                value.fogMultiplier = fog;
                value.rainMultiplier = rain;
                value.wetnessMultiplier = wetness;
            }
        }

        private struct SkyState
        {
            internal Light[] localLights;
            internal Shader skyShader, reflectionSkyShader, rainShader, rainDepthCopyShader;
            internal float reflectionIntensity, reflectionUpdateSeconds, reflectionChangeThreshold, stylizedSkyTintStrength;
            internal bool overrideSkyColors, syncSkyColorTo2D, sceneEnvironmentEnabled;

            internal static SkyState Capture(EnvironmentSkyBinding value) => new SkyState
            {
                localLights = value.localLights,
                skyShader = value.skyShader,
                reflectionSkyShader = value.reflectionSkyShader,
                rainShader = value.rainShader,
                rainDepthCopyShader = value.rainDepthCopyShader,
                reflectionIntensity = value.reflectionIntensity,
                reflectionUpdateSeconds = value.reflectionUpdateSeconds,
                reflectionChangeThreshold = value.reflectionChangeThreshold,
                stylizedSkyTintStrength = value.stylizedSkyTintStrength,
                overrideSkyColors = value.overrideSkyColors,
                syncSkyColorTo2D = value.syncSkyColorTo2D,
                sceneEnvironmentEnabled = value.sceneEnvironmentEnabled
            };

            internal void Apply(EnvironmentSkyBinding value)
            {
                value.localLights = localLights;
                value.skyShader = skyShader;
                value.reflectionSkyShader = reflectionSkyShader;
                value.rainShader = rainShader;
                value.rainDepthCopyShader = rainDepthCopyShader;
                value.reflectionIntensity = reflectionIntensity;
                value.reflectionUpdateSeconds = reflectionUpdateSeconds;
                value.reflectionChangeThreshold = reflectionChangeThreshold;
                value.stylizedSkyTintStrength = stylizedSkyTintStrength;
                value.overrideSkyColors = overrideSkyColors;
                value.syncSkyColorTo2D = syncSkyColorTo2D;
                value.sceneEnvironmentEnabled = sceneEnvironmentEnabled;
            }
        }
    }
}
