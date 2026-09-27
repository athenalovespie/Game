using Microsoft.Xna.Framework;

namespace First_game.World;

public sealed class WorldSpawnNode
{
	public WorldSpawnNode(string nodeType, Vector2 position)
	{
		NodeType = nodeType;
		Position = position;
	}

	public string NodeType { get; }
	public Vector2 Position { get; }
}