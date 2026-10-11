using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace First_game.Input;

// MonoGame has no physics triggers. Movement notifications query only touched
// spatial buckets, then emit enter/exit callbacks; stationary players do no work.
public class SpatialTriggers<T> where T : class, ISpatialTrigger
{
    private const int CellSize = 128;
    private readonly Dictionary<Point, T[]> buckets = new();
    private T[] overlaps;
    private int count;
    private int visit;
    public T[] Doors { get; private set; }
    public int OverlapCount => count;
    public T GetOverlap(int index) => overlaps[index];
    public T Candidate
    {
        get
        {
            for (int i = 0; i < count; i++)
                if (!overlaps[i].SuppressedUntilExit) return overlaps[i];
            return null;
        }
    }

    public event Action<T> Entered;
    public event Action<T> Exited;

    public SpatialTriggers(T[] doors)
    {
        Replace(doors);
    }

    public void Replace(T[] doors)
    {
        Reset();
        buckets.Clear();
        Doors = (T[])doors.Clone();
        overlaps = new T[doors.Length];
        var building = new Dictionary<Point, List<T>>();
        foreach (T door in Doors)
        {
            GetCells(door.TriggerBounds, out Point first, out Point last);
            for (int x = first.X; x <= last.X; x++)
                for (int y = first.Y; y <= last.Y; y++)
                {
                    var cell = new Point(x, y);
                    if (!building.TryGetValue(cell, out List<T> list))
                        building.Add(cell, list = new List<T>());
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
            T door = overlaps[--count];
            overlaps[count] = null;
            door.SuppressedUntilExit = false;
            Exited?.Invoke(door);
        }
    }

    public void Observe(Rectangle bounds, bool spawning = false)
    {
        for (int i = count - 1; i >= 0; i--)
        {
            T door = overlaps[i];
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
                if (!buckets.TryGetValue(new Point(x, y), out T[] nearby)) continue;
                for (int i = 0; i < nearby.Length; i++)
                {
                    T door = nearby[i];
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

    private static bool Overlaps(T door, Rectangle bounds) => door.GroundOnly
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
