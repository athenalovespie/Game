using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGameLibrary.Graphics;

public class Animation
{
    public int FrameCount { get; private set; }

    public int FrameHeight { get { return Texture.Height; } }

    public float FrameDuration { get; set ; }

    public int FrameWidth { get {return Texture.Width / FrameCount; }}

    public bool IsLooping { get; set; }

    public Texture2D Texture { get; private set; }

    public Animation(Texture2D texture, int frameCount)
    {
        Texture = texture;

        FrameCount = frameCount;

        IsLooping = true;

        FrameDuration = 0.1f;

    }
}