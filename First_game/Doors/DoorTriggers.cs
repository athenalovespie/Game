using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace First_game.Doors;

// MonoGame has no physics triggers. Movement notifications query only touched
// spatial buckets, then emit enter/exit callbacks; stationary players do no work.
public sealed class DoorTriggers
{
    private const int CellSize = 128;
    private readonly Dictionary<Point, Door[]> buckets = new();
    private Door[] overlaps;
    private int count;
    private int visit;
    public Door[] Doors { get; private set; }
    public Door Candidate
    {
        get
        {
            for (int i = 0; i < count; i++)
                if (!overlaps[i].SuppressedUntilExit) return overlaps[i];
            return null;
        }
    }

    public event Action<Door> Entered;
    public event Action<Door> Exited;

    public DoorTriggers(Door[] doors)
    {
        Replace(doors);
    }

    public void Replace(Door[] doors)
    {
        Reset();
        buckets.Clear();
        Doors = (Door[])doors.Clone();
        overlaps = new Door[doors.Length];
        var building = new Dictionary<Point, List<Door>>();
        foreach (Door door in Doors)
        {
            GetCells(door.TriggerBounds, out Point first, out Point last);
            for (int x = first.X; x <= last.X; x++)
                for (int y = first.Y; y <= last.Y; y++)
                {
                    var cell = new Point(x, y);
                    if (!building.TryGetValue(cell, out List<Door> list))
                        building.Add(cell, list = new List<Door>());
                    list.Add(door);
                }
        }
        foreach (var entry in building)
            buckets.Add(entry.Key, entry.Value.ToArray());
    }

    public void Reset()
    {
        while (count > 0)
        {
            Door door = overlaps[--count];
            overlaps[count] = null;
            door.SuppressedUntilExit = false;
            Exited?.Invoke(door);
        }
    }

    public void Observe(Rectangle bounds, bool spawning = false)
    {
        for (int i = count - 1; i >= 0; i--)
        {
            Door door = overlaps[i];
            if (Overlaps(door, bounds)) continue;
            door.SuppressedUntilExit = false;
            overlaps[i] = overlaps[--count];
            overlaps[count] = null;
            Exited?.Invoke(door);
        }

        // Unsigned wrap is harmless: only doors visited in this call share the stamp.
        visit = unchecked(visit + 1);
        if (visit == 0)
        {
            for (int i = 0; i < Doors.Length; i++) Doors[i].Visit = 0;
            visit = 1;
        }
        GetCells(bounds, out Point first, out Point last);
        for (int x = first.X; x <= last.X; x++)
            for (int y = first.Y; y <= last.Y; y++)
            {
                if (!buckets.TryGetValue(new Point(x, y), out Door[] nearby)) continue;
                for (int i = 0; i < nearby.Length; i++)
                {
                    Door door = nearby[i];
                    if (door.Visit == visit) continue;
                    door.Visit = visit;
                    if (!Overlaps(door, bounds)) continue;
                    bool present = false;
                    for (int j = 0; j < count; j++)
                        if (ReferenceEquals(overlaps[j], door)) { present = true; break; }
                    if (present) continue;
                    door.SuppressedUntilExit = spawning;
                    overlaps[count++] = door;
                    Entered?.Invoke(door);
                }
            }
    }

    private static bool Overlaps(Door door, Rectangle bounds) => door.GroundOnly
        ? door.TriggerBounds.Contains(bounds.Center.X, bounds.Bottom - 1)
        : door.TriggerBounds.Intersects(bounds);

    private static void GetCells(Rectangle bounds, out Point first, out Point last)
    {
        first = new Point((int)MathF.Floor(bounds.Left / (float)CellSize),
            (int)MathF.Floor(bounds.Top / (float)CellSize));
        last = new Point((int)MathF.Floor((bounds.Right - 1) / (float)CellSize),
            (int)MathF.Floor((bounds.Bottom - 1) / (float)CellSize));
    }
}
