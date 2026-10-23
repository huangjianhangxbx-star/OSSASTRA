using System;
using System.Collections.Generic;
using UnityEngine;

public enum DRPGBattleQuoteContext
{
    TurnBegin = 0,
    Attack = 1,
    Hit = 2,
    Break = 3,
    Defeated = 4
}

[Serializable]
public sealed class DRPGBattleQuoteLine
{
    [TextArea]
    [SerializeField] private string text;
    [SerializeField] private string voiceCueId;

    public DRPGBattleQuoteLine(string text, string voiceCueId = "")
    {
        this.text = text;
        this.voiceCueId = voiceCueId;
    }

    public string Text => text ?? string.Empty;
    public string VoiceCueId => voiceCueId ?? string.Empty;
}

[CreateAssetMenu(
    fileName = "DRPGBattleQuotes",
    menuName = "ANKALUTE/DRPG/Battle Quote Set")]
public sealed class DRPGBattleQuoteSetSO : ScriptableObject
{
    [SerializeField] private List<DRPGBattleQuoteLine> turnBeginLines =
        new List<DRPGBattleQuoteLine>();
    [SerializeField] private List<DRPGBattleQuoteLine> attackLines =
        new List<DRPGBattleQuoteLine>();
    [SerializeField] private List<DRPGBattleQuoteLine> hitLines =
        new List<DRPGBattleQuoteLine>();
    [SerializeField] private List<DRPGBattleQuoteLine> breakLines =
        new List<DRPGBattleQuoteLine>();
    [SerializeField] private List<DRPGBattleQuoteLine> defeatedLines =
        new List<DRPGBattleQuoteLine>();

    public bool TryGetRandom(
        DRPGBattleQuoteContext context,
        out DRPGBattleQuoteLine line)
    {
        IReadOnlyList<DRPGBattleQuoteLine> lines = ResolveLines(context);
        if (lines == null || lines.Count == 0)
        {
            line = null;
            return false;
        }

        int start = UnityEngine.Random.Range(0, lines.Count);
        for (int offset = 0; offset < lines.Count; offset++)
        {
            DRPGBattleQuoteLine candidate =
                lines[(start + offset) % lines.Count];
            if (candidate != null && !string.IsNullOrWhiteSpace(candidate.Text))
            {
                line = candidate;
                return true;
            }
        }

        line = null;
        return false;
    }

    public bool TryGetRandom(
        DRPGBattleQuoteContext context,
        out string text)
    {
        bool found = TryGetRandom(context, out DRPGBattleQuoteLine line);
        text = found ? line.Text : string.Empty;
        return found;
    }

    private IReadOnlyList<DRPGBattleQuoteLine> ResolveLines(
        DRPGBattleQuoteContext context)
    {
        switch (context)
        {
            case DRPGBattleQuoteContext.TurnBegin:
                return turnBeginLines;
            case DRPGBattleQuoteContext.Attack:
                return attackLines;
            case DRPGBattleQuoteContext.Hit:
                return hitLines;
            case DRPGBattleQuoteContext.Break:
                return breakLines;
            case DRPGBattleQuoteContext.Defeated:
                return defeatedLines;
            default:
                return Array.Empty<DRPGBattleQuoteLine>();
        }
    }
}
