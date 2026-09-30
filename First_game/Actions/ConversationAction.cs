using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using First_game.Entities;

namespace First_game.Actions;

/// <summary>Space advances lines; Q cancels. The UI can read CurrentLine.</summary>
public sealed class ConversationAction : PlayerAction
{
    private readonly string[] lines;
    private readonly Vector2 target;
    private readonly float range;
    private readonly Func<bool> available;
    private int index;
    public override string Name => "Talking";
    public string CurrentLine => index < lines.Length ? lines[index] : string.Empty;

    public ConversationAction(IEnumerable<string> lines, Vector2 target, float range = 200f,
        Func<bool> available = null)
    {
        this.lines = new List<string>(lines ?? throw new ArgumentNullException(nameof(lines))).ToArray();
        if (this.lines.Length == 0 || !float.IsFinite(range) || range <= 0)
            throw new ArgumentException("Provide dialogue lines and a positive range.");
        this.target = target;
        this.range = range;
        this.available = available ?? (() => true);
    }

    public override bool CanStart(Player player, out string reason)
    {
        bool valid = available() && Vector2.Distance(player.GroundPosition, target) <= range;
        reason = valid ? null : "Move closer to talk.";
        return valid;
    }
    public override void Begin(Player player)
    {
        player.FaceTowards(target);
        player.RestoreIdleAnimation();
    }
    public override void Update(Player player, GameTime time, ActionInput input)
    {
        if (!available() || (input.ConfirmPressed && ++index >= lines.Length)) Complete();
    }
}
