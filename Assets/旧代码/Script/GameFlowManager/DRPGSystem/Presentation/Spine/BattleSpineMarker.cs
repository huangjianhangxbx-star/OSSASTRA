using System;

public static class BattleSpineMarkerNames
{
    public const string WindupComplete = "WindupComplete";
    public const string Impact = "Impact";
    public const string HitStop = "HitStop";
    public const string PresentationEnd = "PresentationEnd";
}

public readonly struct BattleSpineMarker : IEquatable<BattleSpineMarker>
{
    public BattleSpineMarker(string name, int index = 0)
    {
        Name = name ?? string.Empty;
        Index = index;
    }

    public string Name { get; }
    public int Index { get; }

    public bool Equals(BattleSpineMarker other)
    {
        return Index == other.Index &&
            string.Equals(Name, other.Name, StringComparison.Ordinal);
    }

    public override bool Equals(object obj)
    {
        return obj is BattleSpineMarker other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((Name != null ? StringComparer.Ordinal.GetHashCode(Name) : 0) * 397) ^ Index;
        }
    }

    public override string ToString()
    {
        return string.IsNullOrEmpty(Name) ? "<unnamed>" : Name + ":" + Index;
    }
}
