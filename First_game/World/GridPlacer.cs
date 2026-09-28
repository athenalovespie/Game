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
        float groundOffsetX = 0f,
        bool? blocksMovement = null)
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

        // Anchor the base at the cell's bottom edge so the blocked space
        // extends behind the object instead of in front of it.
        Vector2 basePosition = grid.CellToWorld(cell)
            + new Vector2(grid.CellSize / 2f, grid.CellSize);
        placedSprite.Position = basePosition
            - new Vector2(groundOffsetX, baseOffset) * scale;

        if (!grid.Occupy(cell, type, placedSprite, blocksMovement))
        {
            return false;
        }

        sprite = placedSprite;
        return true;
    }

    public bool TryPlaceBuilding(
        Texture2D texture,
        Vector2 worldPosition,
        int widthInCells,
        int heightInCells,
        float scale,
        out Sprite sprite,
        float? groundOffsetY = null,
        float groundOffsetX = 0f)
    {
        sprite = null;

        // A building must occupy at least one cell.
        if (widthInCells <= 0 || heightInCells <= 0)
        {
            return false;
        }

        // The supplied position chooses the footprint's top-left cell.
        Point startingCell = grid.WorldToCell(worldPosition);

        if (!grid.CanPlaceArea(
            startingCell, widthInCells, heightInCells))
        {
            return false;
        }

        // Find the bottom center of the entire footprint.
        Vector2 topLeft = grid.CellToWorld(startingCell);

        Vector2 basePosition = topLeft + new Vector2(
            widthInCells * grid.CellSize / 2f,
            heightInCells * grid.CellSize);

        // Measure the building's base from the texture's center.
        float baseOffsetY = groundOffsetY ?? texture.Height / 2f;

        Sprite building = new Sprite(texture);
        building.Scale = scale;
        building.GroundOffsetY = baseOffsetY;

        building.Position = basePosition
            - new Vector2(groundOffsetX, baseOffsetY) * scale;

        // Reserve every cell and make them block movement.
        if (!grid.OccupyArea(
            startingCell,
            widthInCells,
            heightInCells,
            CellType.Building,
            building,
            blocksMovement: true))
        {
            return false;
        }

        sprite = building;
        return true;

    }

    public bool TryPlaceFootprint(
        Texture2D texture,
        Vector2 worldPosition,
        Point[] footprint,
        Vector2 anchorInCells,
        float scale,
        out PlacedObject placedObject,
        float? groundOffsetY = null,
        float groundOffsetX = 0f)
    {
        placedObject = null;

        Point startingCell = grid.WorldToCell(worldPosition);

        if (!grid.CanPlaceFootprint(startingCell, footprint))
            return false;

        Vector2 anchorPosition = grid.CellToWorld(startingCell)
            + anchorInCells * grid.CellSize;

        float baseOffsetY = groundOffsetY ?? texture.Height / 2f;

        Sprite placedSprite = new Sprite(texture);
        placedSprite.Scale = scale;
        placedSprite.GroundOffsetY = baseOffsetY;
        placedSprite.Position = anchorPosition
            - new Vector2(groundOffsetX, baseOffsetY) * scale;

        PlacedObject placement = new PlacedObject(
            placedSprite, startingCell, footprint);

        if (!grid.OccupyFootprint(
            startingCell,
            footprint,
            CellType.Building,
            placement,
            blocksMovement: true))
        {
            return false;
        }

        placedObject = placement;
        return true;
    }

    public void Remove(PlacedObject placedObject)
    {
        if (placedObject == null)
            return;

        foreach (Point cell in placedObject.GetOccupiedCells())
        {
            if (!grid.IsValidCell(cell))
                continue;

            // Never clear a cell that has been reassigned to another object.
            if (object.ReferenceEquals(grid.GetCell(cell).Occupant, placedObject))
                grid.ClearCell(cell);
        }
    }

    public static Point[] CreateFootprint(params string[] rows)
    {
    var cells = new System.Collections.Generic.List<Point>();

    for (int row = 0; row < rows.Length; row++)
    {
        for (int column = 0; column < rows[row].Length; column++)
        {
            if (rows[row][column] == 'X')
            {
                cells.Add(new Point(column, row));
            }
        }
    }

    return cells.ToArray();
    }
}
