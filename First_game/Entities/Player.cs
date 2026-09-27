
using System;
using System.Collections.Generic;
using System.Linq;
using MonoGameLibrary.Graphics;
using MonoGameLibrary.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace First_game.Entities;

public class Player
{
    protected AnimationManager _animationManager;

    protected Dictionary<string, Animation> _animations;
    private Sprite _sprite;
    private Vector2 _position;

    private Vector2 _velocity;

    public InputBindings Input { get; } = new InputBindings();

    public float Speed;
    public float Scale;
    public Vector2 CollisionSize {get; set; }
    public Vector2 CollisionOffset {get; set; }

    public Func<Rectangle, bool> IsMovementBlocked { get; set; }

    public Rectangle Bounds
    {
        get
        {
            // Rectangle source = _sprite.SourceRectangle
            //     ?? new Rectangle(0, 0, _sprite.Texture.Width, _sprite.Texture.Height);
            float width = (CollisionSize.X * Scale);
            float height = (CollisionSize.Y * Scale);

            Vector2 collisionCenter = Position + CollisionOffset*Scale;

            return new Rectangle(
                (int)(collisionCenter.X - width * 0.5f),
                (int)(collisionCenter.Y - height * 0.5f),
                (int)width,
                (int)height);
        }
    }

    private string _idleAnimation = "Right_Idle";

    public Vector2 Position
    {
        get { return _position; }
        set { _position = value; }
    }
    public Player(Texture2D texture, Vector2 position)
    {
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
            _sprite.Position = Position;
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
        // Small steps prevent crossing an obstacle during a long frame.
        int steps = Math.Max(1, (int)MathF.Ceiling(
            MathF.Max(MathF.Abs(movement.X), MathF.Abs(movement.Y))));
        Vector2 step = movement / steps;

        for (int i = 0; i < steps; i++)
        {
            Vector2 previous = Position;
            Position += new Vector2(step.X, 0);
            if (IsMovementBlocked?.Invoke(Bounds) == true)
                Position = previous;

            previous = Position;
            Position += new Vector2(0, step.Y);
            if (IsMovementBlocked?.Invoke(Bounds) == true)
                Position = previous;
        }
    }

    protected virtual void SetAnimations()
    {
        if(_velocity.X > 0)
            {
            _idleAnimation = "Right_Idle";
            _animationManager.Play(_animations["WalkRight"]); 
            }   
        else if(_velocity.X < 0)
            {
            _idleAnimation = "Left_Idle";
            _animationManager.Play(_animations["WalkLeft"]);
            }
        else if(_velocity.Y > 0)
            {
            _idleAnimation = "Front_Idle";
            _animationManager.Play(_animations["WalkDown"]);
            }
        else if(_velocity.Y < 0)
            {
            _idleAnimation = "Back_Idle";
            _animationManager.Play(_animations["WalkUp"]);
            }
        else
            {
            _animationManager.Play(_animations[_idleAnimation]);
            }
    }

    public Player(Dictionary<string, Animation> animations)
    {
        _animations = animations;
        _animationManager = new AnimationManager(_animations.First().Value);
        _sprite = new Sprite(_animationManager.Texture);
    }
    public void Update(GameTime gameTime)
    {
        Move(gameTime);

        if (_animationManager != null)
        {
            SetAnimations();
            _animationManager.Update(gameTime);
        }
              
    }



}

