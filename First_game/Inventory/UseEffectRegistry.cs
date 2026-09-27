using System;
using System.Collections.Generic;

namespace First_game.Inventory;

public enum ItemUseOutcome
{
	Failed,
	Used,
	Consumed
}

public interface IItemUseContext
{
	bool RestoreVitals(float health, float stamina);
	bool ApplyBuff(string buffId, float durationSeconds, float magnitude);
	bool PlaceWorldObject(string objectId);
	bool PerformToolAction(string actionId, ItemInstance tool);
}

public interface IItemUseEffect
{
	ItemUseOutcome Execute(ItemDefinition definition, ItemInstance instance, IItemUseContext context);
}

public sealed class UseEffectRegistry
{
	private readonly Dictionary<string, Func<ItemDefinition, IItemUseEffect>> _factories = new(StringComparer.Ordinal);

	public void Register(string effectId, Func<ItemDefinition, IItemUseEffect> factory)
	{
		if (string.IsNullOrWhiteSpace(effectId))
			throw new ArgumentException("An effect ID is required.", nameof(effectId));
		if (!_factories.TryAdd(effectId, factory ?? throw new ArgumentNullException(nameof(factory))))
			throw new InvalidOperationException($"Use effect '{effectId}' is already registered.");
	}

	public IItemUseEffect Resolve(ItemDefinition definition)
	{
		if (definition.UseEffectId == null)
			return null;
		return Resolve(definition.UseEffectId, definition);
	}

	public IItemUseEffect Resolve(string effectId, ItemDefinition definition)
	{
		if (!_factories.TryGetValue(effectId, out Func<ItemDefinition, IItemUseEffect> factory))
			throw new KeyNotFoundException($"Use effect '{effectId}' for '{definition.QualifiedId}' is not registered.");
		return factory(definition);
	}

	public static UseEffectRegistry CreateBuiltIns()
	{
		var registry = new UseEffectRegistry();
		registry.Register("restore_vitals", definition => new RestoreVitalsEffect(
			definition.EffectAmount,
			definition.EffectSecondaryAmount));
		registry.Register("apply_buff", definition => new ApplyBuffEffect(
			definition.EffectTargetId,
			definition.EffectDurationSeconds,
			definition.EffectAmount));
		registry.Register("place_object", definition => new PlaceObjectEffect(definition.EffectTargetId));
		registry.Register("tool_action", definition => new ToolActionEffect(definition.EffectTargetId));
		registry.Register("weapon_action", definition => new ToolActionEffect(definition.EffectTargetId));
		return registry;
	}

	private sealed class RestoreVitalsEffect : IItemUseEffect
	{
		private readonly float _health;
		private readonly float _stamina;

		public RestoreVitalsEffect(float health, float stamina)
		{
			_health = health;
			_stamina = stamina;
		}

		public ItemUseOutcome Execute(ItemDefinition definition, ItemInstance instance, IItemUseContext context)
		{
			return context != null && context.RestoreVitals(_health, _stamina)
				? ItemUseOutcome.Consumed
				: ItemUseOutcome.Failed;
		}
	}

	private sealed class ApplyBuffEffect : IItemUseEffect
	{
		private readonly string _buffId;
		private readonly float _durationSeconds;
		private readonly float _magnitude;

		public ApplyBuffEffect(string buffId, float durationSeconds, float magnitude)
		{
			_buffId = buffId;
			_durationSeconds = durationSeconds;
			_magnitude = magnitude;
		}

		public ItemUseOutcome Execute(ItemDefinition definition, ItemInstance instance, IItemUseContext context)
		{
			return context != null && context.ApplyBuff(_buffId, _durationSeconds, _magnitude)
				? ItemUseOutcome.Consumed
				: ItemUseOutcome.Failed;
		}
	}

	private sealed class PlaceObjectEffect : IItemUseEffect
	{
		private readonly string _objectId;

		public PlaceObjectEffect(string objectId) => _objectId = objectId;

		public ItemUseOutcome Execute(ItemDefinition definition, ItemInstance instance, IItemUseContext context)
		{
			return context != null && context.PlaceWorldObject(_objectId)
				? ItemUseOutcome.Consumed
				: ItemUseOutcome.Failed;
		}
	}

	private sealed class ToolActionEffect : IItemUseEffect
	{
		private readonly string _actionId;

		public ToolActionEffect(string actionId) => _actionId = actionId;

		public ItemUseOutcome Execute(ItemDefinition definition, ItemInstance instance, IItemUseContext context)
		{
			return context != null && context.PerformToolAction(_actionId, instance)
				? ItemUseOutcome.Used
				: ItemUseOutcome.Failed;
		}
	}
}