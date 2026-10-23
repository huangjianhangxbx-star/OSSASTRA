using UnityEngine;

public interface ITargetable
{
    BattleCamp BattleCamp { get; }

    bool IsAlive { get; }

    bool CanBeTargeted { get; }

    Transform TargetPoint { get; }

    Transform Transform { get; }
}