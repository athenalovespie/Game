using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary;
using First_game.Entities;
using MonoGameLibrary.Graphics;
using System;
using System.Collections.Generic;
using First_game.World;
using PlayerInventory = First_game.Inventory.Inventory;
using First_game.Inventory;
using First_game.Input;
using First_game.UI;
using MonoGameLibrary.Input;
using First_game.Actions;
using First_game.Fishing;
using First_game.Doors;
using First_game.Harvesting;
using System.IO;
using System.Diagnostics;

namespace First_game;


public class Game1 : Core
{
    private Sprite background;
    private Player cat;
    private PlayerInventory inventory;
    private WorldPickupSystem pickupSystem;
    private SpriteFont hudFont;
    private Camera2D camera;
    private readonly WorldRenderer worldRenderer = new WorldRenderer();
    private ResourceWorld resources;
    private HarvestController harvesting;
    private readonly Random random = new Random();
    private Sprite House;
    private Sprite Tent;
    private PlacedObject Lake;
    private MouseInteractionController mouseInteractions;
    private KeyboardState _previousKeyboard;
    private GridPlacer gridPlacer;
    private Texture2D gridPixel;
    private Func<string, Texture2D> itemTextureLoader;
    private UIManager uiManager;
    private InventoryPanel inventoryPanel;
    private Hotbar hotbar;
    private HotbarPanel hotbarPanel;
    private readonly MouseInput mouseInput = new MouseInput();
    private TimeSpan worldElapsed;
    private FishingController fishing;
    private FishingOverlay fishingOverlay;
    private DoorSystem doors;
    private DoorOverlay doorOverlay;
    private readonly GameTime worldTime = new GameTime();

    public Game1() : base("Game1" , 1280 , 720, false)
    {

    }
    private readonly WorldGrid worldGrid = new WorldGrid(
    cellSize: 100,
    origin: new Vector2(-3000, -3000),
    columnCount: 60,
    rowCount: 60);

    protected override void Initialize()
    {
        var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;

        Graphics.PreferredBackBufferWidth = display.Width;
        Graphics.PreferredBackBufferHeight = display.Height;
        Graphics.IsFullScreen = true;
        Graphics.ApplyChanges();

        base.Initialize();
    }
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
        inventory = new PlayerInventory(itemDefinitions, itemBehaviors, PlayerInventory.PlayerCapacity);
        inventory.AddItem(HarvestCatalog.BasicAxeId);
        hotbar = new Hotbar(inventory);
        resources = new ResourceWorld(worldGrid);
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
        var inventoryBackground = Content.Load<Texture2D>("Images/Inventory");
        var hotbarBackground = Content.Load<Texture2D>("Images/Hudbar");
        var selectionHighlight = Content.Load<Texture2D>("Images/Highlight");
        var itemSlots = new ItemSlotRenderer(itemDefinitions, itemTextureLoader, hudFont, selectionHighlight);
        var inventoryDrag = new InventoryDragController();
        hotbarPanel = new HotbarPanel(inventory, hotbar, hotbarBackground, itemSlots, inventoryDrag);


        var walkTextureRight = Content.Load<Texture2D>("Images/Right_walk");
        var walkTextureLeft = Content.Load<Texture2D>("Images/Left_walk");

        var animations = new Dictionary<string, Animation>
        {
            // These filenames face opposite to their labels. Offsets align the wider sheets.
            { "ChopRight", new Animation(Content.Load<Texture2D>("Images/Left_Axe"), 4)
                { IsLooping = false, DrawOffset = new Vector2(155, -1.5f) } },
            { "ChopLeft", new Animation(Content.Load<Texture2D>("Images/Right_Axe"), 4)
                { IsLooping = false, DrawOffset = new Vector2(-155, -1.5f) } },
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
                Content.Load<Texture2D>("Images/Back_Idle"), 2) { FrameDuration = 0.7f }},
        };

        cat = new Player(animations);
        uiManager = new UIManager(gridPixel, cat.Input);
        inventoryPanel = new InventoryPanel(
            inventory, hotbar, inventoryBackground, itemSlots, inventoryDrag, cat.Input);
        uiManager.Register(MenuType.Inventory, inventoryPanel);
        uiManager.Register(MenuType.Crafting, new CraftingPanel(hudFont, cat.Input));
        uiManager.Register(MenuType.Pause, new PauseMenu(hudFont));
        cat.Position = new Vector2(100, 1200);
        cat.Scale = 0.5f;
        cat.Speed = 300f;
        cat.CollisionSize = new Vector2(270, 64);
        cat.CollisionOffset = new Vector2(0, 331);

        //placing custom footprint for lake
        Point[] lakeFootprint = GridPlacer.CreateFootprint(
                "..XXXXXX....",
                ".XXXXXXXXX..",
                ".XXXXXXXXXX.",
                "XXXXXXXXXXXX",
                "XXXXXXXXXXXX",
                ".XXXXXXXXXXX",
                ".XXX....XXX."
        );

        if (!gridPlacer.TryPlaceFootprint(
        LakeTexture,
        new Vector2(-2000, 1500),
        lakeFootprint,
        anchorInCells: new Vector2(6f, 7f),
        scale: 0.4f,
        out Lake,
        groundOffsetY: LakeTexture.Height / 2f - 550f,
        groundOffsetX: -100f))
        {
        throw new InvalidOperationException(
            "The lake footprint is occupied or outside the grid.");
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
        doors = DoorConfiguration.Preload(
            Path.Combine(AppContext.BaseDirectory, "Content", "doors.json"),
            cat, worldGrid, LogDoorError);
        doorOverlay = new DoorOverlay(doors, gridPixel, inventoryPanel);
        doors.AreaChanged += OnAreaChanged;

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
                resources.Register(tree, worldGrid.WorldToCell(TreePosition), HarvestCatalog.Tree);
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
                resources.Register(pine, worldGrid.WorldToCell(PinePosition), HarvestCatalog.Pine);
            }
        }
        camera = new Camera2D(cat.Position);
        mouseInteractions = new MouseInteractionController(camera, pickupSystem, inventory, mouseInput);
        fishing = new FishingController(cat, inventory);
        fishing.Register(new FishingSpot(worldGrid, Lake.GetOccupiedCells(), "(O)fish", new FishingSettings()));
        fishingOverlay = new FishingOverlay(gridPixel);
        mouseInteractions.TryInteract = fishing.TryInteract;
        harvesting = new HarvestController(cat, hotbar, resources, HarvestCatalog.Tools);
        mouseInteractions.TryPrimaryInteract = harvesting.TryInteract;

        background = new Sprite(mapTexture);
        background.Scale = 2.0f;
        background.Position = new Vector2(620, 360);
    }

    protected override void OnDeactivated(object sender, EventArgs args)
    {
        // A release outside the game window must not leave a pending drag.
        if (uiManager?.ActiveMenu == MenuType.Inventory)
            uiManager.Close();
        base.OnDeactivated(sender, args);
    }

    protected override void Update(GameTime gameTime)
    {
        mouseInput.Update();
        var currentKeyboard = Keyboard.GetState();
        var uiInput = new UIInput(currentKeyboard, _previousKeyboard, mouseInput);
        bool wasTransitioning = doors.IsTransitioning;

        // Continue sampling input during fades, but consume it without buffering.
        if (!wasTransitioning && IsActive)
            uiManager.Update(gameTime, uiInput, GraphicsDevice.Viewport);

        bool blocked = wasTransitioning || !IsActive || uiManager.ConsumedInputThisFrame;
        bool mouseOverHotbar = false;
        if (!blocked)
            mouseOverHotbar = hotbarPanel.Update(uiInput, GraphicsDevice.Viewport);

        if ((!wasTransitioning && uiManager.ExitRequested)
            || GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
            Exit();

        // The inactive exterior retains its objects and its exact respawn clock.
        bool advanceExterior = doors.ActiveArea.IsExterior
            && !wasTransitioning && !uiManager.PausesWorld;
        if (advanceExterior)
            worldElapsed += gameTime.ElapsedGameTime;
        worldTime.TotalGameTime = worldElapsed;
        worldTime.ElapsedGameTime = advanceExterior ? gameTime.ElapsedGameTime : TimeSpan.Zero;
        if (advanceExterior)
            pickupSystem.Update(worldTime);

        doors.SetInteractionEnabled(!blocked && !cat.Actions.IsBusy);
        if (!blocked)
        {
            // Resolve movement/trigger exits before E, including an exit on this frame.
            cat.Update(gameTime);
            cat.Actions.Update(gameTime, ActionInput.FromKeyboard(currentKeyboard, _previousKeyboard));
            if (doors.ActiveArea.IsExterior)
                fishing.Update(gameTime);
        }
        doors.SetInteractionEnabled(!blocked && !cat.Actions.IsBusy);
        doors.Update((float)gameTime.ElapsedGameTime.TotalSeconds, uiInput);

        if (doors.ActiveArea.IsExterior)
            mouseInteractions.Update(worldTime, GraphicsDevice.Viewport, cat.GroundPosition,
                blocked || doors.IsTransitioning || mouseOverHotbar || cat.Actions.IsBusy);

        // A world click may have started an action after the door update.
        doors.SetInteractionEnabled(!blocked && !doors.IsTransitioning && !cat.Actions.IsBusy);
        if (!uiManager.PausesWorld && !wasTransitioning && !doors.IsTransitioning)
        {
            camera.UpdateTarget(cat.Position);
            camera.Update(gameTime);
        }

        _previousKeyboard = currentKeyboard;
        base.Update(gameTime);
    }

    private void OnAreaChanged()
    {
        // Teleports must not interpolate the camera across unrelated areas.
        camera.Position = cat.Position;
        camera.TargetPosition = cat.Position;
    }

    private static void LogDoorError(string message)
    {
        Trace.TraceError(message);
        Console.Error.WriteLine(message);
    }

    protected override void UnloadContent()
    {
        if (doors != null)
        {
            doors.AreaChanged -= OnAreaChanged;
            doors.Dispose();
        }
        gridPixel?.Dispose();
        base.UnloadContent();
    }

    private bool PlantCellOverlapsPlayer(Vector2 position)
    {
        Vector2 topLeft = worldGrid.CellToWorld(worldGrid.WorldToCell(position));
        return cat.Bounds.Intersects(new Rectangle(
            (int)topLeft.X, (int)topLeft.Y, worldGrid.CellSize, worldGrid.CellSize));
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(doors.ActiveArea.IsExterior ? Color.White : new Color(31, 27, 26));
        Matrix transformMatrix = camera.GetTransform(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        SpriteBatch.Begin(transformMatrix: transformMatrix);

        if (doors.ActiveArea.IsExterior)
        {
            background.Draw(SpriteBatch);
            Lake?.Sprite.Draw(SpriteBatch);
            worldRenderer.Submit(House.SortY, House.Draw);
            worldRenderer.Submit(Tent.SortY, Tent.Draw);
            resources.SubmitDraw(worldRenderer);
            pickupSystem.SubmitDraw(worldRenderer);
            worldRenderer.Submit(cat.Bounds.Bottom, cat.Draw);
            worldRenderer.Draw(SpriteBatch);
            fishingOverlay.DrawWorld(SpriteBatch, cat, fishing.Active);
            DrawGrid();
        }
        else
        {
            doors.ActiveArea.Draw(SpriteBatch, gridPixel);
            cat.Draw(SpriteBatch);
        }

        SpriteBatch.End();
        SpriteBatch.Begin();
        hotbarPanel.Draw(SpriteBatch, GraphicsDevice.Viewport);
        uiManager.Draw(SpriteBatch, GraphicsDevice.Viewport);
        doorOverlay.Draw(SpriteBatch, GraphicsDevice.Viewport);
        SpriteBatch.End();
        base.Draw(gameTime);
    }
}
