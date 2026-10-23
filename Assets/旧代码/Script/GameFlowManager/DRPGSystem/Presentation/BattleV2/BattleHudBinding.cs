using System;
using UnityEngine;

/// <summary>
/// Read-only adapter between an active session and the BattleV2 HUD. It never
/// advances the session or applies card effects.
/// </summary>
public sealed class BattleHudBinding : MonoBehaviour
{
    [SerializeField] private DRPGBattleUGUISurface surface;
    [SerializeField] private bool previewEnabled;
    private DRPGBattleSession session;
    private int lastActorId;

    public bool PreviewEnabled => previewEnabled;

    public void Bind(DRPGBattleSession value)
    {
        session = value ?? throw new ArgumentNullException(nameof(value));
        lastActorId = 0;
        Refresh();
    }

    public void Unbind()
    {
        session = null;
        lastActorId = 0;
        if (surface != null && surface.BattleV2PipRow != null)
            surface.BattleV2PipRow.Bind(0, 0, 0);
    }

    public void SetPreviewEnabled(bool enabled)
    {
        previewEnabled = enabled;
        lastActorId = 0;
        Refresh();
    }

    public void RefreshCurrent() => Refresh();

    private void LateUpdate()
    {
        if (!previewEnabled || session == null || session.CurrentActor == null)
            return;
        if (session.CurrentActor.RuntimeId == lastActorId)
            return;
        Refresh();
    }

    private void Refresh()
    {
        if (surface == null)
            surface = GetComponentInChildren<DRPGBattleUGUISurface>(true);
        if (surface == null || surface.BattleV2PipRow == null)
            return;
        if (session == null || session.CurrentActor == null ||
            session.CurrentActor.Side != DRPGBattleSide.Player)
        {
            surface.BattleV2PipRow.Bind(0, 0, 0);
            return;
        }
        lastActorId = session.CurrentActor.RuntimeId;
        surface.BattleV2PipRow.Bind(
            session.CurrentActor.BattleStamina.Available,
            session.CurrentActor.BattleStamina.Capacity,
            0);
    }
}
