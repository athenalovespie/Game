using System;
using System.Collections.Generic;
using First_game.Inventory;
using First_game.Placement;
using Microsoft.Xna.Framework.Graphics;

namespace First_game.Regrowing;

public sealed record HarvestableState(string Id, string SpriteAsset);
public sealed record HarvestYield(string QualifiedItemId, int Minimum, int Maximum, float BonusChance = 0, int BonusQuantity = 0)
{
    public int Roll(Random random) => random.Next(Minimum, Maximum + 1)
        + (BonusChance > 0 && random.NextDouble() < BonusChance ? BonusQuantity : 0);
}

/// <summary>Immutable cached data. State IDs are resolved to indexes once during registration.</summary>
public sealed class HarvestableData
{
    private readonly HarvestableState[] states;
    public string Id { get; }
    public PlaceableData Footprint { get; }
    public HarvestYield Yield { get; }
    public int RipeState { get; }
    public int EmptyState { get; }
    public double RegrowSeconds { get; }
    public string PromptText { get; }
    public int RangePixels { get; }
    public int StateCount => states.Length;
    public HarvestableState State(int index) => states[index];
    public int StateIndex(string id)
    {
        for (int i = 0; i < states.Length; i++) if (states[i].Id == id) return i;
        throw new InvalidOperationException("Unknown harvestable state: " + id);
    }
    public HarvestableData(string id, PlaceableData footprint, HarvestableState[] states, HarvestYield yield,
        string ripeState, string afterHarvestState, double regrowSeconds, string promptText, int rangePixels = 140)
    {
        if (string.IsNullOrWhiteSpace(id) || footprint == null || states == null || states.Length < 2 || yield == null
            || yield.Minimum < 1 || yield.Maximum < yield.Minimum || yield.Maximum == int.MaxValue
            || yield.BonusQuantity < 0 || (long)yield.Maximum + yield.BonusQuantity > int.MaxValue
            || !float.IsFinite(yield.BonusChance) || yield.BonusChance < 0 || yield.BonusChance > 1
            || !double.IsFinite(regrowSeconds) || regrowSeconds <= 0 || rangePixels < 1 || rangePixels > 1000
            || string.IsNullOrWhiteSpace(promptText) || footprint.AllowOverlap || footprint.Enterable != null)
            throw new ArgumentException("Invalid regrowing resource definition.");
        this.states = (HarvestableState[])states.Clone();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var state in states)
            if (state == null || string.IsNullOrWhiteSpace(state.Id) || string.IsNullOrWhiteSpace(state.SpriteAsset) || !ids.Add(state.Id))
                throw new ArgumentException("Resource states require unique IDs and sprites.");
        Id = id; Footprint = footprint; Yield = yield; RegrowSeconds = regrowSeconds;
        PromptText = promptText; RangePixels = rangePixels;
        RipeState = StateIndex(ripeState); EmptyState = StateIndex(afterHarvestState);
        if (RipeState == EmptyState) throw new ArgumentException("Harvest and regrow must have distinct states.");
    }
}

public sealed class HarvestableRegistry
{
    public sealed class Resolved
    {
        public HarvestableData Data { get; }
        public ItemDefinition YieldItem { get; }
        public Texture2D[] Textures { get; }
        internal Resolved(HarvestableData data, ItemDefinition item, Texture2D[] textures)
        { Data = data; YieldItem = item; Textures = textures; }
    }
    private readonly Dictionary<string, Resolved> definitions = new(StringComparer.Ordinal);
    private readonly ItemDefinitionRegistry items;
    private readonly Func<string, Texture2D> loadTexture;
    public HarvestableRegistry(ItemDefinitionRegistry items, Func<string, Texture2D> loadTexture)
    { this.items = items; this.loadTexture = loadTexture; }
    public void Register(HarvestableData data)
    {
        var item = items.GetRequired(data.Yield.QualifiedItemId);
        var textures = new Texture2D[data.StateCount];
        for (int i = 0; i < textures.Length; i++) textures[i] = loadTexture(data.State(i).SpriteAsset);
        definitions.Add(data.Id, new Resolved(data, item, textures));
    }
    public Resolved GetRequired(string id) => definitions.TryGetValue(id, out var value)
        ? value : throw new InvalidOperationException("Unknown harvestable type: " + id);
}
