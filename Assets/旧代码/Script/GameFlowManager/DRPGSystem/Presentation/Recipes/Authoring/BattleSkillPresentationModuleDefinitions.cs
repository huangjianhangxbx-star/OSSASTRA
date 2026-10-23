using System;
using UnityEngine;

[Serializable]
public sealed class BattleHudPresentationDefinition
{
    [SerializeField] private BattleHudVisibilityPolicy visibility =
        BattleHudVisibilityPolicy.HideAll;

    public BattleHudVisibilityPolicy Visibility => visibility;
    public BattleHudPresentationDefinition Snapshot() =>
        new BattleHudPresentationDefinition { visibility = visibility };
}

[Serializable]
public sealed class BattleStagePresentationDefinition
{
    [SerializeField] private BattleStagePresentationPolicy policy =
        BattleStagePresentationPolicy.KeepCurrent;
    [SerializeField] private string stageProfileKey = string.Empty;

    public BattleStagePresentationPolicy Policy => policy;
    public string StageProfileKey => stageProfileKey ?? string.Empty;
    public BattleStagePresentationDefinition Snapshot() =>
        new BattleStagePresentationDefinition
        {
            policy = policy,
            stageProfileKey = stageProfileKey
        };
}

[Serializable]
public enum BattleCameraFramingMode
{
    StaticShot = 0,
    FrameActorAndTargets = 1
}

[Serializable]
public sealed class BattleCameraPresentationDefinition
{
    [SerializeField] private string cameraRigKey = string.Empty;
    [Min(1f)]
    [SerializeField] private float fieldOfView = 38f;
    [Min(0f)]
    [SerializeField] private float transitionSeconds = 0.16f;
    [SerializeField] private BattleCameraFramingMode framingMode =
        BattleCameraFramingMode.StaticShot;
    [Min(0f)]
    [SerializeField] private float horizontalPadding = 0.6f;
    [Min(0f)]
    [SerializeField] private float verticalPadding = 0.8f;

    public string CameraRigKey => cameraRigKey ?? string.Empty;
    public float FieldOfView => fieldOfView;
    public float TransitionSeconds => transitionSeconds;
    public BattleCameraFramingMode FramingMode => framingMode;
    public float HorizontalPadding => horizontalPadding;
    public float VerticalPadding => verticalPadding;
    public BattleCameraPresentationDefinition Snapshot() =>
        new BattleCameraPresentationDefinition
        {
            cameraRigKey = cameraRigKey,
            fieldOfView = fieldOfView,
            transitionSeconds = transitionSeconds,
            framingMode = framingMode,
            horizontalPadding = horizontalPadding,
            verticalPadding = verticalPadding
        };
}

[Serializable]
public sealed class BattleActorPresentationDefinition
{
    [SerializeField] private bool visible = true;
    [SerializeField] private string poseKey = string.Empty;
    [Min(0f)]
    [SerializeField] private float scale = 1f;

    public bool Visible => visible;
    public string PoseKey => poseKey ?? string.Empty;
    public float Scale => scale;
    public BattleActorPresentationDefinition Snapshot() =>
        new BattleActorPresentationDefinition
        {
            visible = visible,
            poseKey = poseKey,
            scale = scale
        };
}

[Serializable]
public sealed class BattleTargetPresentationDefinition
{
    [SerializeField] private bool focus = true;
    [Min(0f)]
    [SerializeField] private float scale = 1.08f;
    [SerializeField] private float retreatDistance = 0.35f;
    [SerializeField] private BattleOtherTargetPresentationPolicy others =
        BattleOtherTargetPresentationPolicy.Dim;

    public bool Focus => focus;
    public float Scale => scale;
    public float RetreatDistance => retreatDistance;
    public BattleOtherTargetPresentationPolicy Others => others;
    public BattleTargetPresentationDefinition Snapshot() =>
        new BattleTargetPresentationDefinition
        {
            focus = focus,
            scale = scale,
            retreatDistance = retreatDistance,
            others = others
        };
}

[Serializable]
public sealed class BattleBackgroundPresentationDefinition
{
    [SerializeField] private BattleBackgroundPresentationPolicy policy =
        BattleBackgroundPresentationPolicy.Dim;
    [SerializeField] private string replacementKey = string.Empty;
    [Range(0f, 1f)]
    [SerializeField] private float intensity = 0.5f;

    public BattleBackgroundPresentationPolicy Policy => policy;
    public string ReplacementKey => replacementKey ?? string.Empty;
    public float Intensity => intensity;
    public BattleBackgroundPresentationDefinition Snapshot() =>
        new BattleBackgroundPresentationDefinition
        {
            policy = policy,
            replacementKey = replacementKey,
            intensity = intensity
        };
}

[Serializable]
public sealed class BattleSpinePresentationDefinition
{
    [SerializeField] private string animationKey = string.Empty;
    [SerializeField] private string windupMarker = "WindupComplete";
    [SerializeField] private string impactMarker = "Impact";
    [SerializeField] private string hitStopMarker = "HitStop";
    [SerializeField] private string endMarker = "PresentationEnd";

    public string AnimationKey => animationKey ?? string.Empty;
    public string WindupMarker => windupMarker ?? string.Empty;
    public string ImpactMarker => impactMarker ?? string.Empty;
    public string HitStopMarker => hitStopMarker ?? string.Empty;
    public string EndMarker => endMarker ?? string.Empty;
    public BattleSpinePresentationDefinition Snapshot() =>
        new BattleSpinePresentationDefinition
        {
            animationKey = animationKey,
            windupMarker = windupMarker,
            impactMarker = impactMarker,
            hitStopMarker = hitStopMarker,
            endMarker = endMarker
        };
}

[Serializable]
public sealed class BattleFeedbackPresentationDefinition
{
    [SerializeField] private string impactMarker = "Impact";
    [SerializeField] private string vfxKey = string.Empty;
    [SerializeField] private string audioCueKey = string.Empty;
    [Min(0f)]
    [SerializeField] private float screenShakeStrength = 0.35f;
    [SerializeField] private bool flashTarget = true;
    [SerializeField] private bool showDamageNumbers = true;
    [Min(0f)]
    [SerializeField] private float holdSeconds = 0.4f;
    [Min(0f)]
    [SerializeField] private float targetRecoilDistance = 0.16f;

    public string ImpactMarker => impactMarker ?? string.Empty;
    public string VfxKey => vfxKey ?? string.Empty;
    public string AudioCueKey => audioCueKey ?? string.Empty;
    public float ScreenShakeStrength => screenShakeStrength;
    public bool FlashTarget => flashTarget;
    public bool ShowDamageNumbers => showDamageNumbers;
    public float HoldSeconds => holdSeconds;
    public float TargetRecoilDistance => targetRecoilDistance;
    public BattleFeedbackPresentationDefinition Snapshot() =>
        new BattleFeedbackPresentationDefinition
        {
            impactMarker = impactMarker,
            vfxKey = vfxKey,
            audioCueKey = audioCueKey,
            screenShakeStrength = screenShakeStrength,
            flashTarget = flashTarget,
            showDamageNumbers = showDamageNumbers,
            holdSeconds = holdSeconds,
            targetRecoilDistance = targetRecoilDistance
        };
}

[Serializable]
public sealed class BattleExitPresentationDefinition
{
    [SerializeField] private BattlePresentationExitPolicy policy =
        BattlePresentationExitPolicy.Fade;
    [Min(0f)]
    [SerializeField] private float restoreSeconds = 0.14f;
    [Min(0.1f)]
    [SerializeField] private float watchdogSeconds = 4f;

    public BattlePresentationExitPolicy Policy => policy;
    public float RestoreSeconds => restoreSeconds;
    public float WatchdogSeconds => watchdogSeconds;
    public BattleExitPresentationDefinition Snapshot() =>
        new BattleExitPresentationDefinition
        {
            policy = policy,
            restoreSeconds = restoreSeconds,
            watchdogSeconds = watchdogSeconds
        };
}

[Serializable]
public sealed class BattleHudPresentationModule
{
    [SerializeField] private BattlePresentationOverrideMode mode;
    [SerializeField] private BattleHudPresentationDefinition definition = new BattleHudPresentationDefinition();
    public BattlePresentationOverrideMode Mode => mode;
    public BattleHudPresentationDefinition Definition => definition;
}

[Serializable]
public sealed class BattleStagePresentationModule
{
    [SerializeField] private BattlePresentationOverrideMode mode;
    [SerializeField] private BattleStagePresentationDefinition definition = new BattleStagePresentationDefinition();
    public BattlePresentationOverrideMode Mode => mode;
    public BattleStagePresentationDefinition Definition => definition;
}

[Serializable]
public sealed class BattleCameraPresentationModule
{
    [SerializeField] private BattlePresentationOverrideMode mode;
    [SerializeField] private BattleCameraPresentationDefinition definition = new BattleCameraPresentationDefinition();
    public BattlePresentationOverrideMode Mode => mode;
    public BattleCameraPresentationDefinition Definition => definition;
}

[Serializable]
public sealed class BattleActorPresentationModule
{
    [SerializeField] private BattlePresentationOverrideMode mode;
    [SerializeField] private BattleActorPresentationDefinition definition = new BattleActorPresentationDefinition();
    public BattlePresentationOverrideMode Mode => mode;
    public BattleActorPresentationDefinition Definition => definition;
}

[Serializable]
public sealed class BattleTargetPresentationModule
{
    [SerializeField] private BattlePresentationOverrideMode mode;
    [SerializeField] private BattleTargetPresentationDefinition definition = new BattleTargetPresentationDefinition();
    public BattlePresentationOverrideMode Mode => mode;
    public BattleTargetPresentationDefinition Definition => definition;
}

[Serializable]
public sealed class BattleBackgroundPresentationModule
{
    [SerializeField] private BattlePresentationOverrideMode mode;
    [SerializeField] private BattleBackgroundPresentationDefinition definition = new BattleBackgroundPresentationDefinition();
    public BattlePresentationOverrideMode Mode => mode;
    public BattleBackgroundPresentationDefinition Definition => definition;
}

[Serializable]
public sealed class BattleSpinePresentationModule
{
    [SerializeField] private BattlePresentationOverrideMode mode;
    [SerializeField] private BattleSpinePresentationDefinition definition = new BattleSpinePresentationDefinition();
    public BattlePresentationOverrideMode Mode => mode;
    public BattleSpinePresentationDefinition Definition => definition;
}

[Serializable]
public sealed class BattleFeedbackPresentationModule
{
    [SerializeField] private BattlePresentationOverrideMode mode;
    [SerializeField] private BattleFeedbackPresentationDefinition definition = new BattleFeedbackPresentationDefinition();
    public BattlePresentationOverrideMode Mode => mode;
    public BattleFeedbackPresentationDefinition Definition => definition;
}

[Serializable]
public sealed class BattleExitPresentationModule
{
    [SerializeField] private BattlePresentationOverrideMode mode;
    [SerializeField] private BattleExitPresentationDefinition definition = new BattleExitPresentationDefinition();
    public BattlePresentationOverrideMode Mode => mode;
    public BattleExitPresentationDefinition Definition => definition;
}
