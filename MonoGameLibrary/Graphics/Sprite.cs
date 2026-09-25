using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary.Graphics;

namespace MonoGameLibrary.Graphics;

public class Sprite
{
    public Texture2D Texture {get; set; }
    public Vector2 Position;
    public float Scale {get; set; } = 1f;
    public float Speed = 200f;

    public Rectangle? SourceRectangle { get; set; }
    public Color Tint { get; set; } = Color.White;  
    public Sprite(Texture2D texture)
    {
        Texture = texture;   
    }
    public void Update(GameTime gameTime)
    {
        
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        Rectangle source = SourceRectangle
        
        ?? new Rectangle(0, 0, Texture.Width, Texture.Height);
        
        Vector2 origin = new Vector2(source.Width * 0.5f, source.Height * 0.5f);
        
        spriteBatch.Draw(Texture, Position, source, Tint, 0f,
        
        origin, Scale, SpriteEffects.None, 0f);
    }
}

