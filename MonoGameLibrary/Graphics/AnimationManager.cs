using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGameLibrary.Graphics;


public class AnimationManager
{
    private Animation _animation;

    private float _timer;
    private float _playbackRate = 1f;

    public int CurrentFrame {get; set; }

    public bool IsPlaying { get; private set; } = true;

    public AnimationManager(Animation animation)
    {
        _animation = animation;
    }

public Texture2D Texture => _animation.Texture;
    public Vector2 DrawOffset => _animation.DrawOffset;
    public Rectangle SourceRectangle => new Rectangle(
                                                    CurrentFrame * _animation.FrameWidth, 0,
                                                    _animation.FrameWidth, _animation.FrameHeight);

    public void Play(Animation animation, bool restart = false, float playbackRate = 1f)
    {
        if (!float.IsFinite(playbackRate) || playbackRate <= 0)
            throw new System.ArgumentOutOfRangeException(nameof(playbackRate));
        if (_animation == animation && IsPlaying && !restart)
            return;
        
        _animation = animation;
        _playbackRate = playbackRate;
        CurrentFrame = 0;
        _timer = 0f;
        IsPlaying = true;
    }

    public void Stop()
    {
        IsPlaying = false;
        _timer = 0f;
        CurrentFrame = 0;
    }

    public void Update(GameTime gameTime)
    {
        if (!IsPlaying)
        return;
        
        _timer += (float)gameTime.ElapsedGameTime.TotalSeconds * _playbackRate;

        while (_timer >= _animation.FrameDuration)
        {  
            _timer -= _animation.FrameDuration;
            CurrentFrame++;

            if(CurrentFrame >= _animation.FrameCount)
            {

                if(_animation.IsLooping)
                    CurrentFrame = 0;
                else
                {
                    CurrentFrame = _animation.FrameCount - 1;
                    IsPlaying = false;
                    _timer = 0f;
                    break;
                }
            }
        }
    }
}
