using System;
using System.Collections.Generic;
using First_game.Entities;
using First_game.Inventory;
using Microsoft.Xna.Framework;

namespace First_game.Harvesting;

/// <summary>Turns a world click and the live equipment selection into an action attempt.</summary>
public sealed class HarvestController
{
    private readonly Player player;
    private readonly Hotbar hotbar;
    private readonly ResourceWorld world;
    private readonly IReadOnlyDictionary<string, ToolDefinition> tools;
    public string LastFailure { get; private set; }

    public HarvestController(Player player, Hotbar hotbar, ResourceWorld world,
        IReadOnlyDictionary<string, ToolDefinition> tools)
    {
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        this.hotbar = hotbar ?? throw new ArgumentNullException(nameof(hotbar));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.tools = tools ?? throw new ArgumentNullException(nameof(tools));
    }

    public bool TryInteract(Vector2 worldPosition)
    {
        LastFailure = null;
        ResourceNode target = world.FindAt(worldPosition);
        if (target == null) return false;
        ItemInstance item = hotbar.SelectedItem;
        if (item == null || !tools.TryGetValue(item.QualifiedId, out ToolDefinition tool))
        {
            LastFailure = "Select a suitable tool in the hotbar.";
            return true;
        }
        var action = new HarvestAction(target, tool,
            () => world.Contains(target) && ReferenceEquals(hotbar.SelectedItem, item)
                && item.Durability != 0);
        player.Actions.TryStart(action, out string reason);
        LastFailure = reason;
        return true; // A recognized resource owns the click even when rejected.
    }
}
