using System;
using Ossastra.EnvironmentSimulation;
using Ossastra.WorldRendering;
using Ossastra.WorldRendering.Integration;
using UnityEngine;
using XingHai.T7B;
using XingHai.T7C1;
using XingHai.T7C2;

namespace Ossastra.Characters.Rendering
{
    [Serializable]
    internal sealed class SideViewComparisonLegacyWorldLook
    {
        // Use the four original profile assets read from SC_RoomVisibilityLab.
        [SerializeField] private HD2DPostProfile outdoorPresentation = null;
        [SerializeField] private OutdoorAtmosphereProfile outdoorAtmosphere = null;
        [SerializeField] private RegionalLookProfile outdoorPalette = null;
        [SerializeField] private HD2DPostProfile indoorPresentation = null;

        private readonly WorldLookEffectOptions effects = new WorldLookEffectOptions
        {
            enabled = true,
            airEnabled = true,
            focusEnabled = true,
            bloomEnabled = true,
            gradeEnabled = true,
            vignetteEnabled = true
        };

        internal bool TryBuild(Camera camera, Vector3 observerPosition, bool exterior,
            out WorldLookSettings settings, out string reason)
        {
            settings = null;
            if (camera == null || outdoorPresentation == null || outdoorAtmosphere == null ||
                outdoorPalette == null || indoorPresentation == null)
            {
                reason = "缺少 A 方案相机或旧主场展示、空气、色板 Profile。";
                return false;
            }

            if (!EnvironmentCameraRegistry.TryGet(camera, out var environment) || !environment.Enabled)
            {
                reason = "A 方案尚无该相机的有效环境快照。";
                return false;
            }

            var built = LegacyWorldLookProfileAdapter.BuildLegacy(camera, observerPosition,
                exterior ? outdoorPresentation : indoorPresentation,
                exterior ? outdoorAtmosphere : null, exterior ? outdoorPalette : null,
                effects, environment, allowWeatherFog: exterior);
            if (!built.Enabled || !built.FocusEnabled || !built.BloomEnabled || !built.VignetteEnabled ||
                built.AirEnabled != exterior || built.GradeEnabled != exterior)
            {
                reason = "A 方案观察点或旧主场合成开关未通过校验。";
                return false;
            }

            settings = built;
            reason = string.Empty;
            return true;
        }

        internal bool TryPublish(object owner, Camera camera, Vector3 observerPosition, bool exterior, out string reason)
        {
            if (owner == null || (owner is UnityEngine.Object unityOwner && unityOwner == null))
            {
                reason = "缺少 A 方案的相机登记所有者。";
                return false;
            }

            if (!TryBuild(camera, observerPosition, exterior, out var settings, out reason)) return false;
            try
            {
                WorldLookCameraRegistry.Publish(owner, camera, settings);
                return true;
            }
            catch (InvalidOperationException error)
            {
                reason = error.Message;
                return false;
            }
        }

        internal void Release(object owner, Camera camera) => WorldLookCameraRegistry.Release(owner, camera);
    }
}
