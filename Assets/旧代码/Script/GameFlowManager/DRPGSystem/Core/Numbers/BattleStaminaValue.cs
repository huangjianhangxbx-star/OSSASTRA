using System;

public readonly struct BattleStaminaValue : IEquatable<BattleStaminaValue>
{
    public BattleStaminaValue(int capacity, double current, double pending)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        ValidateFiniteNonnegative(current, nameof(current));
        ValidateFiniteNonnegative(pending, nameof(pending));
        if (current + pending > capacity + 1e-9d)
            throw new ArgumentOutOfRangeException(nameof(pending),
                "Current and pending stamina cannot exceed capacity.");
        Capacity = capacity;
        Current = current;
        Pending = pending;
    }

    public int Capacity { get; }
    public double Current { get; }
    public double Pending { get; }
    public int Available => checked((int)Math.Floor(Current));

    public BattleStaminaValue Accrue(double amount)
    {
        ValidateFiniteNonnegative(amount, nameof(amount));
        double room = Math.Max(0d, Capacity - Current - Pending);
        double accepted = Math.Min(amount, room);
        return new BattleStaminaValue(Capacity, Current, Pending + accepted);
    }

    public BattleStaminaValue CreditOwnTurn()
    {
        return Pending <= 0d
            ? this
            : new BattleStaminaValue(Capacity, Current + Pending, 0d);
    }

    public bool TrySpend(int cost, out BattleStaminaValue next)
    {
        if (cost < 0)
            throw new ArgumentOutOfRangeException(nameof(cost));
        if (Available < cost)
        {
            next = this;
            return false;
        }
        next = new BattleStaminaValue(Capacity, Current - cost, Pending);
        return true;
    }

    public bool Equals(BattleStaminaValue other)
    {
        return Capacity == other.Capacity &&
            Current.Equals(other.Current) && Pending.Equals(other.Pending);
    }

    public override bool Equals(object obj) =>
        obj is BattleStaminaValue other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Capacity;
            hash = hash * 397 ^ Current.GetHashCode();
            hash = hash * 397 ^ Pending.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(BattleStaminaValue left,
        BattleStaminaValue right) => left.Equals(right);

    public static bool operator !=(BattleStaminaValue left,
        BattleStaminaValue right) => !left.Equals(right);

    private static void ValidateFiniteNonnegative(double value, string name)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            throw new ArgumentOutOfRangeException(name,
                "Stamina values must be finite and nonnegative.");
    }
}
