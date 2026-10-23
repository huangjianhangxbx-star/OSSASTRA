using System;

public readonly struct BattlePresentationScope :
    IEquatable<BattlePresentationScope>
{
    public BattlePresentationScope(
        long battleGeneration,
        long turnSerial,
        long actionId)
    {
        BattleGeneration = battleGeneration;
        TurnSerial = turnSerial;
        ActionId = actionId;
    }

    public long BattleGeneration { get; }
    public long TurnSerial { get; }
    public long ActionId { get; }

    public bool Equals(BattlePresentationScope other)
    {
        return BattleGeneration == other.BattleGeneration &&
            TurnSerial == other.TurnSerial &&
            ActionId == other.ActionId;
    }

    public override bool Equals(object obj)
    {
        return obj is BattlePresentationScope other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = BattleGeneration.GetHashCode();
            hash = (hash * 397) ^ TurnSerial.GetHashCode();
            hash = (hash * 397) ^ ActionId.GetHashCode();
            return hash;
        }
    }

    public override string ToString()
    {
        return $"Battle {BattleGeneration} / Turn {TurnSerial} / Action {ActionId}";
    }

    public static bool operator ==(
        BattlePresentationScope left,
        BattlePresentationScope right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(
        BattlePresentationScope left,
        BattlePresentationScope right)
    {
        return !left.Equals(right);
    }
}
