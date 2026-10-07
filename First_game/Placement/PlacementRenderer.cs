using System;
using System.Collections.Generic;
using First_game.Inventory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameLibrary.Graphics;

namespace First_game.Placement;

public sealed class PlacementRenderer : IDisposable
{
    private readonly PlacementWorld world;
    private readonly Texture2D pixel;
    private readonly Dictionary<ItemDefinition, Texture2D> textures = new();
    private ItemDefinition previewDefinition;
    private Texture2D previewTexture;
    public PlacementRenderer(PlacementWorld world, ItemDefinitionRegistry definitions,
        Func<string, Texture2D> loadTexture, Texture2D pixel)
    {
        this.world = world; this.pixel = pixel;
        foreach (var definition in definitions.All)
            if (definition.Placeable != null) textures.Add(definition, loadTexture(definition.Placeable.SpriteAsset));
        world.Added += BindDraw;
        foreach (var obj in world.Objects) BindDraw(obj);
    }
    private void BindDraw(PlacementObject obj)
    {
        Texture2D texture = textures[obj.Definition]; // Resolved once per placed object.
        obj.DrawAction = batch => DrawSprite(batch, obj.Definition.Placeable, texture, obj.OriginTile, obj.Rotation, Color.White);
    }
    public void SubmitDraw(WorldRenderer renderer)
    {
        for (int i = 0; i < world.Objects.Count; i++)
        {
            var obj = world.Objects[i];
            renderer.Submit(world.Grid.CellToWorld(obj.OriginTile).Y + obj.Definition.Placeable.Size(obj.Rotation).Y * world.Grid.CellSize, obj.DrawAction);
        }
    }
    public void DrawPreview(SpriteBatch batch, PlacementController preview)
    {
        if (!preview.IsVisible) return;
        if (!ReferenceEquals(previewDefinition, preview.Definition))
        {
            previewDefinition = preview.Definition; previewTexture = textures[previewDefinition];
        }
        Color color = preview.IsValid ? Color.LimeGreen : Color.Red;
        Point size = preview.Definition.Placeable.Size(preview.Rotation);
        int cellSize = world.Grid.CellSize;
        for (int y = 0; y < size.Y; y++) for (int x = 0; x < size.X; x++)
        {
            Vector2 p = world.Grid.CellToWorld(preview.OriginTile + new Point(x, y));
            var tile = new Rectangle((int)p.X + 1, (int)p.Y + 1, cellSize - 2, cellSize - 2);
            batch.Draw(pixel, tile, color * .22f);
            batch.Draw(pixel, new Rectangle(tile.X, tile.Y, tile.Width, 2), color * .75f);
            batch.Draw(pixel, new Rectangle(tile.X, tile.Y, 2, tile.Height), color * .75f);
            batch.Draw(pixel, new Rectangle(tile.X, tile.Bottom - 2, tile.Width, 2), color * .75f);
            batch.Draw(pixel, new Rectangle(tile.Right - 2, tile.Y, 2, tile.Height), color * .75f);
        }
        DrawSprite(batch, preview.Definition.Placeable, previewTexture, preview.OriginTile, preview.Rotation, color * .5f);
    }
    private void DrawSprite(SpriteBatch batch, PlaceableData data, Texture2D texture, Point origin, int rotation, Color tint)
    {
        Point size = data.Size(rotation);
        Vector2 basePosition = world.Grid.CellToWorld(origin) + new Vector2(size.X * world.Grid.CellSize * .5f, size.Y * world.Grid.CellSize);
        // Rotate about the art's declared ground anchor. The Tent opts out of rotation.
        var artOrigin = new Vector2(texture.Width * .5f + data.GroundOffsetX, texture.Height - data.BaseInsetPixels);
        batch.Draw(texture, basePosition, null, tint, rotation * MathHelper.PiOver2, artOrigin, data.SpriteScale, SpriteEffects.None, 0);
    }
    public void Dispose() => world.Added -= BindDraw;
}
