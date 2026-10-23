using System;

public sealed class BattleInputGate
{
    private int leaseCount;

    public event Action<bool> Changed;

    public bool IsBlocked => leaseCount > 0;
    public int LeaseCount => leaseCount;

    public IDisposable Acquire(string owner)
    {
        bool wasBlocked = IsBlocked;
        leaseCount++;
        if (!wasBlocked)
            Changed?.Invoke(true);
        return new Lease(this);
    }

    private void Release()
    {
        if (leaseCount <= 0)
            return;

        leaseCount--;
        if (leaseCount == 0)
            Changed?.Invoke(false);
    }

    private sealed class Lease : IDisposable
    {
        private BattleInputGate owner;

        public Lease(BattleInputGate owner)
        {
            this.owner = owner;
        }

        public void Dispose()
        {
            BattleInputGate currentOwner = owner;
            if (currentOwner == null)
                return;

            owner = null;
            currentOwner.Release();
        }
    }
}
