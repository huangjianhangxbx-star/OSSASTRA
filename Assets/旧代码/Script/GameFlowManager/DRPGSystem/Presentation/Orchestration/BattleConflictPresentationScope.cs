using System;

public sealed class BattleConflictPresentationScope : IDisposable
{
    private readonly IBattleHudPresenter hud;
    private readonly IBattleCameraPresenter camera;
    private readonly IBattleStagePresenter stage;
    private BattleConflictStateSnapshot snapshot;
    private bool entered;
    private bool restored;

    public BattleConflictPresentationScope(
        IBattleHudPresenter hud,
        IBattleCameraPresenter camera,
        IBattleStagePresenter stage)
    {
        this.hud = hud ?? throw new ArgumentNullException(nameof(hud));
        this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
        this.stage = stage ?? throw new ArgumentNullException(nameof(stage));
    }

    public static int ActiveScopeCount { get; private set; }
    public bool IsEntered => entered;
    public bool IsRestored => restored;
    public BattleConflictStateSnapshot Snapshot => snapshot;

    public void Enter(
        BattleHudVisibilityPolicy hudPolicy,
        BattleStageProfileSO temporaryStage,
        BattleCameraShotDefinition cameraShot)
    {
        Begin();

        try
        {
            hud.Apply(hudPolicy);
            if (temporaryStage != null)
                stage.SwitchTemporary(temporaryStage);
            if (cameraShot != null)
                camera.Play(cameraShot);
        }
        catch
        {
            Restore();
            throw;
        }
    }

    public void Begin()
    {
        if (entered)
            return;

        snapshot = new BattleConflictStateSnapshot(
            hud.Capture(),
            camera.Capture(),
            stage.Capture());
        entered = true;
        restored = false;
        ActiveScopeCount++;
    }

    public void Restore()
    {
        if (!entered || restored)
            return;

        restored = true;
        try
        {
            camera.Restore(snapshot.Camera);
        }
        finally
        {
            try
            {
                stage.Restore(snapshot.Stage);
            }
            finally
            {
                hud.Restore(snapshot.Hud);
                ActiveScopeCount = Math.Max(0, ActiveScopeCount - 1);
            }
        }
    }

    public void Dispose() => Restore();
}
