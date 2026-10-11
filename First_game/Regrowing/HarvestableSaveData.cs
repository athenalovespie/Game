using System;
namespace First_game.Regrowing;

public sealed class HarvestableSnapshot
{
    public double GameplaySeconds { get; set; }
    public HarvestableSaveEntry[] Instances { get; set; } = Array.Empty<HarvestableSaveEntry>();
}
public sealed class HarvestableSaveEntry
{
    public string Id { get; set; }
    public string MapId { get; set; }
    public string TypeId { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public bool Spawned { get; set; }
    public string State { get; set; }
    public double? RegrowAt { get; set; }
    public string RuleId { get; set; }
    public float NodeX { get; set; }
    public float NodeY { get; set; }
    public double? SpawnAt { get; set; }
    public int PendingYield { get; set; }
}
