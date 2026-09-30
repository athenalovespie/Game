using Microsoft.Xna.Framework;
using First_game.Entities;

namespace First_game.Actions;

public enum ActionEndReason { Completed, Cancelled }

/// <summary>A fresh instance represents one attempt. Keep gameplay rules in subclasses.</summary>
public abstract class PlayerAction
{
    public abstract string Name { get; }
    public virtual bool BlocksMovement => true;
    public virtual bool CanCancel => true;
    public bool IsComplete { get; private set; }
    internal bool HasStarted { get; set; }
    public virtual bool CanStart(Player player, out string reason)
    {
        reason = null;
        return true;
    }
    public virtual void Begin(Player player) { }
    public abstract void Update(Player player, GameTime time, ActionInput input);
    public virtual void End(Player player, ActionEndReason reason) { }
    protected void Complete() => IsComplete = true;
}
