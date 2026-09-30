using System;
using Microsoft.Xna.Framework;
using First_game.Entities;

namespace First_game.Actions;

/// <summary>One axe swing, pickaxe strike, or attack. Apply damage in effect, never in Draw.</summary>
public sealed class TimedEffectAction : PlayerAction
{
    private readonly string animation;
    private readonly Vector2 target;
    private readonly float range, duration, effectTime;
    private readonly Func<bool> canInteract;
    private readonly Action effect;
    private float elapsed;
    private bool applied;
    public override string Name { get; }

    public TimedEffectAction(string name, string animation, Vector2 target, float range,
        float duration, float effectTime, Func<bool> canInteract, Action effect)
    {
        if (!float.IsFinite(range) || range <= 0 || !float.IsFinite(duration) || duration <= 0
            || !float.IsFinite(effectTime) || effectTime < 0 || effectTime > duration)
            throw new ArgumentOutOfRangeException(nameof(duration));
        Name = name;
        this.animation = animation;
        this.target = target;
        this.range = range;
        this.duration = duration;
        this.effectTime = effectTime;
        this.canInteract = canInteract ?? throw new ArgumentNullException(nameof(canInteract));
        this.effect = effect ?? throw new ArgumentNullException(nameof(effect));
    }

    public override bool CanStart(Player player, out string reason)
    {
        bool valid = Vector2.Distance(player.GroundPosition, target) <= range && canInteract();
        reason = valid ? null : "Target is unavailable, out of reach, or requires another tool.";
        return valid;
    }

    public override void Begin(Player player)
    {
        player.FaceTowards(target);
        player.PlayActionAnimation(animation);
    }

    public override void Update(Player player, GameTime time, ActionInput input)
    {
        elapsed += (float)time.ElapsedGameTime.TotalSeconds;
        if (!applied && elapsed >= effectTime)
        {
            applied = true; // Crossing the impact time applies once, even after a long frame.
            if (CanStart(player, out _)) effect();
        }
        if (elapsed >= duration) Complete();
    }
}
