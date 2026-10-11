using First_game.Input;

namespace First_game.Doors;

// Compatibility facade; doors and regrowing plants share the same spatial trigger implementation.
public sealed class DoorTriggers : SpatialTriggers<Door>
{
    public DoorTriggers(Door[] doors) : base(doors) { }
}
