using System;

namespace First_game.Harvesting;

/// <summary>Shared tuning, never per-item durability or other mutable state.</summary>
public sealed class ToolDefinition
{
    public string ItemId { get; }
    public string Kind { get; }
    public int Damage { get; }
    public float Range { get; }
    public float SwingSeconds { get; }
    public float ImpactProgress { get; }
    public string LeftAnimation { get; }
    public string RightAnimation { get; }

    public ToolDefinition(string itemId, string kind, int damage, float range,
        float swingSeconds, float impactProgress, string leftAnimation, string rightAnimation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(leftAnimation);
        ArgumentException.ThrowIfNullOrWhiteSpace(rightAnimation);
        if (damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));
        if (!float.IsFinite(range) || range <= 0) throw new ArgumentOutOfRangeException(nameof(range));
        if (!float.IsFinite(swingSeconds) || swingSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(swingSeconds));
        if (!float.IsFinite(impactProgress) || impactProgress <= 0 || impactProgress > 1)
            throw new ArgumentOutOfRangeException(nameof(impactProgress));
        ItemId = itemId;
        Kind = kind;
        Damage = damage;
        Range = range;
        SwingSeconds = swingSeconds;
        ImpactProgress = impactProgress;
        LeftAnimation = leftAnimation;
        RightAnimation = rightAnimation;
    }
}
