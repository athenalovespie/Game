using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGameLibrary.Graphics;

public class WorldRenderer
{
    private readonly List<(float SortY, int Order, Action<SpriteBatch> Draw)> _entries = new();

    public void Submit(float sortY, Action<SpriteBatch> draw)
    {
        _entries.Add((sortY, _entries.Count, draw));
    }

    // Call inside a begun SpriteBatch using its default Deferred sort mode.
    public void Draw(SpriteBatch spriteBatch)
    {
        _entries.Sort((a, b) =>
        {
            int comparison = a.SortY.CompareTo(b.SortY);
            return comparison != 0 ? comparison : a.Order.CompareTo(b.Order);
        });

        try
        {
            foreach (var entry in _entries)
                entry.Draw(spriteBatch);
        }
        finally
        {
            _entries.Clear();
        }
    }
}
