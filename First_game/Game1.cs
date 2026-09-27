
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary;
using First_game.Entities;
using MonoGameLibrary.Graphics;
using System;
using System.Collections.Generic;
using First_game.World;
using InventoryRenderer = First_game.Inventory.Inventory;
using First_game.Inventory;

namespace First_game;


public class Game1 : Core
{
    private Sprite background;
    private Player cat;
    private InventoryRenderer inventory;
    private WorldPickupSystem pickupSystem;
    private SpriteFont hudFont;
    private Camera2D camera;
    private readonly WorldRenderer worldRenderer = new WorldRenderer();
    private readonly List<Sprite> trees = new List<Sprite>();
    private readonly List<Sprite> pines = new List<Sprite>();
    private readonly Random random = new Random();
    private Sprite House;
    private Sprite Tent;

    private Sprite Lake;
    private MouseState _previousMouse;
    private KeyboardState _previousKeyboard;
    private GridPlacer gridPlacer;
    private Texture2D gridPixel;
    private Func<string, Texture2D> itemTextureLoader;
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
        itemTextureLoader = assetName => Content.Load<Texture2D>(assetName);
        var catTexture = Content.Load<Texture2D>("Images/startercat");
        var mapTexture = Content.Load<Texture2D>("Images/grass");
        ItemDefinitionRegistry itemDefinitions = SampleItemCatalog.CreateDefinitions();
        ItemCategoryBehaviorRegistry itemBehaviors = SampleItemCatalog.CreateBehaviors();
        itemDefinitions.ResolveUseEffects(UseEffectRegistry.CreateBuiltIns(), itemBehaviors);
        inventory = new InventoryRenderer(itemDefinitions, itemBehaviors);
        var spawnRules = new WorldSpawnRuleRegistry(itemDefinitions);
        WorldSpawnCatalog.RegisterRules(spawnRules);
        pickupSystem = new WorldPickupSystem(
            worldGrid,
            itemDefinitions,
            spawnRules,
            itemTextureLoader,
            random);
        hudFont = Content.Load<SpriteFont>("Fonts/UIFont");
        var HouseTexture = Content.Load<Texture2D>("Images/House");
        var TreeTexture = Content.Load<Texture2D>("Images/Tree");
        var PineTexture = Content.Load<Texture2D>("Images/Pine");
        var TentTexture = Content.Load<Texture2D>("Images/Tent");
        var LakeTexture = Content.Load<Texture2D>("Images/Lake");

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
            LakeTexture,
            new Vector2(-2000, 1500),
            widthInCells: 12,
            heightInCells: 7,
            scale: 0.4f,
            out Lake,
            groundOffsetY: LakeTexture.Height / 2f - 550f,
            groundOffsetX: -100f))
            {
            throw new InvalidOperationException(
                "The house footprint is occupied or outside the grid.");

        }
        string[] lakeShape =
            {
                "..XXXXXX....",
                ".XXXXXXXXX..",
                ".XXXXXXXXXX.",
                "XXXXXXXXXXXX",
                "XXXXXXXXXXXX",
                ".XXXXXXXXXXX",
                ".XXX....XXX."
            };
        // Use the same position passed to TryPlaceBuilding.
        Point lakeStart = worldGrid.WorldToCell(
            new Vector2(-2000, 1500));

        for (int row = 0; row < lakeShape.Length; row++)
        {
            for (int column = 0; column < lakeShape[row].Length; column++)
            {
                if (lakeShape[row][column] == '.')
                {
                    Point cell = new Point(
                        lakeStart.X + column,
                        lakeStart.Y + row);

                    worldGrid.ClearCell(cell);
                }
            }
        }
        
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
            groundOffsetX: -200f))
            {
            throw new InvalidOperationException(
                "The house footprint is occupied or outside the grid.");

        }
        pickupSystem.RegisterNodes(WorldSpawnCatalog.CreateNodes());

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
                groundOffsetY: TreeTexture.Height / 2f - 100f,
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
                groundOffsetY: PineTexture.Height / 2f -100f,
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
        pickupSystem.Update(gameTime);
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

            pickupSystem.TryCollectAt(
                cat.Position,
                mouseWorldPosition,
                inventory,
                200f,
                gameTime.TotalGameTime.TotalSeconds);
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
        Lake.Draw(SpriteBatch);
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

        pickupSystem.SubmitDraw(worldRenderer);

        // Submit the player last so it draws in front when ground positions tie.
        worldRenderer.Submit(cat.Bounds.Bottom, cat.Draw);
        worldRenderer.Draw(SpriteBatch);
        DrawGrid();
   
        // Always end the sprite batch when finished.
        SpriteBatch.End();

        SpriteBatch.Begin();
        if (inventoryOpen)
            inventory.Draw(
                SpriteBatch,
                gridPixel,
                hudFont,
                itemTextureLoader,
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height);
        SpriteBatch.End();


        // TODO: Add your drawing code here

        base.Draw(gameTime);
    }

}

// Hello cutsy, I added this comment
//This is bby, we are doing a new test
