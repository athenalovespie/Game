using Microsoft.Xna.Framework;

namespace First_game.Input;

public interface ISpatialTrigger
{
    Rectangle TriggerBounds { get; }
    bool GroundOnly { get; }
    bool SuppressedUntilExit { get; set; }
    int Visit { get; set; }
}
