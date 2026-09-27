
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
    private readonly WorldRenderer worldRenderer = new WorldRenderer();
    private readonly List<WorldPickup> fish = new List<WorldPickup>();
    private readonly List<Sprite> trees = new List<Sprite>();
    private readonly List<Sprite> pines = new List<Sprite>();
    private readonly Random random = new Random();
    private Sprite House;
    private Sprite Tent;
    private MouseState _previousMouse;
    private KeyboardState _previousKeyboard;
    private GridPlacer gridPlacer;
    private Texture2D gridPixel;
    private Texture2D fishTexture;
    private bool inventoryOpen;

    public Game1() : base("Game1" , 1280 , 720, false)
    {

    }
//chat gpt
    private readonly WorldGrid worldGrid = new WorldGrid(
    cellSize: 100,
    origin: new Vector2(-3000, -3000),
    columnCount: 60,
    rowCount: 60);

    private bool TryPlaceFish(Texture2D texture, Vector2 worldPosition)
    {
    Point cell = worldGrid.WorldToCell(worldPosition);

    if (!worldGrid.CanPlace(cell))
    {
        return false;
    }

    // Snap the fish to the center of its cell.
    Vector2 position = worldGrid.CellCenter(cell);
    WorldPickup pickup = new WorldPickup(texture, position, 0.2f);

    if (!worldGrid.Occupy(cell, CellType.Pickup, pickup))
    {
        return false;
    }

    fish.Add(pickup);
    return true;
    }


//chat gpt
    protected override void Initialize()
    {
        var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;

        Graphics.PreferredBackBufferWidth = display.Width;
        Graphics.PreferredBackBufferHeight = display.Height;
        Graphics.IsFullScreen = true;
        Graphics.ApplyChanges();

        base.Initialize();
    }
// to see the grid lines
    private void DrawGrid()
{
    int left = (int)worldGrid.Origin.X;
    int top = (int)worldGrid.Origin.Y;
    int width = worldGrid.Columns * worldGrid.CellSize;
    int height = worldGrid.Rows * worldGrid.CellSize;

    Color lineColor = Color.White * 0.25f;

    for (int column = 0; column <= worldGrid.Columns; column++)
    {
        int x = left + column * worldGrid.CellSize;

        SpriteBatch.Draw(
            gridPixel,
            new Rectangle(x, top, 1, height),
            lineColor);
    }

    for (int row = 0; row <= worldGrid.Rows; row++)
    {
        int y = top + row * worldGrid.CellSize;

        SpriteBatch.Draw(
            gridPixel,
            new Rectangle(left, y, width, 1),
            lineColor);
    }
}



    protected override void LoadContent()
    { 
        gridPixel = new Texture2D(GraphicsDevice, 1, 1);
        gridPixel.SetData(new[] { Color.White });  
        gridPlacer = new GridPlacer(worldGrid);
        var catTexture = Content.Load<Texture2D>("Images/startercat");
        var mapTexture = Content.Load<Texture2D>("Images/grass");
        fishTexture = Content.Load<Texture2D>("Images/Fish");
        hudFont = Content.Load<SpriteFont>("Fonts/UIFont");
        var HouseTexture = Content.Load<Texture2D>("Images/House");
        var TreeTexture = Content.Load<Texture2D>("Images/Tree");
        var PineTexture = Content.Load<Texture2D>("Images/Pine");
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
        cat.Position = new Vector2(100, 1200);
        cat.Scale = 0.5f;
        cat.Speed = 300f;
        cat.CollisionSize = new Vector2(270, 64);
        cat.CollisionOffset = new Vector2(0, 331);
        cat.IsMovementBlocked = bounds => worldGrid.IntersectsBlockedCell(bounds);


        if (!gridPlacer.TryPlaceBuilding(
            HouseTexture,
            new Vector2(400, 150),
            widthInCells: 11,
            heightInCells: 4,
            scale: 0.3f,
            out House,
            groundOffsetY: HouseTexture.Height / 2f - 200f,
            groundOffsetX: 0f))
            {
            throw new InvalidOperationException(
                "The house footprint is occupied or outside the grid.");

        }
        if (!gridPlacer.TryPlaceBuilding(
            TentTexture,
            new Vector2(-500, 2000),
            widthInCells: 5,
            heightInCells: 3,
            scale: 0.15f,
            out Tent,
            groundOffsetY: TentTexture.Height / 2f - 667f,
            groundOffsetX: 0f))
            {
            throw new InvalidOperationException(
                "The house footprint is occupied or outside the grid.");

        }
        for (int fishIndex = 0; fishIndex < 5; fishIndex++)
        {
            Vector2 fishPosition = new Vector2(
                random.Next(-1000, 1201),
                random.Next(-1000, 1201));
            TryPlaceFish(fishTexture, fishPosition);
        }

        for (int treeIndex = 0; treeIndex < 10; treeIndex++)
        {
            Vector2 TreePosition = new Vector2(
                random.Next(-3000, 2000),
                random.Next(-3000, 2000));
            if (PlantCellOverlapsPlayer(TreePosition))
                continue;
            if (gridPlacer.TryPlaceSprite( 
                TreeTexture,
                TreePosition,
                CellType.Plant,
                0.2f,
                out Sprite tree,
                groundOffsetY: TreeTexture.Height / 2f,
                groundOffsetX: -447.5f))
            {
                trees.Add(tree);
            }
        }

        for (int pineIndex = 0; pineIndex < 10; pineIndex++)
        {
            Vector2 PinePosition = new Vector2(
                random.Next(-3000, 2000),
                random.Next(-3000, 2000));
            if (PlantCellOverlapsPlayer(PinePosition))
                continue;
            if (gridPlacer.TryPlaceSprite(
                PineTexture,
                PinePosition,
                CellType.Plant,
                0.2f,
                out Sprite pine,
                groundOffsetY: PineTexture.Height / 2f,
                groundOffsetX: -60f))
            {
                pines.Add(pine);
            }
        }
        camera = new Camera2D(cat.Position);

        // houseObstacle = new Obstacle(House, new Vector2(3750f, 1462f),new Vector2(-77.5f,1000.5f));
        // House.GroundOffsetY = (houseObstacle.Bounds.Bottom - House.Position.Y) / House.Scale;
    
        background = new Sprite(mapTexture);
        background.Scale = 2.0f;
        background.Position = new Vector2(620, 360);
    }

    protected override void Update(GameTime gameTime)
    {
        var currentMouse = Mouse.GetState();
        var currentKeyboard = Keyboard.GetState();
        if (currentKeyboard.IsKeyDown(Keys.E) && !_previousKeyboard.IsKeyDown(Keys.E))
            inventoryOpen = !inventoryOpen;

        bool rightClicked;
        if (currentMouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released)
        {
            rightClicked = true;
        }
        else
        {
            rightClicked = false; 
        }

        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
            Exit();

        if (currentKeyboard.IsKeyDown(Keys.Escape) && !_previousKeyboard.IsKeyDown(Keys.Escape))
        {
            if (inventoryOpen)
                inventoryOpen = false;
            else
                Exit();
        }

        if (!inventoryOpen)
            cat.Update(gameTime);

        if (rightClicked && !inventoryOpen)
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
                    Point cell = worldGrid.WorldToCell(fishPickup.Position);
                    worldGrid.ClearCell(cell);

                    fishPickup.Collect();
                    fishCollected++;
                    break;
                }
            }
        }

        camera.UpdateTarget(cat.Position);
        camera.Update(gameTime);

        _previousMouse = currentMouse;
        _previousKeyboard = currentKeyboard;

        base.Update(gameTime);
    }

    private bool PlantCellOverlapsPlayer(Vector2 position)
    {
        Vector2 topLeft = worldGrid.CellToWorld(worldGrid.WorldToCell(position));
        return cat.Bounds.Intersects(new Rectangle(
            (int)topLeft.X, (int)topLeft.Y, worldGrid.CellSize, worldGrid.CellSize));
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.White);


        Matrix transformMatrix = camera.GetTransform(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        // Begin the sprite batch to prepare for rendering.
        SpriteBatch.Begin(transformMatrix: transformMatrix);

        background.Draw(SpriteBatch);
        worldRenderer.Submit(House.SortY, House.Draw);
        worldRenderer.Submit(Tent.SortY, Tent.Draw);

        foreach (Sprite tree in trees)
        {
            worldRenderer.Submit(tree.SortY, tree.Draw);
        }

        foreach (Sprite pine in pines)
        {
            worldRenderer.Submit(pine.SortY, pine.Draw);
        }

        foreach (WorldPickup fishPickup in fish)
        {
            if (!fishPickup.IsCollected)
                worldRenderer.Submit(fishPickup.Position.Y, fishPickup.Draw);
        }

        // Submit the player last so it draws in front when ground positions tie.
        worldRenderer.Submit(cat.Bounds.Bottom, cat.Draw);
        worldRenderer.Draw(SpriteBatch);
        DrawGrid();
   
        // Always end the sprite batch when finished.
        SpriteBatch.End();

        SpriteBatch.Begin();
        SpriteBatch.DrawString(hudFont, $"Fish: {fishCollected}", new Vector2(20, 20), Color.Black);
        if (inventoryOpen)
            DrawInventory();
        SpriteBatch.End();


        // TODO: Add your drawing code here

        base.Draw(gameTime);
    }

    private void DrawInventory()
    {
        int viewportWidth = GraphicsDevice.Viewport.Width;
        int viewportHeight = GraphicsDevice.Viewport.Height;
        const int panelWidth = 460;
        const int panelHeight = 210;
        const int slotSize = 40;
        const int slotGap = 5;
        int panelX = (viewportWidth - panelWidth) / 2;
        int panelY = (viewportHeight - panelHeight) / 2;

        SpriteBatch.Draw(gridPixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.55f);
        SpriteBatch.Draw(gridPixel, new Rectangle(panelX, panelY, panelWidth, panelHeight), new Color(48, 52, 45));
        SpriteBatch.Draw(gridPixel, new Rectangle(panelX, panelY, panelWidth, 3), new Color(179, 167, 125));
        SpriteBatch.DrawString(hudFont, "Inventory", new Vector2(panelX + 24, panelY + 18), Color.White);

        int gridX = panelX + (panelWidth - (9 * slotSize + 8 * slotGap)) / 2;
        int gridY = panelY + 54;
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 9; column++)
            {
                int slotX = gridX + column * (slotSize + slotGap);
                int slotY = gridY + row * (slotSize + slotGap);
                SpriteBatch.Draw(gridPixel, new Rectangle(slotX, slotY, slotSize, slotSize), new Color(20, 23, 20));
                SpriteBatch.Draw(gridPixel, new Rectangle(slotX + 2, slotY + 2, slotSize - 4, slotSize - 4), new Color(77, 79, 64));

                if (row == 0 && column == 0 && fishCollected > 0)
                {
                    int iconSize = 26;
                    float scale = Math.Min(iconSize / (float)fishTexture.Width, iconSize / (float)fishTexture.Height);
                    int iconWidth = (int)(fishTexture.Width * scale);
                    int iconHeight = (int)(fishTexture.Height * scale);
                    var iconBounds = new Rectangle(
                        slotX + (slotSize - iconWidth) / 2,
                        slotY + (slotSize - iconHeight) / 2,
                        iconWidth,
                        iconHeight);
                    SpriteBatch.Draw(fishTexture, iconBounds, Color.White);
                    SpriteBatch.DrawString(hudFont, fishCollected.ToString(), new Vector2(slotX + 24, slotY + 22), Color.White);
                }
            }
        }
    }

}

// Hello cutsy, I added this comment
//This is bby, we are doing a new test
