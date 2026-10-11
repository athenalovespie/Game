using System;
using First_game.Doors;
using First_game.Entities;
using First_game.Input;
using First_game.UI;
using PlayerInventory = First_game.Inventory.Inventory;

namespace First_game.Regrowing;

/// <summary>Movement/state events refresh only nearby candidates; uses the shared right-click input edge.</summary>
public sealed class HarvestableInteraction : IDisposable
{
    private readonly HarvestableManager manager;
    private readonly Player player;
    private readonly DoorSystem doors;
    private readonly PlayerInventory inventory;
    private SpatialTriggers<HarvestableInstance> triggers;
    private HarvestableInstance candidate;
    public bool HasCandidate => candidate != null;
    public HarvestableInteraction(HarvestableManager manager, Player player, DoorSystem doors, PlayerInventory inventory)
    {
        this.manager = manager; this.player = player; this.doors = doors; this.inventory = inventory;
        player.PositionChanged += Refresh;
        doors.AreaChanged += Refresh;
        manager.Changed += Refresh;
        Refresh();
    }
    private void Refresh()
    {
        var next = manager.Triggers(player.MapId);
        if (!ReferenceEquals(next, triggers)) { triggers?.Reset(); triggers = next; }
        triggers?.Observe(player.Bounds);
        candidate = null;
        if (triggers == null) return;
        float nearest = float.MaxValue;
        for (int i = 0; i < triggers.OverlapCount; i++)
        {
            var p = triggers.GetOverlap(i);
            if (!p.InRange(player.GroundPosition)) continue;
            float distance = Microsoft.Xna.Framework.Vector2.DistanceSquared(player.GroundPosition, p.Sprite.Position);
            // Prefer a ripe plant over an empty neighbor; keep empty plants as input consumers.
            if (candidate != null && candidate.IsRipe && !p.IsRipe) continue;
            if (candidate == null || (p.IsRipe && !candidate.IsRipe) || distance < nearest)
            { candidate = p; nearest = distance; }
        }
    }
    public bool TryInteract(UIInput input, bool enabled)
    {
        if (!enabled || doors.IsTransitioning || player.Actions.IsBusy || candidate == null
            || input.Mouse?.RightClicked != true) return false;
        manager.TryHarvest(candidate, inventory);
        return true; // Empty/full plants still consume the click; no pickup/tool fall-through.
    }
    public void Dispose()
    {
        player.PositionChanged -= Refresh;
        doors.AreaChanged -= Refresh; manager.Changed -= Refresh;
    }
}