using System;
using Microsoft.Xna.Framework;

namespace First_game.Harvesting;

public sealed class ResourceDefinition
{
    public string Id { get; }
    public string RequiredToolKind { get; }
    public int MaxHealth { get; }

    public ResourceDefinition(string id, string requiredToolKind, int maxHealth)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredToolKind);
        if (maxHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maxHealth));
        Id = id;
        RequiredToolKind = requiredToolKind;
        MaxHealth = maxHealth;
    }
}

/// <summary>Resource state without textures, input, inventory, or grid dependencies.</summary>
public sealed class ResourceNode
{
    public ResourceDefinition Definition { get; }
    public Vector2 GroundPosition { get; }
    public int Health { get; private set; }
    public bool IsDepleted => Health == 0;
    public event Action<ResourceNode, int> Hit;
    public event Action<ResourceNode> Depleted;

    public ResourceNode(ResourceDefinition definition, Vector2 groundPosition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        GroundPosition = groundPosition;
        Health = definition.MaxHealth;
    }

    public bool Accepts(ToolDefinition tool) => !IsDepleted
        && tool != null && tool.Kind == Definition.RequiredToolKind;

    public bool TryHit(ToolDefinition tool)
    {
        if (!Accepts(tool)) return false;
        int damage = Math.Min(Health, tool.Damage);
        Health -= damage;
        // Capture the transition before notifying observers; it only occurs once.
        bool depleted = IsDepleted;
        if (depleted) Depleted?.Invoke(this);
        Hit?.Invoke(this, damage);
        return true;
    }
}
