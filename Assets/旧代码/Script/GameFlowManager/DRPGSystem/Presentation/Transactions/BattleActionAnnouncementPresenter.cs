using System;
using TMPro;
using UnityEngine;

// Only owns the action label. No actor, camera, VFX or rule references.
internal sealed class BattleActionAnnouncementPresenter
{
    private readonly TMP_Text text;
    private readonly CanvasGroup group;
    private readonly IDRPGBattlePresentationClock clock;
    private readonly bool holdActionLabel;
    private bool keepVisible;
    private PresentationReceipt active;
    private double startedAt, duration;
    private long startedFrame;
    internal BattleActionAnnouncementPresenter(TMP_Text text, CanvasGroup group,
        IDRPGBattlePresentationClock clock, bool holdActionLabel = false)
    {
        if (text == null || group == null || clock == null) throw new ArgumentNullException();
        this.text = text; this.group = group; this.clock = clock; this.holdActionLabel = holdActionLabel;
    }
    internal PresentationReceipt Present(in AnnouncementCommand command)
    {
        var result = new PresentationReceipt(command.Identity, DRPGBattleActionStage.Announcement);
        string actionName = command.Snapshot.Semantics.ActionName;
        if (string.IsNullOrWhiteSpace(actionName))
            actionName = "技能";
        return Present(result, command.ActorName + " · " + actionName,
            Math.Max(0d, command.Snapshot.Timing.AnnouncementSeconds), holdActionLabel);
    }
    internal PresentationReceipt Present(in WaveAnnouncementCommand command)
    {
        if (!command.Identity.IsValid || double.IsNaN(command.Duration) || double.IsInfinity(command.Duration) || command.Duration <= 0)
            throw new ArgumentException("Wave announcement requires valid identity and duration.");
        return Present(new PresentationReceipt(BattleTransactionIdentity.ForWave(command.Identity), DRPGBattleActionStage.Announcement),
            "第 " + command.Identity.WaveId + " 波敌人", command.Duration);
    }
    private PresentationReceipt Present(PresentationReceipt result, string label, double seconds, bool hold = false)
    {
        if (active != null && !active.Completion.IsCompleted)
        {
            result.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Faulted, "Announcement already owned."));
            return result;
        }
        active = result; startedAt = clock.Now; startedFrame = clock.RenderedFrame;
        duration = seconds; keepVisible = hold;
        text.text = label;
        text.raycastTarget = false;
        group.alpha = 1f; group.interactable = false; group.blocksRaycasts = false;
        return result;
    }
    internal void Tick()
    {
        if (active == null || active.Completion.IsCompleted || clock.Now - startedAt < duration || clock.RenderedFrame - startedFrame < 2) return;
        if (text == null || group == null)
        { active.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Degraded, "Announcement view lost.")); return; }
        if (!keepVisible) group.alpha = 0f;
        active.TryComplete(new PresentationReceiptTerminal(PresentationReceiptTerminalKind.Completed, "Announcement rendered."));
    }
    internal void Hide() { if (group != null) group.alpha = 0f; }
    internal void Cancel(PresentationReceipt receipt)
    {
        if (!ReferenceEquals(active, receipt)) return;
        if (group != null) group.alpha = 0f;
        active = null;
    }
}
