using UnityEngine;

public class HuntAudioCombatState : BaseHuntAudioState
{
    public override string StateName => "HuntAudioCombatState";

    public override void Enter()
    {
        Debug.Log("[HuntAudioCombat] Enter");

        eventHub.Hunt.Subscribe<WeaponFiredEvent>(OnWeaponFired);
        eventHub.Hunt.Subscribe<BulletImpactEvent>(OnBulletImpact);
        eventHub.Hunt.Subscribe<WeaponReloadEvent>(OnWeaponReload);

        // audioRoot.PlayMusic(... combat music ...)
    }

    public override void Exit()
    {
        eventHub.Hunt.Unsubscribe<WeaponFiredEvent>(OnWeaponFired);
        eventHub.Hunt.Unsubscribe<BulletImpactEvent>(OnBulletImpact);
        eventHub.Hunt.Unsubscribe<WeaponReloadEvent>(OnWeaponReload);
    }

    private void OnWeaponFired(WeaponFiredEvent e)
    {
        if (e.audioProfile == null)
            return;

        audioRoot.PlaySfx(e.audioProfile.fire, e.position);

        if (e.audioProfile.fireMechanical != null)
            audioRoot.PlaySfx(e.audioProfile.fireMechanical, e.position);
    }

    private void OnBulletImpact(BulletImpactEvent e)
    {
        if (e.audioProfile == null)
            return;

        AudioEventSO cue = SelectImpactCue(e);

        if (cue != null)
            audioRoot.PlaySfx(cue, e.position);
    }

    private void OnWeaponReload(WeaponReloadEvent e)
    {
        if (e.audioProfile == null)
            return;

        AudioEventSO cue = null;

        switch (e.phase)
        {
            case WeaponReloadPhase.Start:
                cue = e.audioProfile.reloadStart;
                break;

            case WeaponReloadPhase.InsertMagazine:
                cue = e.audioProfile.reloadInsert;
                break;

            case WeaponReloadPhase.Charge:
                cue = e.audioProfile.reloadCharge;
                break;

            case WeaponReloadPhase.End:
                cue = e.audioProfile.reloadEnd;
                break;
        }

        if (cue != null)
            audioRoot.PlaySfx(cue, e.position);
    }

    private AudioEventSO SelectImpactCue(BulletImpactEvent e)
    {
        if (e.hitEnemy && e.audioProfile.fleshImpact != null)
            return e.audioProfile.fleshImpact;

        switch (e.surfaceType)
        {
            case SurfaceType.Flesh:
                return e.audioProfile.fleshImpact;

            case SurfaceType.Metal:
                return e.audioProfile.metalImpact;

            case SurfaceType.Concrete:
                return e.audioProfile.concreteImpact;

            case SurfaceType.Wood:
                return e.audioProfile.woodImpact;

            case SurfaceType.Glass:
                return e.audioProfile.glassImpact;

            default:
                return e.audioProfile.defaultImpact;
        }
    }

    public override bool CanTransitionTo(BaseHuntAudioState newState)
    {
        return true;
    }
}