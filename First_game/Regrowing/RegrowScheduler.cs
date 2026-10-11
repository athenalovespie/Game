using System;
using System.Collections.Generic;

namespace First_game.Regrowing;

/// <summary>At most one deadline per instance per queue; capacity reserved when nodes register/load.</summary>
public sealed class RegrowScheduler
{
    private readonly PriorityQueue<HarvestableInstance, double> queue = new();
    public int Count => queue.Count;
    public void Reserve(int capacity) => queue.EnsureCapacity(capacity);
    public void Schedule(HarvestableInstance instance, double at)
    {
        if (!double.IsFinite(at) || at < 0) throw new ArgumentOutOfRangeException(nameof(at));
        queue.Enqueue(instance, at);
    }
    public bool TryTakeDue(double now, out HarvestableInstance instance)
    {
        if (queue.TryPeek(out instance, out double at) && at <= now) { queue.Dequeue(); return true; }
        instance = null; return false;
    }
    public void Clear() => queue.Clear();
    public static double Deadline(double now, double duration)
    {
        double at = now + duration;
        if (!double.IsFinite(now) || now < 0 || !double.IsFinite(duration) || duration <= 0 || !double.IsFinite(at))
            throw new ArgumentOutOfRangeException(nameof(now));
        return at;
    }
}
