using System;
using Microsoft.Xna.Framework.Graphics;

namespace First_game.Inventory;

public abstract class Item
{
	protected Item(string id, string name, int maxStack, Texture2D icon)
	{
		if (string.IsNullOrWhiteSpace(id))
			throw new ArgumentException("An item ID is required.", nameof(id));
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("An item name is required.", nameof(name));
		if (maxStack < 1)
			throw new ArgumentOutOfRangeException(nameof(maxStack));

		Id = id;
		Name = name;
		MaxStack = maxStack;
		Icon = icon ?? throw new ArgumentNullException(nameof(icon));
	}

	public string Id { get; }
	public string Name { get; }
	public int MaxStack { get; }
	public Texture2D Icon { get; }
}