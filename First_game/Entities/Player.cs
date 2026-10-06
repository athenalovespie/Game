using System;
using System.Collections.Generic;
using System.Linq;
using MonoGameLibrary.Graphics;
using MonoGameLibrary.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using First_game.Actions;


namespace First_game.Entities;


public class Player
{
    protected AnimationManager _animationManager;

    protected Dictionary<string, Animation> _animations;
    private Sprite _sprite;
    private Vector2 _position;

    private Vector2 _velocity;

    public InputBindings Input { get; } = new InputBindings();

    public PlayerActionController Actions { get; }
    public PlayerActionState ActionState => Actions.IsBusy ? PlayerActionState.Acting : PlayerActionState.Free;
    public Vector2 GroundPosition => new Vector2(Bounds.Center.X, Bounds.Bottom);
    public string Facing { get; private set; } = "Right";

    public bool IsActionAnimationComplete
    {
        get
        {
            return ActionState == PlayerActionState.Acting
            && _animationManager != null
            && !_animationManager.IsPlaying;
        }
    }

    public float Speed;
    public float Scale;
    public Vector2 CollisionSize {get; set; }
    public Vector2 CollisionOffset {get; set; }

    public Func<Rectangle, bool> IsMovementBlocked { get; set; }

    public bool InputLocked { get; set; }
    public event Action PositionChanged;

    public Rectangle Bounds => BoundsAt(Position);

    public Rectangle BoundsAt(Vector2 position)
    {
            float width = (CollisionSize.X * Scale);
            float height = (CollisionSize.Y * Scale);

            Vector2 collisionCenter = position + CollisionOffset*Scale;

            return new Rectangle(
                (int)(collisionCenter.X - width * 0.5f),
                (int)(collisionCenter.Y - height * 0.5f),
                (int)width,
                (int)height);
    }

    public Vector2 PositionForGround(Vector2 ground)
    {
        float width = CollisionSize.X * Scale;
        float height = CollisionSize.Y * Scale;
        return new Vector2(ground.X + width * .5f - (int)width / 2,
            ground.Y - (int)height + height * .5f) - CollisionOffset * Scale;
    }

    private string _idleAnimation = "Right_Idle";

    public Vector2 Position
    {
        get { return _position; }
        set
        {
            if (_position == value) return;
            Rectangle previous = Bounds;
            _position = value;
            if (Bounds != previous) PositionChanged?.Invoke();
        }
    }
    public Player(Texture2D texture, Vector2 position)
    {
       Actions = new PlayerActionController(this);
       _sprite = new Sprite(texture);
        _position = position;
    }

    public void Draw(SpriteBatch spriteBatch)
{
        if (_animationManager != null)
            {
                _sprite.Texture = _animationManager.Texture;
                _sprite.SourceRectangle = _animationManager.SourceRectangle;
            }
            _sprite.Position = Position + (_animationManager?.DrawOffset ?? Vector2.Zero) * Scale;
            _sprite.Scale = Scale;
            _sprite.Draw(spriteBatch);
        }
    protected virtual void Move(GameTime gameTime)
    {
        _velocity = Vector2.Zero;
        var keyboard = Keyboard.GetState();

        if (keyboard.IsKeyDown(Input.Right)) _velocity.X += 1;
        if (keyboard.IsKeyDown(Input.Left))  _velocity.X -= 1;
        if (keyboard.IsKeyDown(Input.Up))    _velocity.Y -= 1;
        if (keyboard.IsKeyDown(Input.Down))  _velocity.Y += 1;

        if (_velocity != Vector2.Zero)
            _velocity.Normalize();

        _velocity *= Speed;

        float seconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        MoveBy(_velocity * seconds);
    }

    public void MoveBy(Vector2 movement)
    {
        if (InputLocked || movement == Vector2.Zero) return;
        Rectangle previousBounds = Bounds;
        // Test tentative positions without sending trigger events for blocked steps.
        int steps = Math.Max(1, (int)MathF.Ceiling(
            MathF.Max(MathF.Abs(movement.X), MathF.Abs(movement.Y))));
        Vector2 step = movement / steps;
        for (int i = 0; i < steps; i++)
        {
            Vector2 previous = _position;
            _position += new Vector2(step.X, 0);
            if (IsMovementBlocked?.Invoke(Bounds) == true) _position = previous;
            previous = _position;
            _position += new Vector2(0, step.Y);
            if (IsMovementBlocked?.Invoke(Bounds) == true) _position = previous;
        }
        // Report the final bounds before this frame's interaction input is handled.
        if (Bounds != previousBounds) PositionChanged?.Invoke();
    }

    protected virtual void SetAnimations()
    {
        if(_velocity.X > 0)
            {
            _idleAnimation = "Right_Idle";
            Facing = "Right";
            _animationManager.Play(_animations["WalkRight"]);
            }
        else if(_velocity.X < 0)
            {
            _idleAnimation = "Left_Idle";
            Facing = "Left";
            _animationManager.Play(_animations["WalkLeft"]);
            }
        else if(_velocity.Y > 0)
            {
            _idleAnimation = "Front_Idle";
            Facing = "Front";
            _animationManager.Play(_animations["WalkDown"]);
            }
        else if(_velocity.Y < 0)
            {
            _idleAnimation = "Back_Idle";
            Facing = "Back";
            _animationManager.Play(_animations["WalkUp"]);
            }
        else
            {
            _animationManager.Play(_animations[_idleAnimation]);
            }
    }

    public Player(Dictionary<string, Animation> animations)
    {
        Actions = new PlayerActionController(this);
        _animations = animations;
        _animationManager = new AnimationManager(_animations.First().Value);
        _sprite = new Sprite(_animationManager.Texture);
    }

    public void FaceTowards(Vector2 target)
    {
        Vector2 direction = target - GroundPosition;
        Facing = MathF.Abs(direction.X) >= MathF.Abs(direction.Y)
            ? (direction.X >= 0 ? "Right" : "Left")
            : (direction.Y >= 0 ? "Front" : "Back");
        _idleAnimation = Facing switch
        {
            "Left" => "Left_Idle",
            "Front" => "Front_Idle",
            "Back" => "Back_Idle",
            _ => "Right_Idle"
        };
    }

    /// <summary>Missing action artwork falls back to the current facing's idle pose.</summary>
    public bool PlayActionAnimation(string animationName, float? durationSeconds = null)
    {
        if (durationSeconds.HasValue && (!float.IsFinite(durationSeconds.Value) || durationSeconds <= 0))
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        _velocity = Vector2.Zero;
        if (_animationManager == null) return false;
        if (animationName == null || !_animations.TryGetValue(animationName, out Animation animation))
        {
            RestoreIdleAnimation();
            return false;
        }
        float rate = durationSeconds.HasValue
            ? animation.FrameCount * animation.FrameDuration / durationSeconds.Value : 1f;
        _animationManager.Play(animation, restart: true, playbackRate: rate);
        return true;
    }

    public void RestoreIdleAnimation()
    {
        _velocity = Vector2.Zero;
        if (_animationManager != null && _animations.TryGetValue(_idleAnimation, out Animation idle))
            _animationManager.Play(idle);
    }

    public void Update(GameTime gameTime)
    {

        if (InputLocked) return;
        if (!Actions.BlocksMovement){
            Move(gameTime);
        }
        if (_animationManager != null)
        {
            if(ActionState == PlayerActionState.Free){
            SetAnimations();
            }
            _animationManager.Update(gameTime);
        }

    }



}
