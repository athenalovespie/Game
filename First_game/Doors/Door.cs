using System;
using First_game.Interiors;
using Microsoft.Xna.Framework;

namespace First_game.Doors;

public readonly record struct AreaSpawn(Vector2 GroundPosition, Vector2 FacingDirection);

public sealed class Door
{
    public Enterable Enterable { get; }
    public bool GroundOnly { get; }
    public string Id { get; }
    public Rectangle TriggerBounds { get; }
    public ResidentArea TargetArea { get; }
    public AreaSpawn TargetSpawn { get; }
    public bool IsExit { get; }
    public string DestinationError { get; }
    public string Prompt { get; internal set; }

    public Door(string id, Rectangle triggerBounds, Enterable enterable, bool isExit, bool groundOnly = true)
    {
        if (triggerBounds.Width <= 0 || triggerBounds.Height <= 0) throw new ArgumentException("Invalid trigger.");
        Id = id; TriggerBounds = triggerBounds; Enterable = enterable; IsExit = isExit; GroundOnly = groundOnly;
        DestinationError = "Door '" + id + "' destination unavailable or spawn blocked.";
    }

    internal bool SuppressedUntilExit;
    internal int Visit;

    public Door(string id, Rectangle triggerBounds, ResidentArea targetArea,
        AreaSpawn targetSpawn, bool isExit)
    {
        if (triggerBounds.Width <= 0 || triggerBounds.Height <= 0)
            throw new ArgumentException("A door trigger must have positive dimensions.");
        Id = id;
        TriggerBounds = triggerBounds;
        TargetArea = targetArea ?? throw new ArgumentNullException(nameof(targetArea));
        TargetSpawn = targetSpawn;
        Enterable = new Enterable(() => new PortalDestination(targetArea, targetSpawn));
        IsExit = isExit;
        DestinationError = "Door '" + id + "' cannot enter '" + targetArea.Id
            + "': destination unavailable or spawn blocked.";
    }
}
