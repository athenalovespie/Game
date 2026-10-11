using System;
using System.Collections.Generic;
using First_game.Input;
using First_game.Placement;
using First_game.World;
using Microsoft.Xna.Framework;
using MonoGameLibrary.Graphics;
using PlayerInventory = First_game.Inventory.Inventory;

namespace First_game.Regrowing;

/// <summary>One gameplay clock and heap for all maps, including inactive maps.
/// Instances, sprites and heap capacity are allocated at registration/load, never on regrowth.</summary>
public sealed class HarvestableManager
{
    private sealed class Map
    {
        public WorldGrid Grid;
        public SpatialTriggers<HarvestableInstance> Triggers;
    }
    private readonly Dictionary<string, Map> maps = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HarvestableInstance> byId = new(StringComparer.Ordinal);
    private readonly List<HarvestableInstance> instances = new();
    private readonly HarvestableRegistry definitions;
    private readonly WorldSpawnRuleRegistry rules;
    private readonly Random random;
    private readonly RegrowScheduler scheduler = new();
    private bool harvesting;
    public string PromptPrefix { get; set; } = "Press E to ";
    public double Now { get; private set; }
    public IReadOnlyList<HarvestableInstance> Instances { get; }
    public event Action<HarvestableInstance, int> OnHarvested;
    public event Action<HarvestableInstance> OnRegrown;
    public event Action<HarvestableInstance> PackFull;
    public event Action Changed;
    // Optional actor exclusion used only when attempting initial/retry placement.
    public Func<string, Rectangle, bool> IntersectsActor { get; set; }

    public HarvestableManager(HarvestableRegistry definitions, WorldSpawnRuleRegistry rules, Random random)
    {
        this.definitions = definitions; this.rules = rules; this.random = random;
        Instances = instances.AsReadOnly();
    }
    public void RegisterMap(string id, WorldGrid grid)
    {
        maps.Add(id, new Map { Grid = grid });
        RebuildTriggers();
    }
    public bool BlocksMovement(string mapId, Rectangle bounds) =>
        maps.TryGetValue(mapId, out var map) && map.Grid.IntersectsBlockedCell(bounds);

    public HarvestableInstance RegisterNode(string mapId, WorldSpawnNode node, WorldSpawnRule rule)
    {
        var instance = Create(mapId + ":" + rule.RuleId + ":" + instances.Count, mapId, rule.HarvestableTypeId);
        instance.SpawnRule = rule; instance.NodePosition = node.Position;
        ConfigureTrigger(instance);
        TrySpawn(instance);
        return instance;
    }
    private HarvestableInstance Create(string id, string mapId, string type)
    {
        if (!maps.ContainsKey(mapId)) throw new InvalidOperationException("Register the map grid before its plants.");
        var instance = new HarvestableInstance(id, mapId, definitions.GetRequired(type));
        instance.Prompt = PromptPrefix + instance.Data.PromptText;
        byId.Add(id, instance); instances.Add(instance); scheduler.Reserve(instances.Count);
        return instance;
    }
    private Rectangle Bounds(HarvestableInstance instance, Point tile)
    {
        var grid = maps[instance.MapId].Grid;
        Vector2 corner = grid.CellToWorld(tile);
        return new Rectangle((int)corner.X, (int)corner.Y,
            instance.Data.Footprint.Width * grid.CellSize, instance.Data.Footprint.Height * grid.CellSize);
    }
    private void ConfigureTrigger(HarvestableInstance instance)
    {
        var grid = maps[instance.MapId].Grid;
        // Includes every possible placement around the node. Exact range is checked only locally.
        instance.TriggerBounds = Bounds(instance, grid.WorldToCell(instance.NodePosition));
        var bounds = instance.TriggerBounds;
        bounds.Inflate(2 * grid.CellSize + instance.Data.RangePixels + 1,
            2 * grid.CellSize + instance.Data.RangePixels + 1);
        instance.TriggerBounds = bounds;
    }
    public void RebuildTriggers()
    {
        foreach (var pair in maps)
        {
            var members = instances.FindAll(i => i.MapId == pair.Key).ToArray();
            pair.Value.Triggers = new SpatialTriggers<HarvestableInstance>(members);
        }
        Changed?.Invoke();
    }
    public SpatialTriggers<HarvestableInstance> Triggers(string mapId) =>
        maps.TryGetValue(mapId, out var map) ? map.Triggers : null;

    private bool Place(HarvestableInstance instance, Point tile, bool excludeActor)
    {
        var grid = maps[instance.MapId].Grid;
        var footprint = instance.Data.Footprint;
        if (!PlacementValidity.CanOccupy(grid, footprint, tile)) return false;
        Rectangle bounds = Bounds(instance, tile);
        if (excludeActor && IntersectsActor?.Invoke(instance.MapId, bounds) == true) return false;
        if (!grid.OccupyArea(tile, footprint.Width, footprint.Height, CellType.Plant, instance, true)) return false;
        instance.Tile = tile; instance.FootprintBounds = bounds; instance.IsSpawned = true;
        instance.SetState(instance.StateIndex);
        return true;
    }
    private void TrySpawn(HarvestableInstance instance)
    {
        instance.SpawnAt = null;
        var rule = instance.SpawnRule;
        if (random.NextDouble() < rule.Chance)
        {
            Point origin = maps[instance.MapId].Grid.WorldToCell(instance.NodePosition);
            for (int attempt = 0; attempt < 16; attempt++)
            {
                Point tile = attempt == 0 ? origin : origin + new Point(random.Next(-2, 3), random.Next(-2, 3));
                if (!Place(instance, tile, excludeActor: true)) continue;
                // Preserve the original spawner's RNG consumption for stable tree/save layouts.
                random.Next(rule.MinimumCount, rule.MaximumCount + 1);
                Changed?.Invoke();
                return;
            }
        }
        if (rule.RespawnSeconds > 0)
        {
            instance.SpawnAt = RegrowScheduler.Deadline(Now, rule.RespawnSeconds);
            scheduler.Schedule(instance, instance.SpawnAt.Value);
        }
    }
    public void Advance(double seconds, bool paused)
    {
        if (!double.IsFinite(seconds) || seconds < 0 || !double.IsFinite(Now + seconds))
            throw new ArgumentOutOfRangeException(nameof(seconds));
        if (paused) return;
        Now += seconds;
        ProcessDue();
    }
    public void ProcessDue()
    {
        while (scheduler.TryTakeDue(Now, out var instance))
        {
            if (!instance.IsSpawned) { TrySpawn(instance); continue; }
            instance.RegrowAt = null;
            instance.SetState(instance.Data.RipeState);
            instance.PendingYield = 0;
            Changed?.Invoke();
            OnRegrown?.Invoke(instance);
        }
    }
    public bool TryHarvest(HarvestableInstance instance, PlayerInventory inventory)
    {
        if (harvesting || instance == null || !instance.IsRipe
            || !byId.TryGetValue(instance.Id, out var owned) || !ReferenceEquals(instance, owned)) return false;
        int count = instance.PendingYield;
        if (count == 0) instance.PendingYield = count = instance.Data.Yield.Roll(random);
        if (!inventory.HasSpaceFor(instance.Definition.YieldItem.QualifiedId, count))
        {
            PackFull?.Invoke(instance); return false;
        }
        double deadline = RegrowScheduler.Deadline(Now, instance.Data.RegrowSeconds);
        // Lock before AddItem notifies listeners: reentrant interactions cannot award twice.
        harvesting = true;
        try
        {
            instance.SetState(instance.Data.EmptyState);
            instance.RegrowAt = deadline; instance.PendingYield = 0;
            scheduler.Schedule(instance, deadline);
            inventory.AddItem(instance.Definition.YieldItem.QualifiedId, count);
            Changed?.Invoke();
            OnHarvested?.Invoke(instance, count);
        }
        finally { harvesting = false; }
        return true;
    }
    public void SubmitDraw(WorldRenderer renderer, string mapId)
    {
        for (int i = 0; i < instances.Count; i++)
        {
            var instance = instances[i];
            if (instance.MapId == mapId && instance.IsSpawned)
                renderer.Submit(instance.Sprite.SortY, instance.DrawAction);
        }
    }
    public HarvestableSnapshot Capture()
    {
        var entries = new HarvestableSaveEntry[instances.Count];
        for (int i = 0; i < entries.Length; i++)
        {
            var p = instances[i];
            entries[i] = new HarvestableSaveEntry { Id = p.Id, MapId = p.MapId, TypeId = p.Data.Id,
                TileX = p.Tile.X, TileY = p.Tile.Y, Spawned = p.IsSpawned, State = p.StateId,
                RegrowAt = p.RegrowAt, RuleId = p.SpawnRule.RuleId, NodeX = p.NodePosition.X,
                NodeY = p.NodePosition.Y, SpawnAt = p.SpawnAt, PendingYield = p.PendingYield };
        }
        return new HarvestableSnapshot { GameplaySeconds = Now, Instances = entries };
    }
    public void Clear()
    {
        foreach (var p in instances)
        {
            if (!p.IsSpawned) continue;
            var grid = maps[p.MapId].Grid;
            for (int y = 0; y < p.Data.Footprint.Height; y++) for (int x = 0; x < p.Data.Footprint.Width; x++)
            {
                Point tile = p.Tile + new Point(x, y);
                if (ReferenceEquals(grid.GetCell(tile).Occupant, p)) grid.ClearCell(tile);
            }
        }
        instances.Clear(); byId.Clear(); scheduler.Clear();
    }
    // WorldSaveStore provides rollback across inventory, occupancy and all resource systems.
    public void Restore(HarvestableSnapshot snapshot)
    {
        if (snapshot?.Instances == null || !double.IsFinite(snapshot.GameplaySeconds) || snapshot.GameplaySeconds < 0)
            throw new InvalidOperationException("Invalid harvestable save.");
        Clear(); Now = snapshot.GameplaySeconds;
        scheduler.Reserve(snapshot.Instances.Length);
        foreach (var e in snapshot.Instances)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.Id) || e.MapId == null || e.TypeId == null
                || e.RuleId == null || !float.IsFinite(e.NodeX) || !float.IsFinite(e.NodeY)
                || !ValidTime(e.RegrowAt) || !ValidTime(e.SpawnAt))
                throw new InvalidOperationException("Invalid saved plant identity/deadline.");
            var rule = rules.GetRequired(e.RuleId);
            if (rule.HarvestableTypeId != e.TypeId) throw new InvalidOperationException("Saved plant/spawn type mismatch.");
            var p = Create(e.Id, e.MapId, e.TypeId);
            p.SpawnRule = rule; p.NodePosition = new Vector2(e.NodeX, e.NodeY);
            p.SetState(p.Data.StateIndex(e.State));
            p.RegrowAt = e.RegrowAt; p.SpawnAt = e.SpawnAt; p.PendingYield = e.PendingYield;
            if ((e.Spawned && (e.SpawnAt.HasValue || (p.StateIndex == p.Data.EmptyState) != e.RegrowAt.HasValue))
                || (!e.Spawned && (e.RegrowAt.HasValue || p.StateIndex != p.Data.RipeState))
                || e.PendingYield < 0 || e.PendingYield > (long)p.Data.Yield.Maximum + p.Data.Yield.BonusQuantity
                || (e.PendingYield > 0 && (!e.Spawned || p.StateIndex != p.Data.RipeState)))
                throw new InvalidOperationException("Invalid saved plant state.");
            ConfigureTrigger(p);
            if (e.Spawned && !Place(p, new Point(e.TileX, e.TileY), excludeActor: false))
                throw new InvalidOperationException("Saved plant footprint is blocked.");
            // Also cover migrated saves whose old pickup tile differed from its source node.
            var trigger = Rectangle.Union(p.TriggerBounds, Bounds(p, new Point(e.TileX, e.TileY)));
            trigger.Inflate(p.Data.RangePixels, p.Data.RangePixels); p.TriggerBounds = trigger;
            double? due = p.RegrowAt ?? p.SpawnAt;
            if (due.HasValue) scheduler.Schedule(p, due.Value);
        }
        RebuildTriggers();
        ProcessDue();
        Changed?.Invoke();
    }
    private static bool ValidTime(double? value) => !value.HasValue || (double.IsFinite(value.Value) && value.Value >= 0);

    public HarvestableSnapshot MigratePickups(PickupSnapshot pickups, double now)
    {
        var entries = new List<HarvestableSaveEntry>();
        void Convert(PickupSaveEntry e, bool spawned)
        {
            var rule = rules.GetRequired(e.RuleId);
            if (rule.HarvestableTypeId == null) return;
            var data = definitions.GetRequired(rule.HarvestableTypeId).Data;
            entries.Add(new HarvestableSaveEntry { Id = "legacy:" + entries.Count, MapId = "exterior",
                TypeId = data.Id, RuleId = rule.RuleId, NodeX = e.NodeX, NodeY = e.NodeY,
                TileX = e.TileX, TileY = e.TileY, Spawned = spawned, State = data.State(data.RipeState).Id,
                SpawnAt = spawned ? null : e.Due });
        }
        foreach (var e in pickups.Active) Convert(e, true);
        foreach (var e in pickups.Pending) Convert(e, false);
        return new HarvestableSnapshot { GameplaySeconds = now, Instances = entries.ToArray() };
    }
}
