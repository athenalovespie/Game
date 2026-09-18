// using System.Numerics;
// using System.Runtime.Intrinsics;
// using System.Security.Cryptography;
// using System.Drawing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary;


namespace First_game;

public class Game1 : Core
{

    private Texture2D _startercat;
    private Vector2 _catPosition = new Vector2(640, 360);
    public Game1() : base("Game1" , 1280 , 720, false)
    {

    }

    protected override void Initialize()
    {
        // TODO: Add your initialization logic here

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _startercat = Content.Load<Texture2D>("Images/startercat");
        // TODO: use this.Content to load your game content here
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        // TODO: Add your update logic here
        float seconds = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (Keyboard.GetState().IsKeyDown(Keys.Right))
        {
            _catPosition.X += 200f*seconds;
        }

          if (Keyboard.GetState().IsKeyDown(Keys.Left))
        {
            _catPosition.X -= 200f*seconds;
        }

        if (Keyboard.GetState().IsKeyDown(Keys.Up))
        {
            _catPosition.Y -= 200f*seconds;
        }

        if (Keyboard.GetState().IsKeyDown(Keys.Down))
        {
            _catPosition.Y += 200f*seconds;
        }
        
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.White);

        // Begin the sprite batch to prepare for rendering.
        SpriteBatch.Begin();

        //Draw the Logo Texture
        SpriteBatch.Draw(
            _startercat,                 // 1. Texture
            _catPosition,                // 2. Position
            null,                        // 3. Source rectangle
            Color.White,                 // 4. Tint
            0f,                          // 5. Rotation
            new Vector2(
                _startercat.Width * 0.5f,
                _startercat.Height * 0.5f), // 6. Origin
            0.25f,                       // 7. Scale
            SpriteEffects.None,          // 8. Flip
            0f                           // 9. Layer depth
        );

        // Always end the sprite batch when finished.
        SpriteBatch.End();


        // TODO: Add your drawing code here

        base.Draw(gameTime);
    }
}

// Hello cutsy, I added this comment
//This is bby, we are doing a new test
