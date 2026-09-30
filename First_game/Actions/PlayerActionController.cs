using System;
using Microsoft.Xna.Framework;
using First_game.Entities;

namespace First_game.Actions;

/// <summary>The only owner of the player's active action.</summary>
public sealed class PlayerActionController
{
    private readonly Player player;
    public PlayerAction Current { get; private set; }
    public bool IsBusy => Current != null;
    public bool BlocksMovement => Current?.BlocksMovement ?? false;

    public PlayerActionController(Player player) => this.player = player;

    public bool TryStart(PlayerAction action, out string reason)
    {
        ArgumentNullException.ThrowIfNull(action);
        reason = null;
        if (IsBusy) { reason = "Finish your current action first."; return false; }
        if (action.HasStarted) { reason = "Create a new action for each attempt."; return false; }
        if (!action.CanStart(player, out reason)) return false;
        Current = action;
        action.HasStarted = true;
        try { action.Begin(player); }
        catch { Finish(ActionEndReason.Cancelled); throw; }
        return true;
    }

    public void Update(GameTime time, ActionInput input)
    {
        if (Current == null) return;
        if (input.CancelPressed && Cancel()) return;
        try { Current.Update(player, time, input); }
        catch { Finish(ActionEndReason.Cancelled); throw; }
        if (Current?.IsComplete == true) Finish(ActionEndReason.Completed);
    }

    public bool Cancel()
    {
        if (Current == null || !Current.CanCancel) return false;
        Finish(ActionEndReason.Cancelled);
        return true;
    }

    private void Finish(ActionEndReason reason)
    {
        PlayerAction ending = Current;
        Current = null;
        try { ending.End(player, reason); }
        finally { player.RestoreIdleAnimation(); }
    }
}
