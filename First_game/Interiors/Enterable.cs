using System;
using First_game.Doors;

namespace First_game.Interiors;

public readonly record struct PortalDestination(ResidentArea Area, AreaSpawn Spawn);

/// <summary>Both configured house doors and lazy placeable doors feed the existing DoorSystem transition.</summary>
public sealed class Enterable
{
    private readonly Func<PortalDestination> resolve;
    public Action OnArrived { get; }
    public Enterable(Func<PortalDestination> resolve, Action onArrived = null)
    {
        this.resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
        OnArrived = onArrived;
    }
    public PortalDestination Resolve() => resolve();
}
