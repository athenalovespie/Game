using System;
using First_game.Input;
using First_game.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameLibrary.Graphics;

namespace First_game.Regrowing;

public sealed class HarvestableInstance : ISpatialTrigger
{
    internal readonly HarvestableRegistry.Resolved Definition;
    internal WorldSpawnRule SpawnRule;
    internal Vector2 NodePosition;
    internal double? SpawnAt;
    internal int PendingYield;
    public string Id { get; }
    public string MapId { get; }
    public HarvestableData Data => Definition.Data;
    public Point Tile { get; internal set; }
    public bool IsSpawned { get; internal set; }
    public int StateIndex { get; private set; }
    public string StateId => Data.State(StateIndex).Id;
    public bool IsRipe => IsSpawned && StateIndex == Data.RipeState;
    public double? RegrowAt { get; internal set; }
    public Rectangle FootprintBounds { get; internal set; }
    public Rectangle TriggerBounds { get; internal set; }
    public bool GroundOnly => true;
    public bool SuppressedUntilExit { get; set; }
    public int Visit { get; set; }
    public Sprite Sprite { get; }
    internal Action<SpriteBatch> DrawAction { get; }
    internal string Prompt;
    internal HarvestableInstance(string id, string mapId, HarvestableRegistry.Resolved definition)
    {
        Id = id; MapId = mapId; Definition = definition;
        Sprite = new Sprite(null) { Scale = definition.Data.Footprint.SpriteScale };
        DrawAction = Sprite.Draw;
        SetState(Data.RipeState);
    }
    internal void SetState(int state)
    {
        StateIndex = state; Sprite.Texture = Definition.Textures[state];
        // Keep the footprint's bottom-center ground anchor fixed across differing sprite dimensions.
        float baseOffset = (Sprite.Texture?.Height ?? 100) / 2f - Data.Footprint.BaseInsetPixels;
        Sprite.GroundOffsetY = baseOffset;
        Sprite.Position = new Vector2(FootprintBounds.Center.X, FootprintBounds.Bottom)
            - new Vector2(Data.Footprint.GroundOffsetX, baseOffset) * Sprite.Scale;
    }
    public bool InRange(Vector2 ground)
    {
        float x = MathHelper.Clamp(ground.X, FootprintBounds.Left, FootprintBounds.Right);
        float y = MathHelper.Clamp(ground.Y, FootprintBounds.Top, FootprintBounds.Bottom);
        return IsSpawned && Vector2.DistanceSquared(ground, new Vector2(x, y)) <= Data.RangePixels * Data.RangePixels;
    }
}
