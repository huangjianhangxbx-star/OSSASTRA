using System;

/// <summary>
/// Owns the single scene-level modal UI lease without coupling its consumers.
/// </summary>
public sealed class GameUIModalCoordinator
{
    public object ActiveOwner { get; private set; }
    public event Action<object> ActiveOwnerChanged;

    public bool TryAcquire(object owner)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (ReferenceEquals(ActiveOwner, owner))
            return true;
        if (ActiveOwner != null)
            return false;

        ActiveOwner = owner;
        ActiveOwnerChanged?.Invoke(owner);
        return true;
    }

    public bool Release(object owner)
    {
        if (owner == null || !ReferenceEquals(ActiveOwner, owner))
            return false;

        ActiveOwner = null;
        ActiveOwnerChanged?.Invoke(null);
        return true;
    }

    public bool IsOwnedBy(object owner) =>
        owner != null && ReferenceEquals(ActiveOwner, owner);
}
