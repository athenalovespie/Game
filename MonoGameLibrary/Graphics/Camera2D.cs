using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary.Graphics;
using System;

namespace MonoGameLibrary.Graphics;
//change this so you can see more in front of you!
public class Camera2D
{
    public Vector2 Position {get; set; } 

    public Vector2 TargetPosition {get; set; }

    public Vector2 DeadZoneSize {get; set; } = new Vector2(300.0f, 180.0f);

    public float FollowSharpness = 8f;

    public void UpdateTarget(Vector2 playerPosition)
    {
        float left = TargetPosition.X - 0.5f*DeadZoneSize.X;

        float right = TargetPosition.X + 0.5f*DeadZoneSize.X;

        float top = TargetPosition.Y - 0.5f*DeadZoneSize.Y;

        float bottom = TargetPosition.Y + 0.5f*DeadZoneSize.Y;

        if (playerPosition.X < left)
        {
            float offset = playerPosition.X - left;
            //needs this to be a vector since TargetPosition is  Vector
            TargetPosition += new Vector2(offset, 0f);
        }
        else if(playerPosition.X > right)
        {
            float offset = playerPosition.X - right;

            TargetPosition += new Vector2(offset, 0f);
        }
        if (playerPosition.Y < top)
        {
            float offset = playerPosition.Y - top;
            //needs this to be a vector since TargetPosition is  Vector
            TargetPosition += new Vector2(0f, offset);
        }
        else if(playerPosition.Y > bottom)
        {
            float offset = playerPosition.Y - bottom;

            TargetPosition += new Vector2(0f, offset);
        }
    }

    public void Update(GameTime gameTime)
    {
        float seconds = (float)gameTime.ElapsedGameTime.TotalSeconds;

        float followFraction = 1 - MathF.Exp(-FollowSharpness*seconds);

        Vector2 displacement = TargetPosition - Position;

        Position += displacement*followFraction;
    }

    public Matrix GetTransform(int viewportWidth, int viewportHeight)
    {
        float halfWidth = 0.5f*viewportWidth;
        float halfHeight = 0.5f*viewportHeight;

        float horizShift = halfWidth - Position.X;
        float vertShift =  halfHeight - Position.Y;

        Matrix Translation = Matrix.CreateTranslation(horizShift, vertShift, 0f);

        return Translation;
    }

    public Camera2D(Vector2 startingPosition)
    {
        Position = startingPosition;
        TargetPosition = startingPosition;
    }

    public Vector2 ScreenToWorld(Vector2 screenPosition, int viewportWidth, int viewportHeight)
    {
        float halfWidth = 0.5f*viewportWidth;
        float halfHeight = 0.5f*viewportHeight;

        Vector2 worldPosition = new Vector2(screenPosition.X + Position.X - halfWidth,
                                     screenPosition.Y + Position.Y - halfHeight);
        
        return worldPosition;
    }
}