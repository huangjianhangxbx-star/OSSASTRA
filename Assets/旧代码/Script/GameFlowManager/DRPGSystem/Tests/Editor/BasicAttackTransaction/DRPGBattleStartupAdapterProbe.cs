#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;

internal static class DRPGBattleStartupAdapterProbe
{
    // A real adapter must wait for its entry handle AND a rendered frame,
    // rather than publishing startup because a callback returned immediately.
    internal static void EntryWaitsForHandleAndRenderedFrame()
    {
        using (var f = new Fixture())
        {
            var command = new BattleStartEntryCommand(new BattleStartIdentity(901, 1), 0);
            var receipt = f.Port.PresentEntry(command);
            Check(receipt.Owner == command.Owner && !receipt.Completion.IsCompleted && f.Issues == 1, "Entry was not owned and pending.");
            f.Clock.Frame++;
            f.Adapter.Tick();
            Check(!receipt.Completion.IsCompleted, "Frame alone completed entry.");
            f.Handle.TrySettle(BattlePresentationOutcome.Completed);
            f.Adapter.Tick();
            Check(!receipt.Completion.IsCompleted, "Handle completion lacked a subsequently rendered frame.");
            f.Clock.Frame++;
            f.Adapter.Tick();
            Check(receipt.Completion.IsCompleted && receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Completed, "Rendered entry never completed.");
            Check(ReferenceEquals(receipt, f.Port.PresentEntry(command)) && f.Issues == 1, "Duplicate startup replayed entry.");
        }
    }

    internal static void EntryCleanupCannotAcknowledgeAForeignOwner()
    {
        using (var f = new Fixture())
        {
            var command = new BattleStartEntryCommand(new BattleStartIdentity(901, 1), 0);
            var receipt = f.Port.PresentEntry(command);
            var wrong = f.Adapter.EmergencyCleanup(new BattlePresentationCleanupCommand(
                BattleTransactionIdentity.ForBattleStart(new BattleStartIdentity(902, 1)), "foreign"));
            Check(wrong.Completion.IsCompleted && !wrong.Completion.Result && !f.Handle.IsSettled, "Foreign cleanup cancelled entry.");
            var cleanup = f.Adapter.EmergencyCleanup(new BattlePresentationCleanupCommand(command.Owner, "closed"));
            Check(cleanup.Completion.IsCompleted && cleanup.Completion.Result && f.Handle.IsSettled && receipt.Completion.IsCompleted &&
                receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted, "Owned entry cleanup left a live handle or successful receipt.");
            f.Clock.Frame++; f.Adapter.Tick();
            Check(receipt.Completion.Result.Kind == PresentationReceiptTerminalKind.Faulted && f.Issues == 1, "Late frame revived cancelled startup.");
        }
    }

    private sealed class TestClock : IDRPGBattlePresentationClock
    { internal long Frame; public double Now => Frame * .02; public long RenderedFrame => Frame; }
    private sealed class Fixture : IDisposable
    {
        internal readonly TestClock Clock = new TestClock();
        internal readonly BattlePresentationHandle Handle = new BattlePresentationHandle();
        internal readonly DRPGBattleTransactionPresentationAdapter Adapter;
        internal readonly IBattleStartPresentationPort Port;
        internal int Issues;
        private readonly GameObject root;
        internal Fixture()
        {
            Check(typeof(IBattleStartPresentationPort).IsAssignableFrom(typeof(DRPGBattleTransactionPresentationAdapter)), "Production adapter has no startup presentation binding.");
            root = new GameObject("StartupAdapterProbe", typeof(RectTransform), typeof(CanvasGroup), typeof(TextMeshProUGUI));
            var constructor = typeof(DRPGBattleTransactionPresentationAdapter).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            var args = constructor.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue : null).ToArray();
            args[0] = Clock;
            args[1] = new BattleActionAnnouncementPresenter(root.GetComponent<TMP_Text>(), root.GetComponent<CanvasGroup>(), Clock);
            args[2] = new BattleAnimationPlaybackRouter(Clock, (id, command) => null);
            args[3] = new Func<ConflictCompositionCommand, IBattlePresentationHandle>(command => null);
            int entry = Array.FindIndex(constructor.GetParameters(), p => p.Name == "presentEntry");
            Check(entry >= 0, "Production adapter cannot receive the real entry coordinator.");
            args[entry] = new Func<BattleStartEntryCommand, IBattlePresentationHandle>(command => { Issues++; return Handle; });
            Adapter = (DRPGBattleTransactionPresentationAdapter)constructor.Invoke(args);
            Port = (IBattleStartPresentationPort)(object)Adapter;
        }
        public void Dispose() { if (root != null) UnityEngine.Object.DestroyImmediate(root); }
    }
    private static void Check(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
}
#endif
