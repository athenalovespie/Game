using System;
using Microsoft.Xna.Framework;

namespace First_game.Doors;

public readonly record struct AreaSpawn(Vector2 GroundPosition, Vector2 FacingDirection);

public sealed class Door
{
    public string Id { get; }
    public Rectangle TriggerBounds { get; }
    public ResidentArea TargetArea { get; }
    public AreaSpawn TargetSpawn { get; }
    public bool IsExit { get; }
    public string DestinationError { get; }
    public string Prompt { get; internal set; }

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
        IsExit = isExit;
        DestinationError = "Door '" + id + "' cannot enter '" + targetArea.Id
            + "': destination unavailable or spawn blocked.";
    }
}
