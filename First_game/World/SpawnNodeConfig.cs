using System;
using Microsoft.Xna.Framework;

namespace First_game.World;

public sealed class SpawnNodeConfig
{
	public SpawnNodeConfig(string nodeType, int count, Vector2 minimumPosition, Vector2 maximumPosition)
	{
		if (string.IsNullOrWhiteSpace(nodeType))
			throw new ArgumentException("A node type is required.", nameof(nodeType));
		if (count < 1)
			throw new ArgumentOutOfRangeException(nameof(count));
		if (minimumPosition.X > maximumPosition.X || minimumPosition.Y > maximumPosition.Y)
			throw new ArgumentException("Minimum node position must be less than or equal to maximum position.");

		NodeType = nodeType;
		Count = count;
		MinimumPosition = minimumPosition;
		MaximumPosition = maximumPosition;
	}

	public string NodeType { get; }
	public int Count { get; }
	public Vector2 MinimumPosition { get; }
	public Vector2 MaximumPosition { get; }
}