using System;
using First_game.Actions;
using First_game.Entities;
using Microsoft.Xna.Framework;

namespace First_game.Harvesting;

/// <summary>One committed swing. Captures its target and tool; never follows the cursor.</summary>
public sealed class HarvestAction : PlayerAction
{
    private readonly ResourceNode target;
    private readonly ToolDefinition tool;
    private readonly Func<bool> isAvailable;
    private float elapsed;
    private bool impacted;
    public override string Name => "Harvesting";
    // Allow cancelling the wind-up, but not skipping recovery after a hit.
    public override bool CanCancel => !impacted;

    public HarvestAction(ResourceNode target, ToolDefinition tool, Func<bool> isAvailable)
    {
        this.target = target ?? throw new ArgumentNullException(nameof(target));
        this.tool = tool ?? throw new ArgumentNullException(nameof(tool));
        this.isAvailable = isAvailable ?? throw new ArgumentNullException(nameof(isAvailable));
    }

    public override bool CanStart(Player player, out string reason)
    {
        reason = !isAvailable() || target.IsDepleted ? "The target or equipped tool is unavailable."
            : !target.Accepts(tool) ? "This resource requires another tool."
            : Vector2.DistanceSquared(player.GroundPosition, target.GroundPosition) > tool.Range * tool.Range
                ? "Move closer to the resource." : null;
        return reason == null;
    }

    public override void Begin(Player player)
    {
        player.FaceTowards(target.GroundPosition);
        // Only side-facing artwork exists; vertical targets use the nearest side.
        string animation = target.GroundPosition.X < player.GroundPosition.X
            ? tool.LeftAnimation : tool.RightAnimation;
        player.PlayActionAnimation(animation, tool.SwingSeconds);
    }

    public override void Update(Player player, GameTime time, ActionInput input)
    {
        elapsed += (float)time.ElapsedGameTime.TotalSeconds;
        if (!impacted && elapsed >= tool.SwingSeconds * tool.ImpactProgress)
        {
            impacted = true; // Mark first, including on a miss or a long frame.
            if (CanStart(player, out _)) target.TryHit(tool);
        }
        if (elapsed >= tool.SwingSeconds) Complete();
    }
}
