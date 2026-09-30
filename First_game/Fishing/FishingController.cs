using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using First_game.Entities;
using PlayerInventory = First_game.Inventory.Inventory;

namespace First_game.Fishing;

/// <summary>Routes water interactions. Player.Actions owns execution and cancellation.</summary>
public sealed class FishingController
{
    private readonly Player player;
    private readonly PlayerInventory inventory;
    private readonly List<FishingSpot> spots = new();
    private FishingAction lastAttempt;
    private float messageSeconds;
    private string message;
    public FishingAction Active => player.Actions.Current as FishingAction;
    public string Message => Active?.Message ?? (messageSeconds > 0 ? message : null);

    // Replace this factory to use a FishingAction subclass containing your own minigame.
    public Func<FishingSpot, Vector2, PlayerInventory, FishingAction> CreateAction { get; set; }
        = (spot, target, inventory) => new FishingAction(spot, target, inventory);

    public FishingController(Player player, PlayerInventory inventory)
    {
        this.player = player;
        this.inventory = inventory;
    }
    public void Register(FishingSpot spot) => spots.Add(spot);

    // True means this was water, even if starting failed; don't also collect a pickup.
    public bool TryInteract(Vector2 target)
    {
        foreach (FishingSpot spot in spots)
        {
            if (!spot.Contains(target)) continue;
            var attempt = CreateAction(spot, target, inventory);
            if (player.Actions.TryStart(attempt, out string reason)) lastAttempt = attempt;
            else ShowMessage(reason);
            return true;
        }
        return false;
    }
    public void Update(GameTime time)
    {
        messageSeconds = MathF.Max(0, messageSeconds - (float)time.ElapsedGameTime.TotalSeconds);
        if (lastAttempt != null && Active != lastAttempt)
        {
            ShowMessage(lastAttempt.Message);
            lastAttempt = null;
        }
    }
    private void ShowMessage(string text) { message = text; messageSeconds = 3f; }
}
