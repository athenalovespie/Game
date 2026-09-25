using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary.Graphics;
using System;

namespace First_game.World;

public class WorldPickup
{
    private Sprite pickupItem;

    public Vector2 Position {get; set; }

    public bool IsCollected {get; private set; }

    public WorldPickup(Texture2D texture, Vector2 initialPosition){
        pickupItem = new Sprite(texture);
        Position = initialPosition;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if(IsCollected == true)
        {
            return;
        }
        pickupItem.Position = Position;
        pickupItem.Draw(spriteBatch);
    }

    public bool IsWithinReach(Vector2 playerPosition, float pickupDistance)
    {
        float distance = Vector2.Distance(playerPosition, Position);
        
        if (distance <= pickupDistance)
        {
            return true;
        }
        else
        {
            return false;
        }

    }

    public void Collect()
    {
        IsCollected = true;      
    }

    public bool ContainsPoint(Vector2 worldPoint)
    {
        float spriteWidth = pickupItem.Texture.Width*pickupItem.Scale;
        float spriteHeight = pickupItem.Texture.Height*pickupItem.Scale;

        float leftBoundary = Position.X - 0.5f*spriteWidth;
        float rightBoundary = Position.X + 0.5f*spriteWidth;
        float topBoundary = Position.Y - 0.5f*spriteHeight;
        float bottomBoundary = Position.Y + 0.5f*spriteHeight;

        if(leftBoundary <= worldPoint.X && worldPoint.X <= rightBoundary && bottomBoundary >= worldPoint.Y && worldPoint.Y >= topBoundary)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}