using Microsoft.Xna.Framework;
using System;

namespace First_game.World;

public enum CellType
{
    Empty,
    Plant,
    Building,
    Pickup
}

public class GridCell
{
    public CellType Type { get; set; }

    public object Occupant { get; set; }

    public bool BlocksMovement { get; set; }

    public GridCell()
    {
        Type = CellType.Empty;
        Occupant = null;
    }

    public bool IsOccupied()
    {
        return Type != CellType.Empty;
    }
}

public class WorldGrid
{
    public int CellSize { get; private set; }

    public Vector2 Origin { get; private set; }

    public int Columns { get; private set; }

    public int Rows { get; private set; }

    private GridCell[,] cells;

    public WorldGrid(
        int cellSize,
        Vector2 origin,
        int columnCount,
        int rowCount)
    {
        if (cellSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cellSize),
                "Cell size must be greater than zero."
            );
        }

        if (rowCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rowCount),
                "Row count must be greater than zero."
            );
        }

        if (columnCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(columnCount),
                "Column count must be greater than zero."
            );
        }

        CellSize = cellSize;
        Origin = origin;
        Columns = columnCount;
        Rows = rowCount;

        cells = new GridCell[Columns, Rows];

        for (int column = 0; column < Columns; column++)
        {
            for (int row = 0; row < Rows; row++)
            {
                cells[column, row] = new GridCell();
            }
        }
    }

    public Point WorldToCell(Vector2 worldPosition)
    {
        int column = (int)MathF.Floor(
            (worldPosition.X - Origin.X) / CellSize
        );

        int row = (int)MathF.Floor(
            (worldPosition.Y - Origin.Y) / CellSize
        );

        return new Point(column, row);
    }

    public Vector2 CellToWorld(Point cell)
    {
        float x = Origin.X + cell.X * CellSize;
        float y = Origin.Y + cell.Y * CellSize;

        return new Vector2(x, y);
    }

    public Vector2 CellCenter(Point cell)
    {
        Vector2 topLeft = CellToWorld(cell);

        return topLeft + new Vector2(
            CellSize / 2f,
            CellSize / 2f
        );
    }

    // Check whether a cell is actually inside the grid
    public bool IsValidCell(Point cell)
    {
        return cell.X >= 0 &&
               cell.X < Columns &&
               cell.Y >= 0 &&
               cell.Y < Rows;
    }

    public GridCell GetCell(Point cell)
    {
        if (!IsValidCell(cell))
        {
            throw new ArgumentOutOfRangeException(
                nameof(cell),
                "Cell is outside the grid."
            );
        }

        return cells[cell.X, cell.Y];
    }

    public bool IsOccupied(Point cell)
    {
        if (!IsValidCell(cell))
        {
            return true;
        }

        return cells[cell.X, cell.Y].IsOccupied();
    }

    public bool CanPlace(Point cell)
    {
        if (!IsValidCell(cell))
        {
            return false;
        }

        return !IsOccupied(cell);
    }

    public bool Occupy(
        Point cell,
        CellType type,
        object occupant = null,
        bool? blocksMovement = null)
    {
        if (!CanPlace(cell))
        {
            return false;
        }

        cells[cell.X, cell.Y].Type = type;
        cells[cell.X, cell.Y].Occupant = occupant;
        cells[cell.X, cell.Y].BlocksMovement = blocksMovement
            ?? (type == CellType.Plant || type == CellType.Building);

        return true;
    }

    public void ClearCell(Point cell)
    {
        if (!IsValidCell(cell))
        {
            return;
        }

        cells[cell.X, cell.Y].Type = CellType.Empty;
        cells[cell.X, cell.Y].Occupant = null;
        cells[cell.X, cell.Y].BlocksMovement = false;
    }

    public bool IntersectsBlockedCell(Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return false;

        Point first = WorldToCell(new Vector2(bounds.Left, bounds.Top));
        // Rectangle's right and bottom edges are exclusive.
        Point last = WorldToCell(new Vector2(bounds.Right - 1, bounds.Bottom - 1));

        // Outside the grid remains walkable.
        for (int x = Math.Max(0, first.X); x <= Math.Min(Columns - 1, last.X); x++)
        {
            for (int y = Math.Max(0, first.Y); y <= Math.Min(Rows - 1, last.Y); y++)
            {
                if (cells[x, y].BlocksMovement)
                    return true;
            }
        }

        return false;
    }

    public bool CanPlaceArea(
        Point startingCell,
        int width,
        int height)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Point cell = new Point(
                    startingCell.X + x,
                    startingCell.Y + y
                );

                if (!CanPlace(cell))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public bool OccupyArea(
        Point startingCell,
        int width,
        int height,
        CellType type,
        object occupant = null,
        bool? blocksMovement = null)
    {
        if (!CanPlaceArea(startingCell, width, height))
        {
            return false;
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Point cell = new Point(
                    startingCell.X + x,
                    startingCell.Y + y
                );

                cells[cell.X, cell.Y].Type = type;
                cells[cell.X, cell.Y].Occupant = occupant;
                cells[cell.X, cell.Y].BlocksMovement = blocksMovement
                    ?? (type == CellType.Plant || type == CellType.Building);
            }
        }

        return true;
    }

    public bool CanPlaceFootprint(Point startingCell, Point[] footprint)
    {
    if (footprint == null || footprint.Length == 0)
        return false;

    foreach (Point offset in footprint)
    {
        Point cell = new Point(
            startingCell.X + offset.X,
            startingCell.Y + offset.Y);

        if (!CanPlace(cell))
            return false;
    }

    return true;
    }

public bool OccupyFootprint(
    Point startingCell,
    Point[] footprint,
    CellType type,
    object occupant,
    bool blocksMovement)
    {
    if (type == CellType.Empty
        || !CanPlaceFootprint(startingCell, footprint))
    {
        return false;
    }

    // Check the entire shape before reserving any cells.
    foreach (Point offset in footprint)
    {
        Point cell = new Point(
            startingCell.X + offset.X,
            startingCell.Y + offset.Y);

        GridCell gridCell = GetCell(cell);
        gridCell.Type = type;
        gridCell.Occupant = occupant;
        gridCell.BlocksMovement = blocksMovement;
    }

    return true;
    }
}
