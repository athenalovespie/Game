using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameLibrary.Graphics;
using PlayerInventory = First_game.Inventory.Inventory;
using First_game.Inventory;

namespace First_game.World;

public sealed class WorldPickup
{
	private Sprite _sprite;

	public WorldPickup()
	{
		DrawAction = Draw;
	}

	public ItemInstance Item { get; private set; }
	public ItemDefinition Definition { get; private set; }
	public WorldSpawnNode SourceNode { get; private set; }
	public WorldSpawnRule SpawnRule { get; private set; }
	public Point Cell { get; private set; }
	public Vector2 Position { get; private set; }
	public float SortY => _sprite.SortY;
	public bool IsCollected { get; private set; }
	internal Action<SpriteBatch> DrawAction { get; }

	internal void Reset(
		ItemDefinition definition,
		Texture2D icon,
		int count,
		Vector2 position,
		Point cell,
		WorldSpawnNode sourceNode,
		WorldSpawnRule spawnRule)
	{
		Definition = definition;
		Item = new ItemInstance(definition.QualifiedId, count);
		Position = position;
		Cell = cell;
		SourceNode = sourceNode;
		SpawnRule = spawnRule;
		IsCollected = false;

		if (_sprite == null)
			_sprite = new Sprite(icon);
		else
			_sprite.Texture = icon;
		_sprite.Scale = 0.2f;
		_sprite.Position = position;
	}

	public int TryCollect(PlayerInventory inventory, Vector2 playerPosition, Vector2 worldPoint, float pickupDistance)
	{
		if (IsCollected
			|| Vector2.Distance(playerPosition, Position) > pickupDistance
			|| !ContainsPoint(worldPoint))
			return 0;

		int added = inventory.AddItem(Item.QualifiedId, Item.Count, Item.Quality, Item.Durability);
		Item.Count -= added;
		if (Item.Count == 0)
			IsCollected = true;
		return added;
	}

	public void Draw(SpriteBatch spriteBatch)
	{
		if (!IsCollected)
			_sprite.Draw(spriteBatch);
	}

	public bool ContainsPoint(Vector2 worldPoint)
	{
		float halfWidth = _sprite.Texture.Width * _sprite.Scale * 0.5f;
		float halfHeight = _sprite.Texture.Height * _sprite.Scale * 0.5f;
		return worldPoint.X >= Position.X - halfWidth
			&& worldPoint.X <= Position.X + halfWidth
			&& worldPoint.Y >= Position.Y - halfHeight
			&& worldPoint.Y <= Position.Y + halfHeight;
	}
}