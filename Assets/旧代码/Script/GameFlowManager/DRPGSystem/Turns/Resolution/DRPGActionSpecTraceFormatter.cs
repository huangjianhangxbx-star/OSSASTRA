using System;
using System.Globalization;
using System.Text;

public static class DRPGActionSpecTraceFormatter
{
    public static string Format(
        DRPGResolvedActionSpec spec,
        long actionId = 0)
    {
        if (spec == null)
            return "No resolved action specification is available.";

        var text = new StringBuilder(1024);
        text.Append("Action #")
            .Append(actionId > 0
                ? actionId.ToString(CultureInfo.InvariantCulture)
                : "unassigned")
            .AppendLine();
        text.Append("Command=").Append(spec.CommandKind)
            .Append("; Stage=").Append(spec.Stage)
            .Append("; Actor=").Append(spec.ActorRuntimeId)
            .Append("; Target=").Append(spec.TargetRuntimeId)
            .AppendLine();
        text.Append("Skill=").Append(Show(spec.SkillId))
            .Append("; Execution=").Append(spec.ExecutionMode)
            .Append("; TargetRule=").Append(spec.TargetRule)
            .AppendLine();
        text.Append("Rows=").Append(spec.SourceFormationRows)
            .Append(" -> ").Append(spec.TargetFormationRows)
            .AppendLine();
        text.Append("Costs: MP=").Append(Number(spec.ManaCost))
            .Append("; Posture=").Append(Number(spec.PostureCost))
            .Append("; Prepare=").Append(Number(spec.PrepareDelay))
            .Append("; Recovery=").Append(Number(spec.RecoveryDelay))
            .AppendLine();
        text.Append("Actor multipliers: Damage=")
            .Append(Number(spec.ActorDamageMultiplier))
            .Append("; Healing=")
            .Append(Number(spec.ActorHealingMultiplier))
            .AppendLine();

        text.Append("Effects (").Append(spec.Effects.Count).AppendLine("):");
        for (int i = 0; i < spec.Effects.Count; i++)
        {
            DRPGResolvedActionEffect effect = spec.Effects[i];
            text.Append("  [").Append(i).Append("] ")
                .Append(effect.Kind)
                .Append(" -> ").Append(effect.Target)
                .Append("; Magnitude=").Append(Number(effect.Magnitude))
                .Append("; Tags=").Append(effect.DamageTags);
            if (effect.Status != null)
                text.Append("; Status=").Append(effect.Status.StatusId);
            text.AppendLine();
        }

        text.Append("Contributions (")
            .Append(spec.ContributionTrace.Count)
            .AppendLine("):");
        for (int i = 0; i < spec.ContributionTrace.Count; i++)
        {
            DRPGActionSpecContributionTrace entry =
                spec.ContributionTrace[i];
            text.Append("  [").Append(i).Append("] ")
                .Append(entry.SourceKind)
                .Append(':').Append(Show(entry.SourceId))
                .Append(" #").Append(entry.SourceSequence)
                .Append(entry.WasApplied ? " APPLIED" : " SKIPPED");
            if (!entry.WasApplied &&
                !string.IsNullOrWhiteSpace(entry.SkipReason))
            {
                text.Append(" (").Append(entry.SkipReason).Append(')');
            }
            text.AppendLine();
            text.Append("      ").Append(entry.BeforeSummary)
                .Append(" -> ").Append(entry.AfterSummary)
                .AppendLine();
        }

        if (spec.GuardSpec != null)
        {
            text.Append("Guard: Lead=")
                .Append(Number(spec.GuardSpec.GuardLeadSeconds))
                .Append("; Perfect=")
                .Append(Number(spec.GuardSpec.PerfectLeadSeconds))
                .Append("; DamageMultiplier=")
                .Append(Number(spec.GuardSpec.GuardedDamageMultiplier))
                .AppendLine();
        }

        if (spec.HasFreeStrikeSpec)
        {
            text.Append("FreeStrike: Shape=")
                .Append(spec.FreeStrikeSpec.Shape)
                .Append("; Duration=")
                .Append(Number(spec.FreeStrikeSpec.Duration))
                .Append("; Cadence=")
                .Append(Number(spec.FreeStrikeSpec.Cadence))
                .AppendLine();
        }

        return text.ToString().TrimEnd();
    }

    private static string Number(float value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Show(string value) =>
        string.IsNullOrWhiteSpace(value) ? "<none>" : value;
}
