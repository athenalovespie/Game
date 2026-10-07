using System;
using System.Collections.Generic;
using System.Linq;
using First_game.Doors;
using First_game.Entities;
using First_game.Inventory;
using First_game.Placement;
using Microsoft.Xna.Framework;
using PlayerInventory = First_game.Inventory.Inventory;

namespace First_game.Interiors;

public sealed class InteriorSaveEntry
{
    public string Key { get; set; }
    public string TemplateId { get; set; }
    public string Contents { get; set; }
}
public sealed class InteriorSnapshot
{
    public string ActiveMapId { get; set; } = "exterior";
    public string ActivePlacementId { get; set; }
    public InteriorSaveEntry[] Instances { get; set; } = Array.Empty<InteriorSaveEntry>();
}

/// <summary>Owns lazy state and resident rooms. DoorSystem remains the sole transition/collision service.</summary>
public sealed class InteriorManager : IDisposable
{
    private sealed class Portal
    {
        public PlacementObject Object;
        public InteriorTemplate Template;
        public string Key;
        public Door Door;
    }
    private sealed class State
    {
        public InteriorTemplate Template;
        public PlayerInventory Contents;
        public ResidentArea Room;
    }
    private readonly Player player;
    private readonly DoorSystem doors;
    private readonly PlacementWorld placements;
    private readonly ItemDefinitionRegistry definitions;
    private readonly ItemCategoryBehaviorRegistry behaviors;
    private readonly ResidentArea exterior;
    private readonly Door[] staticDoors;
    private readonly Dictionary<string, ResidentArea> staticAreas;
    private readonly Dictionary<ItemDefinition, InteriorTemplate> templates = new();
    private readonly Dictionary<string, Portal> portals = new(StringComparer.Ordinal);
    private Dictionary<string, State> states = new(StringComparer.Ordinal);
    public string ActivePlacementId { get; private set; }
    public int InstanceCount => states.Count;
    public int ResidentRoomCount => states.Values.Count(s => s.Room != null);
    public Vector2 PlayerPosition => player.Position;
    public PlayerInventory ActiveContents => ActivePlacementId != null && portals.TryGetValue(ActivePlacementId, out var portal)
        && states.TryGetValue(portal.Key, out var state) ? state.Contents : null;

    public InteriorManager(Player player, DoorSystem doors, PlacementWorld placements, InteriorRegistry registry,
        ItemDefinitionRegistry definitions, ItemCategoryBehaviorRegistry behaviors)
    {
        this.player = player; this.doors = doors; this.placements = placements;
        this.definitions = definitions; this.behaviors = behaviors;
        staticAreas = doors.Areas.ToDictionary(a => a.Id, StringComparer.Ordinal);
        exterior = staticAreas["exterior"]; staticDoors = (Door[])exterior.Triggers.Doors.Clone();
        foreach (var definition in definitions.All)
        {
            var data = definition.Placeable?.Enterable;
            if (data == null) continue;
            InteriorTemplate template = registry.GetRequired(data.InteriorTemplateId);
            var area = template.CreateArea("validation");
            Vector2 spawn = template.TileCenter(data.InteriorSpawnTile);
            if (area.BlocksMovement(player.BoundsAt(player.PositionForGround(spawn)))
                || area.BlocksMovement(player.BoundsAt(player.PositionForGround(template.TileCenter(template.ExitTile))))
                || template.TileBounds(template.ExitTile).Intersects(player.BoundsAt(player.PositionForGround(spawn))))
                throw new InvalidOperationException("Interior spawn/exit must fit the player and remain separate: " + template.Id);
            templates.Add(definition, template);
        }
        placements.Added += Added; placements.Removed += Removed; placements.Restored += Rebuild;
        placements.CanPickUp = CanPickUp;
        doors.AreaChanged += AreaChanged;
        Rebuild();
    }
    private string Key(PlacementObject obj) => obj.Definition.Placeable.Enterable.Shared
        ? "shared:" + obj.Definition.Placeable.Enterable.InteriorTemplateId : "instance:" + obj.InstanceId;
    private void Added(PlacementObject obj)
    {
        if (!templates.TryGetValue(obj.Definition, out var template)) return;
        var portal = new Portal { Object = obj, Template = template, Key = Key(obj) };
        var data = obj.Definition.Placeable;
        Point tile = data.Enterable.OutsideTile(data, obj.OriginTile, obj.Rotation);
        Vector2 corner = placements.Grid.CellToWorld(tile);
        portal.Door = new Door("placement:" + obj.InstanceId,
            new Rectangle((int)corner.X, (int)corner.Y, placements.Grid.CellSize, placements.Grid.CellSize),
            new Enterable(() => ResolveEntry(portal), () => ActivePlacementId = obj.InstanceId), false);
        portals[obj.InstanceId] = portal;
        RefreshExterior();
    }
    private void RefreshExterior()
    {
        exterior.Triggers.Replace(staticDoors.Concat(portals.Values.Select(p => p.Door)).ToArray());
        doors.RefreshTriggers();
    }
    private void Rebuild()
    {
        var live = placements.Objects.Select(o => o.InstanceId).ToHashSet(StringComparer.Ordinal);
        foreach (string id in portals.Keys.Where(id => !live.Contains(id)).ToArray()) portals.Remove(id);
        foreach (var obj in placements.Objects)
            if (!portals.TryGetValue(obj.InstanceId, out var portal) || !ReferenceEquals(portal.Object, obj)) Added(obj);
        Prune(); RefreshExterior();
    }
    private void Removed(PlacementObject obj)
    {
        if (!portals.Remove(obj.InstanceId)) return;
        Prune(); RefreshExterior();
    }
    private void Prune()
    {
        var live = portals.Values.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        foreach (string key in states.Keys.Where(key => !live.Contains(key)).ToArray())
        {
            Release(states[key]); states.Remove(key);
        }
    }
    private bool CanPickUp(PlacementObject obj)
    {
        // All exterior pickup requests are blocked while the actor is in any interior.
        if (!doors.ActiveArea.IsExterior || doors.IsTransitioning) return false;
        return !portals.TryGetValue(obj.InstanceId, out var portal) || !states.TryGetValue(portal.Key, out var state)
            || !HasContents(state.Contents);
    }
    private static bool HasContents(PlayerInventory inventory)
    {
        for (int i = 0; i < inventory.Capacity; i++) if (inventory.GetSlot(i) != null) return true;
        return false;
    }
    private PortalDestination ResolveEntry(Portal portal)
    {
        if (!states.TryGetValue(portal.Key, out var state))
        {
            state = new State { Template = portal.Template, Contents = new PlayerInventory(definitions, behaviors) };
            states.Add(portal.Key, state);
        }
        Materialize(portal.Key, state);
        var spawn = portal.Object.Definition.Placeable.Enterable.InteriorSpawnTile;
        return new PortalDestination(state.Room, new AreaSpawn(portal.Template.TileCenter(spawn), -Vector2.UnitY));
    }
    private void Materialize(string key, State state)
    {
        if (state.Room != null) return;
        state.Room = state.Template.CreateArea(key);
        state.Room.SetDoors(new[] { new Door("exit:" + key, state.Template.TileBounds(state.Template.ExitTile),
            new Enterable(ResolveExit, () => ActivePlacementId = null), true) });
        doors.RegisterArea(state.Room);
    }
    private PortalDestination ResolveExit()
    {
        if (ActivePlacementId == null || !portals.TryGetValue(ActivePlacementId, out var portal))
            throw new InvalidOperationException("The entered placement no longer exists.");
        PlacementObject obj = portal.Object;
        var data = obj.Definition.Placeable;
        Vector2 preferred = data.Enterable.ReturnGround(placements.Grid, data, obj.OriginTile, obj.Rotation, player.Bounds);
        if (!ReturnPosition.TryFind(placements.Grid, preferred, player.Bounds, out Vector2 ground))
            throw new InvalidOperationException("No free exterior return tile; remain inside until one is available.");
        Point direction = data.Enterable.Direction(obj.Rotation);
        return new PortalDestination(exterior, new AreaSpawn(ground, new Vector2(direction.X, direction.Y)));
    }
    private void AreaChanged()
    {
        if (doors.ActiveArea.IsExterior) ActivePlacementId = null;
        // Keep item state, release inactive room/trigger objects. Re-entry rematerializes from the cached template.
        foreach (var state in states.Values)
            if (state.Room != null && state.Room != doors.ActiveArea) Release(state);
    }
    private void Release(State state)
    {
        if (state.Room == null) return;
        doors.UnregisterArea(state.Room); state.Room = null;
    }
    public InteriorSnapshot Capture() => new() {
        ActiveMapId = doors.ActiveArea.Id, ActivePlacementId = ActivePlacementId,
        Instances = states.Select(pair => new InteriorSaveEntry { Key = pair.Key,
            TemplateId = pair.Value.Template.Id, Contents = pair.Value.Contents.SaveToJson() }).ToArray() };

    public void Restore(InteriorSnapshot snapshot, Vector2 position)
    {
        snapshot ??= new InteriorSnapshot(); // Version-1 saves were exterior-only.
        if (snapshot.Instances == null || snapshot.ActiveMapId == null) throw new InvalidOperationException("Invalid interior snapshot.");
        var staged = new Dictionary<string, State>(StringComparer.Ordinal);
        var owners = portals.Values.GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.First());
        foreach (var entry in snapshot.Instances)
        {
            if (entry?.Key == null || !owners.TryGetValue(entry.Key, out var owner) || entry.TemplateId != owner.Template.Id)
                throw new InvalidOperationException("Saved interior references an unknown placement/template.");
            staged.Add(entry.Key, new State { Template = owner.Template,
                Contents = PlayerInventory.LoadFromJson(entry.Contents, definitions, behaviors) });
        }
        ResidentArea target;
        State active = null;
        string activeKey = null;
        if (snapshot.ActivePlacementId != null)
        {
            if (!portals.TryGetValue(snapshot.ActivePlacementId, out var portal)
                || !staged.TryGetValue(portal.Key, out active) || snapshot.ActiveMapId != "interior:" + portal.Key)
                throw new InvalidOperationException("Saved active interior has no matching instance.");
            activeKey = portal.Key; target = active.Template.CreateArea(activeKey);
        }
        else if (!staticAreas.TryGetValue(snapshot.ActiveMapId, out target))
            throw new InvalidOperationException("Unknown saved map.");
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || target.BlocksMovement(player.BoundsAt(position))
            || doors.IsTransitioning) throw new InvalidOperationException("Invalid saved player position.");
        foreach (var state in states.Values) Release(state);
        states = staged;
        ActivePlacementId = snapshot.ActivePlacementId;
        if (active != null) { Materialize(activeKey, active); target = active.Room; }
        doors.RestoreLocation(target, position);
    }
    public void Dispose()
    {
        placements.Added -= Added; placements.Removed -= Removed; placements.Restored -= Rebuild;
        placements.CanPickUp = null; doors.AreaChanged -= AreaChanged;
        foreach (var state in states.Values) Release(state);
        states.Clear(); portals.Clear(); exterior.Triggers.Replace(staticDoors);
    }
}
