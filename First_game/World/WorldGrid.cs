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
        object occupant = null)
    {
        if (!CanPlace(cell))
        {
            return false;
        }

        cells[cell.X, cell.Y].Type = type;
        cells[cell.X, cell.Y].Occupant = occupant;

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
        object occupant = null)
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
            }
        }

        return true;
    }
}