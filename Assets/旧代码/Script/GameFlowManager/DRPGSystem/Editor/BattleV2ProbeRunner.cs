#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class BattleV2ProbeRunner
{
    public static string LastReport { get; private set; } = "Not run";

    public static string Run(string group)
    {
        try
        {
            switch (group)
            {
                case "A1":
                    BattleHudSnapshotProbe.RunAll();
                    break;
                case "A2":
                    BattleFixedSkillBarProbe.RunAll();
                    break;
                case "A3":
                    BattleCardSelectionProbe.RunAll();
                    break;
                case "A4":
                    BattleArrowAndPipProbe.RunAll();
                    break;
                case "A5":
                    BattleStatusProjectionProbe.RunAll();
                    break;
                case "A6":
                    BattleHudIntegrationProbe.RunAll();
                    break;
                case "B1":
                    BattleSkillRoutingProbe.RunAll();
                    break;
                case "B2":
                    BattleInPlaceActionProbe.RunAll();
                    break;
                case "B3":
                    BattleSkillRoutingProbe.RunAll();
                    BattleInPlaceActionProbe.RunAll();
                    break;
                case "C2":
                    BattleDamageMathProbe.RunAll();
                    break;
                case "C3":
                    BattleLayeredHealthProbe.RunAll();
                    break;
                case "C4":
                    BattlePanelIntentProbe.RunAll();
                    break;
                case "C1":
                    BattleStaminaProbe.RunAll();
                    break;
                case "C5":
                    BattleV2AcceptanceProbe.RunAll();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(group), group,
                        "BattleV2 probe group is not registered.");
            }

            LastReport = "PASS BattleV2 " + group;
        }
        catch (Exception exception)
        {
            LastReport = "FAIL BattleV2 " + group + ": " + exception;
            Debug.LogError(LastReport);
        }

        return LastReport;
    }

    [MenuItem("Tools/ANKALUTE/DRPG/Battle V2/Run A1 HUD Snapshot")]
    private static void RunA1FromMenu()
    {
        string report = Run("A1");
        if (report.StartsWith("PASS", StringComparison.Ordinal))
            Debug.Log(report);
    }
}
#endif
