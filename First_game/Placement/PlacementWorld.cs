using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using First_game.Inventory;
using First_game.World;
using Microsoft.Xna.Framework;
using PlayerInventory = First_game.Inventory.Inventory;

namespace First_game.Placement;

public sealed class PlacementSaveEntry
{
    public string QualifiedItemId { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public int Rotation { get; set; }
    public int Quality { get; set; }
    public int? Durability { get; set; }
}

public sealed class PlacementWorld
{
    private readonly ItemDefinitionRegistry definitions;
    private readonly Dictionary<ItemDefinition, Action<PlacementObject>> effects = new();
    private readonly List<PlacementObject> objects = new();
    private bool committing;
    public PlacementWorld(WorldGrid grid, ItemDefinitionRegistry definitions,
        IReadOnlyDictionary<string, Action<PlacementObject>> effectRegistry = null)
    {
        Grid = grid; this.definitions = definitions; Occupancy = new OccupancyGrid(grid);
        Objects = objects.AsReadOnly();
        foreach (var definition in definitions.All)
        {
            string effect = definition.Placeable?.OnPlacedEffect;
            if (effect == null) continue;
            if (effectRegistry == null || !effectRegistry.TryGetValue(effect, out var callback))
                throw new InvalidOperationException($"Unknown placement effect '{effect}'.");
            effects.Add(definition, callback);
        }
    }
    public WorldGrid Grid { get; }
    public OccupancyGrid Occupancy { get; }
    public IReadOnlyList<PlacementObject> Objects { get; }
    public Vector2 PlayerGround { get; set; }
    public Rectangle PlayerBounds { get; set; }
    public event Action<PlacementObject> Added;
    public event Action PlacementRejected;

    // Both preview and commit use this exact entry point; commit never trusts a cached green ghost.
    public bool IsPlacementValid(ItemDefinition definition, Point originTile, int rotation) =>
        Validate(definition, originTile, rotation, checkPlayer: true);

    private bool Validate(ItemDefinition definition, Point origin, int rotation, bool checkPlayer)
    {
        PlaceableData data = definition?.Placeable;
        if (data == null || !data.AllowsRotation(rotation)) return false;
        Point size = data.Size(rotation);
        // Check bounds before adding offsets, including hostile/corrupt save coordinates.
        if (origin.X < 0 || origin.Y < 0 || origin.X > Grid.Columns - size.X || origin.Y > Grid.Rows - size.Y) return false;
        float range = data.MaxRangeTiles * Grid.CellSize;
        for (int y = 0; y < size.Y; y++) for (int x = 0; x < size.X; x++)
        {
            Point tile = origin + new Point(x, y);
            GridCell cell = Grid.GetCell(tile);
            if (cell.PlacementForbidden || !data.AllowsGround(cell.Ground) || (!data.AllowOverlap && (cell.IsOccupied() || cell.BlocksMovement))) return false;
            if (!checkPlayer) continue;
            Vector2 topLeft = Grid.CellToWorld(tile);
            if (Vector2.DistanceSquared(Grid.CellCenter(tile), PlayerGround) > range * range
                || PlayerBounds.Intersects(new Rectangle((int)topLeft.X, (int)topLeft.Y, Grid.CellSize, Grid.CellSize))) return false;
        }
        return true;
    }

    public bool TryPlace(ItemDefinition definition, Point origin, int rotation,
        PlayerInventory inventory, int slot, ItemInstance expected)
    {
        if (committing) return false;
        if (expected == null || !ReferenceEquals(inventory.GetSlot(slot), expected)
            || expected.QualifiedId != definition?.QualifiedId || expected.Count < 1
            || !IsPlacementValid(definition, origin, rotation))
        {
            PlacementRejected?.Invoke();
            return false;
        }
        var obj = new PlacementObject(definition, origin, rotation, expected.Quality, expected.Durability);
        committing = true;
        try
        {
            Occupancy.Add(obj);
            objects.Add(obj);
            if (definition.Placeable.ConsumeOnPlacement && !inventory.TryConsumeSlot(slot, expected))
            {
                objects.Remove(obj); Occupancy.Remove(obj); return false;
            }
            Added?.Invoke(obj);
        }
        finally { committing = false; }
        // Effects are post-commit notifications, never a second inventory transaction.
        if (effects.TryGetValue(definition, out var effect))
        {
            try { effect(obj); }
            catch (Exception error) { Trace.TraceError($"Placement effect failed after commit: {error}"); }
        }
        return true;
    }

    // A hit is consumed even when out of range or full, so it cannot fall through to fishing/pickups.
    public bool TryPickUp(Point tile, PlayerInventory inventory)
    {
        PlacementObject obj = Occupancy.At(tile);
        if (obj == null) return false;
        float range = obj.Definition.Placeable.MaxRangeTiles * Grid.CellSize;
        if (committing || Vector2.DistanceSquared(Grid.CellCenter(tile), PlayerGround) > range * range) return true;
        bool refund = obj.Definition.Placeable.ConsumeOnPlacement;
        if (refund && !inventory.HasSpaceFor(obj.Definition.QualifiedId, 1, obj.Quality, obj.Durability)) return true;
        committing = true;
        try
        {
            Occupancy.Remove(obj); objects.Remove(obj);
            // Non-consuming placement tools already retain their item; removal must not mint another.
            if (refund) inventory.AddItem(obj.Definition.QualifiedId, 1, obj.Quality, obj.Durability);
        }
        finally { committing = false; }
        return true;
    }

    public PlacementSaveEntry[] Capture()
    {
        var result = new PlacementSaveEntry[objects.Count];
        for (int i = 0; i < result.Length; i++)
        {
            PlacementObject obj = objects[i];
            result[i] = new PlacementSaveEntry { QualifiedItemId = obj.Definition.QualifiedId,
                TileX = obj.OriginTile.X, TileY = obj.OriginTile.Y, Rotation = obj.Rotation,
                Quality = obj.Quality, Durability = obj.Durability };
        }
        return result;
    }
    public string SaveToJson() => JsonSerializer.Serialize(Capture());
    public void LoadFromJson(string json) => Restore(JsonSerializer.Deserialize<PlacementSaveEntry[]>(json));

    public void Restore(PlacementSaveEntry[] entries)
    {
        if (entries == null || committing) throw new InvalidOperationException("Invalid placement snapshot.");
        // Stage against the static world, excluding the current placement layer. Failure changes nothing.
        var stagingGrid = new WorldGrid(Grid.CellSize, Grid.Origin, Grid.Columns, Grid.Rows);
        for (int y = 0; y < Grid.Rows; y++) for (int x = 0; x < Grid.Columns; x++)
        {
            Point tile = new(x, y); GridCell source = Grid.GetCell(tile), target = stagingGrid.GetCell(tile);
            target.Ground = source.Ground; target.Type = source.Type;
            target.PlacementForbidden = source.PlacementForbidden;
            target.BlocksMovement = source.StaticBlocksMovement;
        }
        var staged = new PlacementWorld(stagingGrid, definitions, BuildEffectRegistry());
        foreach (PlacementSaveEntry entry in entries)
        {
            if (entry == null || entry.QualifiedItemId == null || !definitions.TryGet(entry.QualifiedItemId, out var definition)
                || entry.Quality < 0 || entry.Durability < 0
                || !staged.Validate(definition, new Point(entry.TileX, entry.TileY), entry.Rotation, checkPlayer: false))
                throw new InvalidOperationException("Saved placement is unknown, blocked, or outside the grid.");
            var obj = new PlacementObject(definition, new Point(entry.TileX, entry.TileY), entry.Rotation, entry.Quality, entry.Durability);
            staged.Occupancy.Add(obj); staged.objects.Add(obj);
        }
        foreach (var obj in objects) Occupancy.Remove(obj);
        objects.Clear();
        foreach (var obj in staged.objects) { Occupancy.Add(obj); objects.Add(obj); Added?.Invoke(obj); }
    }
    private Dictionary<string, Action<PlacementObject>> BuildEffectRegistry()
    {
        var registry = new Dictionary<string, Action<PlacementObject>>();
        foreach (var pair in effects) registry[pair.Key.Placeable.OnPlacedEffect] = pair.Value;
        return registry;
    }
}
