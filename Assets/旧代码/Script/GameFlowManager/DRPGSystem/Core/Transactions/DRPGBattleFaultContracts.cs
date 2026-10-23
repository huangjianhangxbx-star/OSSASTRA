using System;
using System.Collections.Generic;

internal enum DRPGBattleFaultKind { RootTransaction, Startup, TurnEntry, WaveTransition }

// A technical terminal, not a combat outcome. The host may return Aborted,
// but the domain never manufactures BattleEnded or successful action facts.
internal readonly struct DRPGBattleFaultTerminal
{
    internal DRPGBattleFaultTerminal(BattleTransactionIdentity owner, long revision,
        string diagnosticCode, string detail, IReadOnlyList<ActionTraceEntry> trace)
    {
        if (!owner.IsValid || revision < 0 || string.IsNullOrWhiteSpace(diagnosticCode))
            throw new ArgumentException("Fault terminal requires identity, revision and diagnostic code.");
        Owner = owner; LastValidBattleRevision = revision;
        DiagnosticCode = diagnosticCode; Detail = detail ?? string.Empty;
        Trace = Array.AsReadOnly(new List<ActionTraceEntry>(trace ?? Array.Empty<ActionTraceEntry>()).ToArray());
    }
    internal BattleTransactionIdentity Owner { get; }
    internal DRPGBattleFaultKind Kind => Owner.Kind == BattleTransactionKind.BattleStart
        ? DRPGBattleFaultKind.Startup : Owner.Kind == BattleTransactionKind.TurnEntry
            ? DRPGBattleFaultKind.TurnEntry : Owner.Kind == BattleTransactionKind.WaveTransition
                ? DRPGBattleFaultKind.WaveTransition : DRPGBattleFaultKind.RootTransaction;
    internal long LastValidBattleRevision { get; }
    internal string DiagnosticCode { get; }
    internal string Detail { get; }
    internal IReadOnlyList<ActionTraceEntry> Trace { get; }
}
