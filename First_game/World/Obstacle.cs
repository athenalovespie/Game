using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary.Graphics;
using System;

namespace First_game.World;


public class Obstacle{
    public Rectangle Bounds {get; private set; }

    public Obstacle(Sprite sprite, Vector2 collisionSize, Vector2 collisionOffset){
        float width = collisionSize.X*sprite.Scale;
        float height = collisionSize.Y*sprite.Scale;
        
        Vector2 scaledOffset = collisionOffset*sprite.Scale;
        Vector2 collisionCenter = scaledOffset + sprite.Position;

        Bounds = new Rectangle(
                    (int)(collisionCenter.X - width*0.5f),
                    (int)(collisionCenter.Y - height*0.5f),
                    (int)width,
                    (int)height);
    }
}