using DG.Tweening;
using UnityEngine;

public interface IBattleCameraPresenter
{
    BattleCameraStateSnapshot Capture();
    void Play(BattleCameraShotDefinition shot, bool immediate = false);
    void Restore(BattleCameraStateSnapshot snapshot);
}

public sealed class BattleCameraPresenter : MonoBehaviour, IBattleCameraPresenter
{
    [SerializeField] private Camera battleCamera;
    [SerializeField] private Transform shotOrigin;

    private Sequence transition;
    private bool preserveSceneFraming;

    public bool IsTransitioning => transition != null &&
        transition.IsActive() &&
        !transition.IsComplete();
    public Camera BattleCamera => battleCamera;
    internal Transform ShotOrigin => shotOrigin;
    public string CurrentShotId { get; private set; } = string.Empty;
    public string LastPlayedShotId { get; private set; } = string.Empty;
    public int PlayCount { get; private set; }
    public int RestoreCount { get; private set; }
    public float Aspect
    {
        get
        {
            EnsureCamera();
            return battleCamera.aspect;
        }
    }

    public void Configure(Camera camera, Transform origin, bool preserveSceneFraming = false)
    {
        CancelTransition();
        battleCamera = camera != null ? camera : GetComponent<Camera>();
        shotOrigin = origin;
        this.preserveSceneFraming = preserveSceneFraming;
    }

    public BattleCameraStateSnapshot Capture()
    {
        EnsureCamera();
        return new BattleCameraStateSnapshot(
            battleCamera.transform.position,
            battleCamera.transform.rotation,
            battleCamera.fieldOfView,
            battleCamera.orthographic,
            battleCamera.orthographicSize,
            battleCamera.enabled);
    }

    public void Play(BattleCameraShotDefinition shot, bool immediate = false)
    {
        if (shot == null)
            return;

        EnsureCamera();
        CancelTransition();
        // Legacy recipes describe an independent stage, not the current level.
        // The standard transaction owns relative conflict framing on a borrowed
        // scene camera; legacy absolute shots must not switch its projection or
        // teleport it toward actor feet. Configure resets this policy on release.
        if (preserveSceneFraming)
            return;
        battleCamera.orthographic = false;
        CurrentShotId = shot.ShotId;
        LastPlayedShotId = shot.ShotId;
        PlayCount++;

        Vector3 targetPosition = shotOrigin != null
            ? shotOrigin.TransformPoint(shot.LocalPosition)
            : shot.LocalPosition;
        Quaternion targetRotation = shotOrigin != null
            ? shotOrigin.rotation * Quaternion.Euler(shot.LocalEulerAngles)
            : Quaternion.Euler(shot.LocalEulerAngles);
        float duration = immediate ? 0f : shot.DurationSeconds;
        if (duration <= 0f)
        {
            battleCamera.transform.SetPositionAndRotation(
                targetPosition,
                targetRotation);
            battleCamera.fieldOfView = shot.FieldOfView;
            return;
        }

        transition = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .Join(battleCamera.transform.DOMove(targetPosition, duration)
                .SetEase(Ease.OutCubic))
            .Join(battleCamera.transform.DORotateQuaternion(
                    targetRotation,
                    duration)
                .SetEase(Ease.OutCubic))
            .Join(battleCamera.DOFieldOfView(shot.FieldOfView, duration)
                .SetEase(Ease.OutCubic));
    }

    public void Restore(BattleCameraStateSnapshot snapshot)
    {
        EnsureCamera();
        CancelTransition();
        battleCamera.transform.SetPositionAndRotation(
            snapshot.Position,
            snapshot.Rotation);
        battleCamera.fieldOfView = snapshot.FieldOfView;
        battleCamera.orthographic = snapshot.Orthographic;
        battleCamera.orthographicSize = snapshot.OrthographicSize;
        battleCamera.enabled = snapshot.Enabled;
        CurrentShotId = string.Empty;
        RestoreCount++;
    }

    public Vector3 ToShotLocal(Vector3 worldPosition)
    {
        return shotOrigin != null
            ? shotOrigin.InverseTransformPoint(worldPosition)
            : worldPosition;
    }

    private void OnDisable() => CancelTransition();

    private void EnsureCamera()
    {
        if (battleCamera == null)
            battleCamera = GetComponent<Camera>();
        if (battleCamera == null)
            battleCamera = gameObject.AddComponent<Camera>();
    }

    private void CancelTransition()
    {
        if (transition != null && transition.IsActive())
            transition.Kill(false);
        transition = null;
    }
}
