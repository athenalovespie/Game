using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using First_game.Inventory;
using MonoGameLibrary.Graphics;
using PlayerInventory = First_game.Inventory.Inventory;

namespace First_game.World;

public sealed class WorldPickupSystem
{
	private const int PlacementAttempts = 16;
	private const int PlacementRadius = 2;
	private readonly WorldGrid _grid;
	private readonly ItemDefinitionRegistry _items;
	private readonly WorldSpawnRuleRegistry _rules;
	private readonly Random _random;
	private readonly Func<string, Texture2D> _loadTexture;
	private readonly Dictionary<string, Texture2D> _icons = new(StringComparer.Ordinal);
	private readonly List<WorldPickup> _activePickups = new();
	private readonly Stack<WorldPickup> _pickupPool = new();
	private readonly PriorityQueue<RespawnRequest, double> _respawns = new();

	public WorldPickupSystem(
		WorldGrid grid,
		ItemDefinitionRegistry items,
		WorldSpawnRuleRegistry rules,
		Func<string, Texture2D> loadTexture,
		Random random)
	{
		_grid = grid ?? throw new ArgumentNullException(nameof(grid));
		_items = items ?? throw new ArgumentNullException(nameof(items));
		_rules = rules ?? throw new ArgumentNullException(nameof(rules));
		_loadTexture = loadTexture ?? throw new ArgumentNullException(nameof(loadTexture));
		_random = random ?? throw new ArgumentNullException(nameof(random));
	}

	public void RegisterNodes(IEnumerable<SpawnNodeConfig> configurations)
	{
		foreach (SpawnNodeConfig configuration in configurations)
		{
			if (_rules.GetForNode(configuration.NodeType).Count == 0)
				throw new InvalidOperationException($"No spawn rules are registered for node type '{configuration.NodeType}'.");

			for (int nodeIndex = 0; nodeIndex < configuration.Count; nodeIndex++)
			{
				var node = new WorldSpawnNode(
					configuration.NodeType,
					new Vector2(
						RandomBetween(configuration.MinimumPosition.X, configuration.MaximumPosition.X),
						RandomBetween(configuration.MinimumPosition.Y, configuration.MaximumPosition.Y)));
				SpawnNode(node);
			}
		}
	}

	public void Update(GameTime gameTime)
	{
		double now = gameTime.TotalGameTime.TotalSeconds;
		while (_respawns.TryPeek(out _, out double dueTime) && dueTime <= now)
		{
			RespawnRequest request = _respawns.Dequeue();
			SpawnRuleAtNode(request.Node, request.Rule, now);
		}
	}

	public bool TryCollectAt(
		Vector2 playerPosition,
		Vector2 worldPoint,
		PlayerInventory inventory,
		float pickupDistance,
		double nowSeconds)
	{
		for (int pickupIndex = _activePickups.Count - 1; pickupIndex >= 0; pickupIndex--)
		{
			WorldPickup pickup = _activePickups[pickupIndex];
			if (pickup.TryCollect(inventory, playerPosition, worldPoint, pickupDistance) == 0)
				continue;

			if (!pickup.IsCollected)
				return true;

			_grid.ClearCell(pickup.Cell);
			_activePickups.RemoveAt(pickupIndex);
			if (pickup.SpawnRule.RespawnSeconds > 0d)
				ScheduleRespawn(pickup.SourceNode, pickup.SpawnRule, pickup.SpawnRule.RespawnSeconds, nowSeconds);
			_pickupPool.Push(pickup);
			return true;
		}
		return false;
	}

	public void SubmitDraw(WorldRenderer renderer)
	{
		for (int pickupIndex = 0; pickupIndex < _activePickups.Count; pickupIndex++)
		{
			WorldPickup pickup = _activePickups[pickupIndex];
			renderer.Submit(pickup.SortY, pickup.DrawAction);
		}
	}

    public PickupSnapshot Capture()
    {
        var snapshot = new PickupSnapshot();
        foreach (var pickup in _activePickups)
            snapshot.Active.Add(new PickupSaveEntry { RuleId = pickup.SpawnRule.RuleId,
                NodeX = pickup.SourceNode.Position.X, NodeY = pickup.SourceNode.Position.Y,
                TileX = pickup.Cell.X, TileY = pickup.Cell.Y, Count = pickup.Item.Count });
        foreach (var pending in _respawns.UnorderedItems)
            snapshot.Pending.Add(new PickupSaveEntry { RuleId = pending.Element.Rule.RuleId,
                NodeX = pending.Element.Node.Position.X, NodeY = pending.Element.Node.Position.Y, Due = pending.Priority });
        return snapshot;
    }

    public void Clear()
    {
        foreach (var pickup in _activePickups)
        {
            if (ReferenceEquals(_grid.GetCell(pickup.Cell).Occupant, pickup)) _grid.ClearCell(pickup.Cell);
            _pickupPool.Push(pickup);
        }
        _activePickups.Clear(); _respawns.Clear();
    }

    public void Restore(PickupSnapshot snapshot)
    {
        if (snapshot?.Active == null || snapshot.Pending == null) throw new InvalidOperationException("Invalid pickup snapshot.");
        var occupied = new HashSet<Point>();
        foreach (var entry in snapshot.Active)
        {
            ValidateEntry(entry);
            var rule = _rules.GetRequired(entry.RuleId);
            Point tile = new(entry.TileX, entry.TileY);
            if (entry.Count < 1 || entry.Count > _items.GetRequired(rule.QualifiedItemId).MaxStackSize
                || !_grid.IsValidCell(tile) || !occupied.Add(tile)
                || (_grid.IsOccupied(tile) && !(_grid.GetCell(tile).Occupant is WorldPickup)))
                throw new InvalidOperationException("Invalid saved pickup location or count.");
        }
        foreach (var entry in snapshot.Pending) ValidateEntry(entry);
        Clear();
        foreach (var entry in snapshot.Active)
        {
            var rule = _rules.GetRequired(entry.RuleId);
            var definition = _items.GetRequired(rule.QualifiedItemId);
            Point tile = new(entry.TileX, entry.TileY);
            var pickup = _pickupPool.Count == 0 ? new WorldPickup() : _pickupPool.Pop();
            pickup.Reset(definition, GetIcon(definition), entry.Count, _grid.CellCenter(tile), tile,
                new WorldSpawnNode(rule.NodeType, new Vector2(entry.NodeX, entry.NodeY)), rule);
            _grid.Occupy(tile, CellType.Pickup, pickup, false); _activePickups.Add(pickup);
        }
        foreach (var entry in snapshot.Pending)
        {
            var rule = _rules.GetRequired(entry.RuleId);
            _respawns.Enqueue(new RespawnRequest(new WorldSpawnNode(rule.NodeType, new Vector2(entry.NodeX, entry.NodeY)), rule), entry.Due);
        }
    }
    private void ValidateEntry(PickupSaveEntry entry)
    {
        if (entry == null || entry.RuleId == null || !float.IsFinite(entry.NodeX) || !float.IsFinite(entry.NodeY)
            || !double.IsFinite(entry.Due) || entry.Due < 0) throw new InvalidOperationException("Invalid pickup snapshot entry.");
        _rules.GetRequired(entry.RuleId);
    }

	private void SpawnNode(WorldSpawnNode node)
	{
		IReadOnlyList<WorldSpawnRule> rules = _rules.GetForNode(node.NodeType);
		for (int ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
			SpawnRuleAtNode(node, rules[ruleIndex], 0d);
	}

	private bool SpawnRuleAtNode(WorldSpawnNode node, WorldSpawnRule rule, double now)
	{
		if (_random.NextDouble() >= rule.Chance)
		{
			ScheduleRespawn(node, rule, rule.RespawnSeconds, now);
			return false;
		}

		Point origin = _grid.WorldToCell(node.Position);
		for (int attempt = 0; attempt < PlacementAttempts; attempt++)
		{
			Point cell = attempt == 0 ? origin : new Point(
				origin.X + _random.Next(-PlacementRadius, PlacementRadius + 1),
				origin.Y + _random.Next(-PlacementRadius, PlacementRadius + 1));
			if (!_grid.CanPlace(cell))
				continue;

			ItemDefinition definition = _items.GetRequired(rule.QualifiedItemId);
			Texture2D icon = GetIcon(definition);
			int count = _random.Next(rule.MinimumCount, rule.MaximumCount + 1);
			WorldPickup pickup = _pickupPool.Count == 0 ? new WorldPickup() : _pickupPool.Pop();
			pickup.Reset(definition, icon, count, _grid.CellCenter(cell), cell, node, rule);
			if (!_grid.Occupy(cell, CellType.Pickup, pickup, blocksMovement: false))
			{
				_pickupPool.Push(pickup);
				continue;
			}

			_activePickups.Add(pickup);
			return true;
		}

		ScheduleRespawn(node, rule, rule.RespawnSeconds, now);
		return false;
	}

	private Texture2D GetIcon(ItemDefinition definition)
	{
		if (string.IsNullOrWhiteSpace(definition.IconAsset))
			throw new InvalidOperationException($"Item '{definition.QualifiedId}' has no world icon asset.");
		if (!_icons.TryGetValue(definition.IconAsset, out Texture2D icon))
			_icons.Add(definition.IconAsset, icon = _loadTexture(definition.IconAsset));
		return icon;
	}

	private void ScheduleRespawn(WorldSpawnNode node, WorldSpawnRule rule, double delaySeconds, double now)
	{
		if (delaySeconds > 0d)
			_respawns.Enqueue(new RespawnRequest(node, rule), now + delaySeconds);
	}

	private float RandomBetween(float minimum, float maximum)
	{
		return minimum + (float)_random.NextDouble() * (maximum - minimum);
	}

	private readonly record struct RespawnRequest(WorldSpawnNode Node, WorldSpawnRule Rule);
}