using System;
using System.IO;
using System.Text.Json;
using First_game.Harvesting;
using First_game.Inventory;
using First_game.Placement;
using Microsoft.Xna.Framework;
using PlayerInventory = First_game.Inventory.Inventory;

namespace First_game.World;

/// <summary>One file commits inventory, placed objects and their surrounding exterior together.</summary>
public static class WorldSaveStore
{
    public sealed class Snapshot
    {
        public int Version { get; set; } = 1;
        public string Inventory { get; set; }
        public PlacementSaveEntry[] Placements { get; set; }
        public PickupSnapshot Pickups { get; set; }
        public ResourceSaveEntry[] Resources { get; set; }
        public double ElapsedSeconds { get; set; }
        public float PlayerX { get; set; }
        public float PlayerY { get; set; }
    }
    public static Snapshot Capture(PlayerInventory inventory, PlacementWorld placements, WorldPickupSystem pickups,
        ResourceWorld resources, Vector2 playerPosition, TimeSpan elapsed) => new() {
            Inventory = inventory.SaveToJson(), Placements = placements.Capture(), Pickups = pickups.Capture(),
            Resources = resources.Capture(), PlayerX = playerPosition.X, PlayerY = playerPosition.Y, ElapsedSeconds = elapsed.TotalSeconds };

    public static void Write(string path, Snapshot snapshot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, overwrite: true);
    }
    public static Snapshot Read(string path) => JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(path));

    public static void Restore(Snapshot snapshot, PlayerInventory inventory, ItemDefinitionRegistry definitions,
        ItemCategoryBehaviorRegistry behaviors, PlacementWorld placements, WorldPickupSystem pickups, ResourceWorld resources)
    {
        if (snapshot == null || snapshot.Version != 1 || !float.IsFinite(snapshot.PlayerX) || !float.IsFinite(snapshot.PlayerY)
            || !double.IsFinite(snapshot.ElapsedSeconds) || snapshot.ElapsedSeconds < 0
            || snapshot.ElapsedSeconds > TimeSpan.MaxValue.TotalSeconds)
            throw new InvalidOperationException("Invalid world save.");
        // Validate inventory before changing the world; retain a full rollback for spatial conflicts.
        _ = PlayerInventory.LoadFromJson(snapshot.Inventory, definitions, behaviors);
        var previous = Capture(inventory, placements, pickups, resources, Vector2.Zero, TimeSpan.Zero);
        try { Apply(snapshot, inventory, placements, pickups, resources); }
        catch { Apply(previous, inventory, placements, pickups, resources); throw; }
    }
    private static void Apply(Snapshot snapshot, PlayerInventory inventory, PlacementWorld placements,
        WorldPickupSystem pickups, ResourceWorld resources)
    {
        placements.Restore(Array.Empty<PlacementSaveEntry>());
        pickups.Clear();
        resources.Restore(snapshot.Resources);
        pickups.Restore(snapshot.Pickups);
        placements.Restore(snapshot.Placements);
        inventory.RestoreFromJson(snapshot.Inventory);
    }
}
