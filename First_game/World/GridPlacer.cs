using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameLibrary.Graphics;

namespace First_game.World;

public class GridPlacer
{
    private readonly WorldGrid grid;

    public GridPlacer(WorldGrid grid)
    {
        this.grid = grid;
    }

    public bool TryPlaceSprite(
        Texture2D texture,
        Vector2 worldPosition,
        CellType type,
        float scale,
        out Sprite sprite,
        float? groundOffsetY = null,
        float groundOffsetX = 0f)
    {
        sprite = null;

        Point cell = grid.WorldToCell(worldPosition);

        if (!grid.CanPlace(cell) || type == CellType.Empty)
        {
            return false;
        }

        // Distance from the texture's center to the tree's base,
        // measured in original texture pixels.
        // Positive X is right; positive Y is down.
        float baseOffset = groundOffsetY ?? texture.Height / 2f;

        Sprite placedSprite = new Sprite(texture);
        placedSprite.Scale = scale;
        placedSprite.GroundOffsetY = baseOffset;

        placedSprite.Position = grid.CellCenter(cell)
            - new Vector2(groundOffsetX, baseOffset) * scale;

        if (!grid.Occupy(cell, type, placedSprite))
        {
            return false;
        }

        sprite = placedSprite;
        return true;
    }
}
