using UnityEngine;

public sealed class BattleFormationOverlayController : MonoBehaviour
{
    [SerializeField] private BattlefieldEnemyRosterView enemyRoster;
    [SerializeField] private BattlePartyHud partyHud;
    [SerializeField] private KeyCode revealKey = KeyCode.LeftAlt;
    [SerializeField, Min(0.1f)] private float holdThreshold = 0.35f;

    private bool persistent;
    private bool pressed;
    private bool holdActivated;
    private float pressedAt;

    private void Update()
    {
        if (Input.GetKeyDown(revealKey))
        {
            pressed = true;
            holdActivated = false;
            pressedAt = Time.unscaledTime;
        }

        if (pressed && !holdActivated && Input.GetKey(revealKey) &&
            Time.unscaledTime - pressedAt >= holdThreshold)
        {
            holdActivated = true;
            Apply(true);
        }

        if (!pressed || !Input.GetKeyUp(revealKey))
            return;

        pressed = false;
        if (holdActivated)
            Apply(persistent);
        else
        {
            persistent = !persistent;
            Apply(persistent);
        }
    }

    public void Configure(
        BattlefieldEnemyRosterView roster,
        BattlePartyHud hud)
    {
        enemyRoster = roster;
        partyHud = hud;
        Apply(persistent);
    }

    public void ResetState()
    {
        persistent = false;
        pressed = false;
        holdActivated = false;
        Apply(false);
    }

    private void Apply(bool visible)
    {
        enemyRoster?.SetFormationOverlayVisible(visible);
        partyHud?.SetFormationOverlayVisible(visible);
    }

    private void OnDisable()
    {
        pressed = false;
        holdActivated = false;
        Apply(false);
    }
}
