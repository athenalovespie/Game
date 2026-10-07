using System.Collections.Generic;
namespace First_game.World;
public sealed class PickupSnapshot
{
    public List<PickupSaveEntry> Active { get; set; } = new();
    public List<PickupSaveEntry> Pending { get; set; } = new();
}
public sealed class PickupSaveEntry
{
    public string RuleId { get; set; }
    public float NodeX { get; set; }
    public float NodeY { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public int Count { get; set; }
    public double Due { get; set; }
}
