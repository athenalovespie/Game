using System;
using Microsoft.Xna.Framework;
using First_game.Actions;
using First_game.Entities;
using PlayerInventory = First_game.Inventory.Inventory;

namespace First_game.Fishing;

public enum FishingState { Casting, Catching, Caught, Failed, Cancelled }

public class FishingAction : PlayerAction
{
    private readonly FishingSpot spot;
    private readonly PlayerInventory inventory;
    private float elapsed;
    public override string Name => "Fishing";
    public Vector2 Target { get; }
    public FishingState State { get; private set; }
    public string Message { get; private set; }
    public float CastProgress => Math.Clamp(elapsed / spot.Settings.CastSeconds, 0, 1);

    public FishingAction(FishingSpot spot, Vector2 target, PlayerInventory inventory)
    {
        this.spot = spot;
        Target = target;
        this.inventory = inventory;
    }
    public override bool CanStart(Player player, out string reason)
    {
        reason = null;
        if (!spot.Contains(Target)) reason = "Cast into the water.";
        else if (Vector2.Distance(player.GroundPosition, Target) > spot.Settings.CastRange)
            reason = "Move closer to the water.";
        else if (spot.Settings.RequiredToolId != null && inventory.GetItemCount(spot.Settings.RequiredToolId) == 0)
            reason = "You need a fishing rod.";
        else if (!inventory.HasSpaceFor(spot.ItemId)) reason = "Make room for a fish first.";
        return reason == null;
    }
    public override void Begin(Player player)
    {
        player.FaceTowards(Target);
        ChangeState(player, FishingState.Casting, "Casting...");
    }
    private void ChangeState(Player player, FishingState state, string message)
    {
        State = state;
        Message = message;
        elapsed = 0;
        string prefix = state switch
        {
            FishingState.Casting => "FishCast",
            _ => "FishWait"
        };
        player.PlayActionAnimation(prefix + player.Facing);
    }
    public override void Update(Player player, GameTime time, ActionInput input)
    {
        elapsed += (float)time.ElapsedGameTime.TotalSeconds;
        switch (State)
        {
            case FishingState.Casting:
                if (elapsed >= spot.Settings.CastSeconds)
                {
                    ChangeState(player, FishingState.Catching, "Fishing ready. Q to cancel.");
                    BeginCatch(player);
                }
                break;
            case FishingState.Catching:
                UpdateCatch(player, time, input);
                break;
        }
    }
    // Override these two methods in your own fishing action, or implement your rules here.
    // The default scaffold waits for cancellation; it has no input rules or automatic reward.
    protected virtual void BeginCatch(Player player) { }
    protected virtual void UpdateCatch(Player player, GameTime time, ActionInput input) { }

    protected void SetMessage(string message) => Message = message;

    /// <summary>Call from your catch rules when they decide success or failure. Safe to call once or repeatedly.</summary>
    protected void FinishCatch(bool success)
    {
        if (State != FishingState.Catching || IsComplete) return;
        bool accepted = success && inventory.AddItem(spot.ItemId, 1) == 1;
        State = accepted ? FishingState.Caught : FishingState.Failed;
        Message = accepted ? "Caught a fish!" : success ? "Inventory full - fish released." : "The fish escaped.";
        Complete();
    }
    public override void End(Player player, ActionEndReason reason)
    {
        if (reason == ActionEndReason.Cancelled)
        {
            State = FishingState.Cancelled;
            Message = "Fishing cancelled.";
        }
    }
}
