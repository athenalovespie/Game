using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using First_game.Entities;
using First_game.Fishing;

namespace First_game.UI;

/// <summary>Draws only; all input, progress and rewards live in actions.</summary>
public sealed class FishingOverlay
{
    private readonly Texture2D pixel;
    public FishingOverlay(Texture2D pixel) { this.pixel = pixel; }

    public void DrawWorld(SpriteBatch batch, Player player, FishingAction action)
    {
        if (action == null) return;
        Vector2 hand = player.GroundPosition + new Vector2(0, -65);
        Vector2 toward = action.Target - hand;
        if (toward.LengthSquared() > 0) toward.Normalize();
        float progress = action.State == FishingState.Casting ? action.CastProgress : 1f;
        Vector2 tip = hand + toward * 65 + new Vector2(0, -35 - MathF.Sin(progress * MathF.PI) * 35);
        Vector2 bobber = Vector2.Lerp(tip, action.Target, progress);
        bobber.Y -= MathF.Sin(progress * MathF.PI) * 90;
        Line(batch, hand, tip, Color.SaddleBrown, 5);
        Line(batch, tip, bobber, Color.White * 0.85f, 1);
        batch.Draw(pixel, new Rectangle((int)bobber.X - 5, (int)bobber.Y - 5, 10, 10), Color.OrangeRed);
    }

    private void Line(SpriteBatch batch, Vector2 from, Vector2 to, Color color, float thickness)
    {
        Vector2 delta = to - from;
        batch.Draw(pixel, from, null, color, MathF.Atan2(delta.Y, delta.X), Vector2.Zero,
            new Vector2(delta.Length(), thickness), SpriteEffects.None, 0);
    }

}
