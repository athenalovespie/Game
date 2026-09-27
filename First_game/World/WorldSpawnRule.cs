using System;

namespace First_game.World;

public sealed class WorldSpawnRule
{
	public WorldSpawnRule(
		string ruleId,
		string nodeType,
		string qualifiedItemId,
		float chance,
		int minimumCount,
		int maximumCount,
		double respawnSeconds)
	{
		if (string.IsNullOrWhiteSpace(ruleId))
			throw new ArgumentException("A rule ID is required.", nameof(ruleId));
		if (string.IsNullOrWhiteSpace(nodeType))
			throw new ArgumentException("A node type is required.", nameof(nodeType));
		if (string.IsNullOrWhiteSpace(qualifiedItemId))
			throw new ArgumentException("A qualified item ID is required.", nameof(qualifiedItemId));
		if (chance < 0f || chance > 1f)
			throw new ArgumentOutOfRangeException(nameof(chance));
		if (minimumCount < 1 || maximumCount < minimumCount)
			throw new ArgumentOutOfRangeException(nameof(minimumCount));
		if (respawnSeconds < 0d)
			throw new ArgumentOutOfRangeException(nameof(respawnSeconds));

		RuleId = ruleId;
		NodeType = nodeType;
		QualifiedItemId = qualifiedItemId;
		Chance = chance;
		MinimumCount = minimumCount;
		MaximumCount = maximumCount;
		RespawnSeconds = respawnSeconds;
	}

	public string RuleId { get; }
	public string NodeType { get; }
	public string QualifiedItemId { get; }
	public float Chance { get; }
	public int MinimumCount { get; }
	public int MaximumCount { get; }
	public double RespawnSeconds { get; }
}