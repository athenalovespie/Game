using System;
using System.Collections;
using System.Collections.Generic;
using First_game.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameLibrary.Graphics;

namespace First_game.Harvesting;

/// <summary>Owns placed resources, selection, rendering, and removal of their grid occupancy.</summary>
public sealed class ResourceWorld
{
    private sealed record Placement(ResourceNode Node, Sprite Sprite, Point Cell);
    private readonly WorldGrid grid;
    private readonly List<Placement> placements = new();
    private readonly Dictionary<Texture2D, BitArray> opaquePixels = new();
    public int Count => placements.Count;
    public event Action<ResourceNode> Depleted;

    public ResourceWorld(WorldGrid grid) => this.grid = grid ?? throw new ArgumentNullException(nameof(grid));

    // Adopts a one-cell plant already placed by GridPlacer. The sprite owns that cell.
    public ResourceNode Register(Sprite sprite, Point cell, ResourceDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(sprite);
        if (!grid.IsValidCell(cell) || !ReferenceEquals(grid.GetCell(cell).Occupant, sprite))
            throw new ArgumentException("The resource must own its grid cell.", nameof(cell));
        if (placements.Exists(p => p.Cell == cell))
            throw new InvalidOperationException("Resource cell already registered.");
        Vector2 ground = grid.CellToWorld(cell) + new Vector2(grid.CellSize / 2f, grid.CellSize);
        var node = new ResourceNode(definition, ground);
        placements.Add(new Placement(node, sprite, cell));
        node.Depleted += OnDepleted;
        return node;
    }

    public bool Contains(ResourceNode node) => placements.Exists(p => ReferenceEquals(p.Node, node));

    public ResourceNode FindAt(Vector2 point)
    {
        Placement selected = null;
        foreach (Placement placement in placements)
        {
            if (placement.Node.IsDepleted || !HitTest(placement.Sprite, point)) continue;
            // Matches painter order: frontmost resource wins; later insertion wins ties.
            if (selected == null || placement.Sprite.SortY >= selected.Sprite.SortY)
                selected = placement;
        }
        return selected?.Node;
    }

    private bool HitTest(Sprite sprite, Vector2 point)
    {
        Rectangle source = sprite.SourceRectangle
            ?? new Rectangle(0, 0, sprite.Texture.Width, sprite.Texture.Height);
        if (sprite.Scale <= 0) return false;
        Vector2 local = (point - sprite.Position) / sprite.Scale
            + new Vector2(source.Width, source.Height) * .5f;
        if (local.X < 0 || local.Y < 0 || local.X >= source.Width || local.Y >= source.Height)
            return false;
        // Textureless rectangles also support logic-only world tests.
        if (sprite.Texture == null) return true;
        if (!opaquePixels.TryGetValue(sprite.Texture, out BitArray mask))
        {
            var pixels = new Color[sprite.Texture.Width * sprite.Texture.Height];
            sprite.Texture.GetData(pixels);
            mask = new BitArray(pixels.Length);
            for (int i = 0; i < pixels.Length; i++) mask[i] = pixels[i].A > 16;
            opaquePixels.Add(sprite.Texture, mask);
        }
        return mask[((int)local.Y + source.Y) * sprite.Texture.Width + (int)local.X + source.X];
    }

    public void SubmitDraw(WorldRenderer renderer)
    {
        foreach (Placement placement in placements)
            renderer.Submit(placement.Sprite.SortY, placement.Sprite.Draw);
    }

    public bool Remove(ResourceNode node)
    {
        int index = placements.FindIndex(p => ReferenceEquals(p.Node, node));
        if (index < 0) return false;
        Placement placement = placements[index];
        placements.RemoveAt(index);
        node.Depleted -= OnDepleted;
        // Do not release occupancy that another system has reassigned.
        if (ReferenceEquals(grid.GetCell(placement.Cell).Occupant, placement.Sprite))
            grid.ClearCell(placement.Cell);
        return true;
    }

    private void OnDepleted(ResourceNode node)
    {
        if (Remove(node)) Depleted?.Invoke(node);
    }
}
