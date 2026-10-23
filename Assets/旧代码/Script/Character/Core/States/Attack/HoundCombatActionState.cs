using UnityEngine;

public abstract class HoundCombatActionState
{
    protected HoundCombatActionStateMachine stateMachine;
    protected CharacterDataBus dataBus;
    protected Transform transform;
    protected Rigidbody2D rigidbody;
    protected CharacterRoot characterRoot;
    private bool hasCommittedControl;

    public abstract string StateName { get; }

    public virtual void Initialize(
        HoundCombatActionStateMachine owner,
        CharacterDataBus bus,
        Transform tf,
        Rigidbody2D rb,
        CharacterRoot root)
    {
        stateMachine = owner;
        dataBus = bus;
        transform = tf;
        rigidbody = rb;
        characterRoot = root;
    }

    public virtual void Enter()
    {
    }

    public virtual void Exit()
    {
        ReleaseCommittedControl();
    }

    public virtual void Update(float deltaTime)
    {
    }

    public virtual void FixedUpdate(float fixedDeltaTime)
    {
    }

    public virtual bool CanTransitionTo(HoundCombatActionState next)
    {
        return true;
    }

    public virtual void OnSpineSignal(CharacterSpineAnimationSignal signal)
    {
    }

    protected void AcquireCommittedControl()
    {
        if (characterRoot == null || characterRoot.controlAuthority == null)
            return;

        characterRoot.controlAuthority.SetOverride(
            this,
            CharacterControlOverride.CommittedAttack());
        hasCommittedControl = true;
    }

    protected void ReleaseCommittedControl()
    {
        if (!hasCommittedControl)
            return;

        characterRoot?.controlAuthority?.ReleaseOverride(this);
        hasCommittedControl = false;
    }

    internal void ForceReleaseControl()
    {
        ReleaseCommittedControl();
    }



}
