// using System.Numerics;
// using System.Runtime.Intrinsics;
// using System.Security.Cryptography;
// using System.Drawing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary;
using First_game.Entities;
using MonoGameLibrary.Graphics;
using System;
using System.Collections.Generic;
using First_game.World;

namespace First_game;


public class Game1 : Core
{
    private Sprite background;
    private Player cat;

    private int fishCollected;

    private SpriteFont hudFont;

    private Camera2D camera;

    private readonly List<WorldPickup> fish = new List<WorldPickup>();
    private readonly Random random = new Random();

    private Sprite House;
    private Sprite Tent;

    private MouseState _previousMouse;
    public Game1() : base("Game1" , 1280 , 720, false)
    {

    }

    protected override void Initialize()
    {
        var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;

        Graphics.PreferredBackBufferWidth = display.Width;
        Graphics.PreferredBackBufferHeight = display.Height;
        Graphics.IsFullScreen = true;
        Graphics.ApplyChanges();

        base.Initialize();
    }


    protected override void LoadContent()
    {   
        var catTexture = Content.Load<Texture2D>("Images/startercat");
        var mapTexture = Content.Load<Texture2D>("Images/grass");
        var fishTexture = Content.Load<Texture2D>("Images/Fish");
        hudFont = Content.Load<SpriteFont>("Fonts/UIFont");
        var HouseTexture = Content.Load<Texture2D>("Images/House");
        var TentTexture = Content.Load<Texture2D>("Images/Tent");

        var walkTextureRight = Content.Load<Texture2D>("Images/Right_walk");
        var walkTextureLeft = Content.Load<Texture2D>("Images/Left_walk");

        var animations = new Dictionary<string, Animation>
        {
            { "WalkRight", new Animation(walkTextureRight, 9)  },
            { "WalkLeft",  new Animation(walkTextureLeft, 9)  },
            { "WalkDown",  new Animation(walkTextureRight, 9)  },
            { "WalkUp",    new Animation(walkTextureLeft, 9)  },

            { "Right_Idle", new Animation(
                Content.Load<Texture2D>("Images/Right_Idle"), 2) { FrameDuration = 0.7f }},
            { "Left_Idle", new Animation(
                Content.Load<Texture2D>("Images/Left_Idle"), 2) { FrameDuration = 0.7f }},
            { "Front_Idle", new Animation(
                Content.Load<Texture2D>("Images/Front_Idle"), 2) { FrameDuration = 0.7f }},
            { "Back_Idle", new Animation(
                Content.Load<Texture2D>("Images/Back_Idle"), 2) { FrameDuration = 0.7f }}
        };
    
        cat = new Player(animations);
        cat.Position = new Vector2(100, 700);
        cat.Scale = 0.5f;
        cat.Speed = 300f;

        for (int fishIndex = 0; fishIndex < 5; fishIndex++)
        {
            Vector2 fishPosition = new Vector2(
                random.Next(-1000, 1201),
                random.Next(-1000, 1201));
            fish.Add(new WorldPickup(fishTexture, fishPosition, 0.2f));
        }
        camera = new Camera2D(cat.Position);

        House = new Sprite(HouseTexture);
        House.Position = new Vector2(400, 100);
        House.Scale = 0.3f;

        Tent = new Sprite(TentTexture);
        Tent.Position = new Vector2(-500, 2000);
        Tent.Scale = 0.2f;
    
        background = new Sprite(mapTexture);
        background.Scale = 1.0f;
        background.Position = new Vector2(620, 360);
        // TODO: use this.Content to load your game content here
    }

    protected override void Update(GameTime gameTime)
    {
        var currentMouse = Mouse.GetState();
        bool rightClicked;
        if (currentMouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released)
        {
            rightClicked = true;
        }
        else
        {
            rightClicked = false; 
        }

        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        cat.Update(gameTime);

        if (rightClicked)
        {
            Vector2 mouseWorldPosition = camera.ScreenToWorld(
                currentMouse.Position.ToVector2(),
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height);

            foreach (WorldPickup fishPickup in fish)
            {
                if (!fishPickup.IsCollected
                    && fishPickup.IsWithinReach(cat.Position, 200f)
                    && fishPickup.ContainsPoint(mouseWorldPosition))
                {
                    fishPickup.Collect();
                    fishCollected++;
                    break;
                }
            }
        }

        camera.UpdateTarget(cat.Position);
        camera.Update(gameTime);

        _previousMouse = currentMouse;

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.White);


        Matrix transformMatrix = camera.GetTransform(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        // Begin the sprite batch to prepare for rendering.
        SpriteBatch.Begin(transformMatrix: transformMatrix);

        background.Draw(SpriteBatch);
        House.Draw(SpriteBatch);
        Tent.Draw(SpriteBatch);
        foreach (WorldPickup fishPickup in fish)
        {
            fishPickup.Draw(SpriteBatch);
        }
        cat.Draw(SpriteBatch);
   
        // Always end the sprite batch when finished.
        SpriteBatch.End();

        SpriteBatch.Begin();
        SpriteBatch.DrawString(hudFont, $"Fish: {fishCollected}", new Vector2(20, 20), Color.Black);
        SpriteBatch.End();


        // TODO: Add your drawing code here

        base.Draw(gameTime);
    }
}

// Hello cutsy, I added this comment
//This is bby, we are doing a new test
