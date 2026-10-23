public sealed class BattleWorldActorBinding
{
    private readonly BattleWorldActorPool pool;

    public BattleWorldActorBinding(BattleWorldActorPool pool)
    {
        this.pool = pool ?? throw new System.ArgumentNullException(nameof(pool));
    }

    public BattleWorldActorView Bind(DRPGBattleParticipantViewModel actor) =>
        pool.Acquire(actor);

    public bool TryGet(int runtimeId, out BattleWorldActorView view) =>
        pool.TryGet(runtimeId, out view);

    public void Release(int runtimeId) => pool.Release(runtimeId);

    public void ReleaseAll() => pool.ReleaseAll();
}
