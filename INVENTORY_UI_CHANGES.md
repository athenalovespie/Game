# Inventory and door prompt changes

## 1. Changed files

File | Change
--- | ---
`MonoGameLibrary/Input/InputBindings.cs` | Default inventory key is Tab; emits InventoryChanged only when the binding changes.
`First_game/Doors/DoorConfiguration.cs` | Uses Tab when InventoryKey is omitted from JSON.
`First_game/Content/doors.json` | Shipped InventoryKey explicitly set to Tab.
`First_game/UI/InventoryPanel.cs` | Binding-derived close hint and cached footer measurements; exposes the footer font and fitted scale.
`First_game/UI/ItemSlotRenderer.cs` | Shares the caption font and scale calculation; supports premeasured caption drawing.
`First_game/UI/DoorOverlay.cs` | Reuses the inventory footer font and exact fitted scale, retaining top-center position, white text and dark backing.
`First_game/UI/CraftingPanel.cs` | Replaces stale E inventory hint with the current binding; caches text and measurement on binding changes.
`First_game/Game1.cs` | Shares player bindings with both panels and passes the existing inventory panel to the door overlay.
`Tests/GameplayChecks/InventoryDragChecks.cs` | Uses Tab for the inventory-close drag-cancellation check.
`Tests/GameplayChecks/InventoryChecks.cs` | Checks fresh/held Tab, E and I behavior, rebinding, and cached hint identity.
`First_game/UI/README.md` | Documents Tab as the inventory toggle.
`First_game/Doors/README.md` | Documents the Tab config default.
`INVENTORY_UI_CHANGES.md` | This complete source bundle and manual checklist.

## Input flow and configuration

Tab opens inventory on an up-to-down keyboard transition; holding it does not toggle.
A fresh Tab closes inventory, consuming that frame's world input. E while inventory
is open neither closes the inventory nor starts a door interaction. After closing,
approach a door and press E to enter or exit. During fades, Game1 skips menu updates
and continues sampling previous/current input, so Tab presses are consumed without
being buffered. The existing UIInput/UIManager/Game1 guards already implement these
rules; no hard-coded Tab check was added.

No runtime code depends on E closing inventory. The stale E strings were in the
inventory close hint and crafting inventory hint; both now follow the binding.

The only gameplay key config found is First_game/Content/doors.json. Its explicit
InventoryKey is now Tab, and rebuilding copies that source config to output.
If you retain a separate config with InventoryKey set to I, that explicit value
still wins: change it to Tab or remove InventoryKey to use the new default.
No separate user settings or save-file key-binding system was found.

The inventory footer uses the already-loaded Fonts/UIFont SpriteFont:
Caslonroman.ttf, size 28, spacing 0, kerning enabled. Its scale is
min(0.65, inventory width / measured footer width). The door prompt now uses that
same reference and fitted scale, plus the footer's one-pixel shadow. Its top-center
position, white color and dark backing are retained. Footer/door/crafting text
measurements are cached; viewport fitting uses arithmetic without remeasuring.

## 2. Complete code for every changed file

The following are unabridged snapshots of the delivered source/config/documentation.


### MonoGameLibrary/Input/InputBindings.cs

````csharp
using System;
using Microsoft.Xna.Framework.Input;

namespace MonoGameLibrary.Input;

public class InputBindings
{
    private Keys interact = Keys.E;
    public Keys Down { get; set; } = Keys.S;
    public Keys Left { get; set; } = Keys.A;
    public Keys Right { get; set; } = Keys.D;
    public Keys Up { get; set; } = Keys.W;
    private Keys inventory = Keys.Tab;
    public Keys Inventory
    {
        get => inventory;
        set
        {
            if (inventory == value) return;
            inventory = value;
            InventoryChanged?.Invoke();
        }
    }

    public event Action InventoryChanged;

    public Keys Interact
    {
        get => interact;
        set
        {
            if (interact == value) return;
            interact = value;
            InteractChanged?.Invoke();
        }
    }

    public event Action InteractChanged;
}
````

### First_game/Doors/DoorConfiguration.cs

````csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using First_game.Entities;
using First_game.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace First_game.Doors;

public sealed class DoorConfiguration
{
    public Keys InteractionKey { get; set; } = Keys.E;
    public Keys InventoryKey { get; set; } = Keys.Tab;
    public float FadeSeconds { get; set; } = .25f;
    public AreaDefinition[] Areas { get; set; } = Array.Empty<AreaDefinition>();
    public DoorDefinition[] Doors { get; set; } = Array.Empty<DoorDefinition>();

    public static DoorSystem Preload(string path, Player player, WorldGrid exteriorGrid,
        Action<string> logError)
    {
        var exterior = new ResidentArea("exterior", exteriorGrid);
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new JsonStringEnumConverter());
            DoorConfiguration config = JsonSerializer.Deserialize<DoorConfiguration>(
                File.ReadAllText(path), options)
                ?? throw new InvalidDataException("Door configuration is empty.");
            if (config.InteractionKey == Keys.None || config.InventoryKey == Keys.None
                || config.InteractionKey == config.InventoryKey)
                throw new InvalidDataException("Door and inventory keys must be distinct and enabled.");
            if (!float.IsFinite(config.FadeSeconds) || config.FadeSeconds <= 0)
                throw new InvalidDataException("FadeSeconds must be positive and finite.");

            var areas = new Dictionary<string, ResidentArea>(StringComparer.Ordinal)
            {
                { "exterior", exterior }
            };
            var spawns = new Dictionary<string, Dictionary<string, AreaSpawn>>(StringComparer.Ordinal);
            var doors = new Dictionary<string, List<Door>>(StringComparer.Ordinal);
            foreach (AreaDefinition definition in config.Areas)
            {
                ResidentArea area;
                if (definition.Id == "exterior")
                    area = exterior;
                else
                {
                    area = new ResidentArea(definition.Id, RectangleFrom(definition.Floor),
                        definition.WallThickness);
                    areas.Add(definition.Id, area);
                }
                var areaSpawns = new Dictionary<string, AreaSpawn>(StringComparer.Ordinal);
                foreach (SpawnDefinition spawn in definition.Spawns)
                {
                    Vector2 ground = VectorFrom(spawn.Ground);
                    Vector2 facing = VectorFrom(spawn.Facing);
                    if (facing == Vector2.Zero)
                        throw new InvalidDataException("Spawn facing must be nonzero.");
                    if (area.BlocksMovement(player.BoundsAt(player.PositionForGround(ground))))
                        throw new InvalidDataException("Spawn is blocked: " + definition.Id + "/" + spawn.Id);
                    areaSpawns.Add(spawn.Id, new AreaSpawn(ground, facing));
                }
                spawns.Add(definition.Id, areaSpawns);
                doors.Add(definition.Id, new List<Door>());
            }
            if (!spawns.ContainsKey("exterior"))
                throw new InvalidDataException("An exterior area with a return spawn is required.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (DoorDefinition definition in config.Doors)
            {
                if (!ids.Add(definition.Id))
                    throw new InvalidDataException("Duplicate door: " + definition.Id);
                var door = new Door(definition.Id, RectangleFrom(definition.Trigger),
                    areas[definition.TargetArea], spawns[definition.TargetArea][definition.TargetSpawn],
                    definition.IsExit);
                doors[definition.SourceArea].Add(door);
            }
            var residents = new ResidentArea[areas.Count];
            int index = 0;
            foreach (var entry in areas)
            {
                entry.Value.SetDoors(doors[entry.Key].ToArray());
                residents[index++] = entry.Value;
            }

            // Reserve approach and spawn cells before random scenery/pickups are placed.
            // They remain walkable; existing building collision is never cleared.
            foreach (DoorDefinition definition in config.Doors)
                if (definition.SourceArea == "exterior")
                    Reserve(exteriorGrid, RectangleFrom(definition.AccessBounds));
            foreach (AreaSpawn spawn in spawns["exterior"].Values)
                Reserve(exteriorGrid, player.BoundsAt(player.PositionForGround(spawn.GroundPosition)));

            player.Input.Interact = config.InteractionKey;
            player.Input.Inventory = config.InventoryKey;
            // Rooms use the already-loaded pixel texture. Build them once at startup:
            // no ContentManager or GPU work, disk I/O, or allocations during a fade.
            return new DoorSystem(player, residents, exterior, config.FadeSeconds, logError);
        }
        catch (Exception error)
        {
            logError("House interior preload failed; exterior control remains available. " + error);
            exterior.SetDoors(Array.Empty<Door>());
            return new DoorSystem(player, new[] { exterior }, exterior, .25f, logError);
        }
    }

    private static void Reserve(WorldGrid grid, Rectangle bounds)
    {
        Point first = grid.WorldToCell(new Vector2(bounds.Left, bounds.Top));
        Point last = grid.WorldToCell(new Vector2(bounds.Right - 1, bounds.Bottom - 1));
        for (int x = first.X; x <= last.X; x++)
            for (int y = first.Y; y <= last.Y; y++)
            {
                var cell = new Point(x, y);
                if (grid.IsValidCell(cell) && grid.CanPlace(cell))
                    grid.Occupy(cell, CellType.Building, blocksMovement: false);
            }
    }

    private static Rectangle RectangleFrom(int[] values)
    {
        if (values == null || values.Length != 4 || values[2] <= 0 || values[3] <= 0)
            throw new InvalidDataException("Rectangles require [x, y, positive width, positive height].");
        return new Rectangle(values[0], values[1], values[2], values[3]);
    }

    private static Vector2 VectorFrom(float[] values)
    {
        if (values == null || values.Length != 2
            || !float.IsFinite(values[0]) || !float.IsFinite(values[1]))
            throw new InvalidDataException("Vectors require two finite numbers.");
        return new Vector2(values[0], values[1]);
    }

    public sealed class AreaDefinition
    {
        public string Id { get; set; }
        public int[] Floor { get; set; }
        public int WallThickness { get; set; } = 32;
        public SpawnDefinition[] Spawns { get; set; } = Array.Empty<SpawnDefinition>();
    }

    public sealed class SpawnDefinition
    {
        public string Id { get; set; }
        public float[] Ground { get; set; }
        public float[] Facing { get; set; }
    }

    public sealed class DoorDefinition
    {
        public string Id { get; set; }
        public string SourceArea { get; set; }
        public int[] Trigger { get; set; }
        public int[] AccessBounds { get; set; }
        public string TargetArea { get; set; }
        public string TargetSpawn { get; set; }
        public bool IsExit { get; set; }
    }
}
````

### First_game/Content/doors.json

````json
{
  "InteractionKey": "E",
  "InventoryKey": "Tab",
  "FadeSeconds": 0.25,
  "Areas": [
    {
      "Id": "exterior",
      "Spawns": [
        {
          "Id": "front-step",
          "Ground": [
            950,
            675
          ],
          "Facing": [
            0,
            1
          ]
        }
      ]
    },
    {
      "Id": "house",
      "Floor": [
        0,
        0,
        1000,
        700
      ],
      "WallThickness": 32,
      "Spawns": [
        {
          "Id": "entry",
          "Ground": [
            500,
            480
          ],
          "Facing": [
            0,
            -1
          ]
        }
      ]
    }
  ],
  "Doors": [
    {
      "Id": "front-door",
      "SourceArea": "exterior",
      "Trigger": [
        870,
        500,
        160,
        90
      ],
      "AccessBounds": [
        770,
        500,
        360,
        300
      ],
      "TargetArea": "house",
      "TargetSpawn": "entry",
      "IsExit": false
    },
    {
      "Id": "house-exit",
      "SourceArea": "house",
      "Trigger": [
        425,
        588,
        150,
        80
      ],
      "TargetArea": "exterior",
      "TargetSpawn": "front-step",
      "IsExit": true
    }
  ]
}
````

### First_game/UI/InventoryPanel.cs

````csharp
using System;
using First_game.Input;
using MonoGameLibrary.Input;
using First_game.Inventory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PlayerInventory = First_game.Inventory.Inventory;

namespace First_game.UI;

/// <summary>Shows every inventory item; its first row is the live hotbar.</summary>
public sealed class InventoryPanel : IMenuPanel
{
    private readonly PlayerInventory inventory;
    private readonly Hotbar hotbar;
    private readonly Texture2D background;
    private readonly ItemSlotRenderer slots;
    private readonly InventoryDragController drag;
    private readonly InputBindings bindings;
    private const string DragHint = "Release over a slot to move. Right-click to cancel.";
    private readonly Vector2 dragHintSize;
    private Vector2 closeHintSize;
    private Vector2 pageCaptionSize;

    public string CloseHint { get; private set; }
    public SpriteFont CaptionFont => slots?.CaptionFont;

    // The door prompt uses the exact footer scale, including narrow-window fitting.
    public float GetHintScale(Viewport viewport) => ItemSlotRenderer.GetCaptionScale(
        closeHintSize, InventoryLayout.ForInventory(viewport).Bounds.Width);
    private int hoveredSlot = -1;
    private int page;
    private string pageCaption;
    private int captionPageCount;

    private int PageCount => Math.Max(1, (int)Math.Ceiling(
        (inventory.Capacity - Hotbar.SlotCount) / (float)InventoryLayout.StorageSlotsPerPage));

    public InventoryPanel(PlayerInventory inventory, Hotbar hotbar,
        Texture2D background, ItemSlotRenderer slots, InventoryDragController drag = null,
        InputBindings bindings = null)
    {
        this.inventory = inventory;
        this.hotbar = hotbar;
        this.background = background;
        this.slots = slots;
        this.drag = drag ?? new InventoryDragController();
        this.bindings = bindings ?? new InputBindings();
        this.bindings.InventoryChanged += RefreshCloseHint;
        dragHintSize = CaptionFont?.MeasureString(DragHint) ?? Vector2.Zero;
        RefreshCloseHint();
        UpdatePageCaption();
    }

    public void Update(GameTime gameTime, UIInput input, Viewport viewport)
    {
        if (captionPageCount != PageCount) UpdatePageCaption();
        int pressedSlot = HotbarInput.GetPressedSlot(input);
        if (pressedSlot >= 0)
            hotbar.SelectSlot(pressedSlot);

        // Legacy upgraded inventories can page their storage rows.
        int previousPage = page;
        if (input.Pressed(Keys.PageDown)) page = Math.Min(PageCount - 1, page + 1);
        if (input.Pressed(Keys.PageUp)) page = Math.Max(0, page - 1);
        bool changedPage = page != previousPage;
        if (changedPage)
        {
            drag.Cancel();
            UpdatePageCaption();
        }

        int visibleSlot = InventoryLayout.ForInventory(viewport).HitTest(input.Mouse.ScreenPosition);
        hoveredSlot = visibleSlot < 0 ? -1 : InventoryLayout.GetInventorySlot(visibleSlot, page);
        if (hoveredSlot >= inventory.Capacity) hoveredSlot = -1;

        if (!changedPage)
        {
            if ((input.Mouse.LeftClicked || (input.Mouse.LeftReleased && drag.IsDragging))
                && hoveredSlot >= 0 && hoveredSlot < Hotbar.SlotCount)
                hotbar.SelectSlot(hoveredSlot);
            drag.Update(input, inventory, hoveredSlot);
        }
    }

    public void OnClosed()
    {
        drag.Cancel();
        // hoveredSlot = -1;
    }

    public void Draw(SpriteBatch batch, Viewport viewport)
    {
        InventoryLayout layout = InventoryLayout.ForInventory(viewport);
        batch.Draw(background, layout.Bounds, Color.White);

        for (int visibleSlot = 0; visibleSlot < InventoryLayout.VisibleSlots; visibleSlot++)
        {
            int inventorySlot = InventoryLayout.GetInventorySlot(visibleSlot, page);
            if (inventorySlot >= inventory.Capacity) continue;
            slots.Draw(batch, layout.GetSlotBounds(visibleSlot), drag.GetDisplayedItem(inventory, inventorySlot),
                visibleSlot < Hotbar.SlotCount ? visibleSlot : -1);
        }

        string hint = drag.IsDragging ? DragHint : CloseHint;
        Vector2 hintSize = drag.IsDragging ? dragHintSize : closeHintSize;
        slots.DrawCaption(batch, hint, hintSize, new Vector2(layout.Bounds.Center.X, layout.Bounds.Bottom + 24),
            layout.Bounds.Width);
        if (PageCount > 1)
            slots.DrawCaption(batch, pageCaption, pageCaptionSize,
                new Vector2(layout.Bounds.Center.X, layout.Bounds.Bottom + 55), layout.Bounds.Width);
        if (hoveredSlot >= 0 && inventory.GetSlot(hoveredSlot) != null)
            slots.DrawCaption(batch, slots.GetItemName(inventory.GetSlot(hoveredSlot)),
                new Vector2(layout.Bounds.Center.X, layout.Bounds.Top - 24), layout.Bounds.Width);

        // Draw last in the screen-space batch so the cursor stack is above the entire panel.
        if (drag.IsDragging)
        {
            Rectangle size = layout.GetSlotBounds(0);
            var cursorBounds = new Rectangle(drag.CursorPosition.X - size.Width / 2,
                drag.CursorPosition.Y - size.Height / 2, size.Width, size.Height);
            slots.Draw(batch, cursorBounds, drag.DraggedItem);
        }
    }

    private void RefreshCloseHint()
    {
        CloseHint = "Drag items to move. Alt+drag: split half. " + bindings.Inventory + ": close.";
        closeHintSize = CaptionFont?.MeasureString(CloseHint) ?? Vector2.Zero;
    }

    private void UpdatePageCaption()
    {
        captionPageCount = PageCount;
        pageCaption = $"Storage page {page + 1}/{captionPageCount} - Page Up / Page Down";
        pageCaptionSize = CaptionFont?.MeasureString(pageCaption) ?? Vector2.Zero;
    }
}
````

### First_game/UI/ItemSlotRenderer.cs

````csharp
using System;
using First_game.Inventory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace First_game.UI;

/// <summary>Draws item icons, counts, key labels, and the supplied selection artwork.</summary>
public sealed class ItemSlotRenderer
{
    private static readonly Color Ink = new(65, 49, 30);
    private readonly ItemDefinitionRegistry definitions;
    private readonly Func<string, Texture2D> loadIcon;
    private readonly SpriteFont font;
    private readonly Texture2D highlight;

    public SpriteFont CaptionFont => font;
    public static float GetCaptionScale(Vector2 measuredSize, float maxWidth) =>
        Math.Min(0.65f, maxWidth / Math.Max(1, measuredSize.X));

    public ItemSlotRenderer(ItemDefinitionRegistry definitions, Func<string, Texture2D> loadIcon,
        SpriteFont font, Texture2D highlight)
    {
        this.definitions = definitions;
        this.loadIcon = loadIcon;
        this.font = font;
        this.highlight = highlight;
    }

    public void Draw(SpriteBatch batch, Rectangle bounds, ItemInstance item, int keySlot = -1,
        bool selected = false)
    {
        float textScale = Math.Min(0.65f, bounds.Width / 150f);
        float padding = Math.Max(3, bounds.Width * 0.1f);
        if (item != null)
        {
            ItemDefinition definition = definitions.GetRequired(item.QualifiedId);
            if (!string.IsNullOrWhiteSpace(definition.IconAsset))
            {
                Texture2D icon = loadIcon(definition.IconAsset);
                float scale = Math.Min(bounds.Width * 0.65f / icon.Width, bounds.Height * 0.65f / icon.Height);
                batch.Draw(icon, bounds.Center.ToVector2(), null, Color.White, 0,
                    new Vector2(icon.Width, icon.Height) / 2, scale, SpriteEffects.None, 0);
            }
            else
            {
                // Items without an icon still have a visible, identifiable slot.
                Vector2 size = font.MeasureString(definition.Name);
                float scale = Math.Min(textScale, bounds.Width * 0.8f / Math.Max(1, size.X));
                batch.DrawString(font, definition.Name, bounds.Center.ToVector2() - size * scale / 2,
                    Ink, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            }

            if (item.Count > 1)
            {
                Vector2 size = font.MeasureString(item.CountText) * textScale;
                batch.DrawString(font, item.CountText,
                    new Vector2(bounds.Right - padding, bounds.Bottom - padding) - size,
                    Ink, 0, Vector2.Zero, textScale, SpriteEffects.None, 0);
            }
        }

        // Draw the selected hotbar slot's frame above the bar and item icon.
        if (selected)
            batch.Draw(highlight, bounds, Color.White);
    }

    public void DrawCaption(SpriteBatch batch, string text, Vector2 center, float maxWidth)
    {
        DrawCaption(batch, text, font.MeasureString(text), center, maxWidth);
    }

    public void DrawCaption(SpriteBatch batch, string text, Vector2 size, Vector2 center, float maxWidth)
    {
        float scale = GetCaptionScale(size, maxWidth);
        Vector2 position = center - size * scale / 2;
        batch.DrawString(font, text, position + Vector2.One, Color.Black,
            0, Vector2.Zero, scale, SpriteEffects.None, 0);
        batch.DrawString(font, text, position, Color.White,
            0, Vector2.Zero, scale, SpriteEffects.None, 0);
    }

    public string GetItemName(ItemInstance item) =>
        item == null ? "Empty slot" : definitions.GetRequired(item.QualifiedId).Name;
}
````

### First_game/UI/DoorOverlay.cs

````csharp
using First_game.Doors;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace First_game.UI;

public sealed class DoorOverlay
{
    private readonly DoorSystem doors;
    private readonly Texture2D pixel;
    private readonly SpriteFont font;
    private readonly InventoryPanel inventoryPanel;
    private string prompt;
    private Vector2 promptSize;

    public DoorOverlay(DoorSystem doors, Texture2D pixel, InventoryPanel inventoryPanel)
    {
        this.doors = doors;
        this.pixel = pixel;
        this.inventoryPanel = inventoryPanel;
        font = inventoryPanel.CaptionFont;
        doors.PromptChanged += SetPrompt;
        SetPrompt(doors.Prompt);
    }

    private void SetPrompt(string value)
    {
        if (ReferenceEquals(prompt, value)) return;
        prompt = value;
        promptSize = value == null ? Vector2.Zero : font.MeasureString(value);
    }

    public void Draw(SpriteBatch batch, Viewport viewport)
    {
        if (prompt != null)
        {
            float scale = inventoryPanel.GetHintScale(viewport);
            Vector2 scaledSize = promptSize * scale;
            Vector2 position = new Vector2((viewport.Width - scaledSize.X) * .5f, 36);
            batch.Draw(pixel, new Rectangle((int)position.X - 12, 28,
                (int)scaledSize.X + 24, (int)scaledSize.Y + 16), Color.Black * .8f);
            batch.DrawString(font, prompt, position + Vector2.One, Color.Black,
                0, Vector2.Zero, scale, SpriteEffects.None, 0);
            batch.DrawString(font, prompt, position, Color.White,
                0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }
        if (doors.FadeOpacity > 0)
            batch.Draw(pixel, viewport.Bounds, Color.Black * doors.FadeOpacity);
    }
}
````

### First_game/UI/CraftingPanel.cs

````csharp
using MonoGameLibrary.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace First_game.UI;

public sealed class CraftingPanel : IMenuPanel
{
    private readonly SpriteFont font;

    private readonly InputBindings bindings;
    private string text;
    private Vector2 textSize;

    public CraftingPanel(SpriteFont font, InputBindings bindings = null)
    {
        this.font = font;
        this.bindings = bindings ?? new InputBindings();
        this.bindings.InventoryChanged += RefreshText;
        RefreshText();
    }

    private void RefreshText()
    {
        text = "Crafting\nNo recipes yet.\n\n" + bindings.Inventory + ": Inventory    C / Esc: Close";
        textSize = font?.MeasureString(text) ?? Vector2.Zero;
    }

    public void Update(GameTime gameTime, UIInput input, Viewport viewport)
    {
        // Add recipe selection here, then ask a crafting system to craft.
    }

    public void Draw(SpriteBatch spriteBatch, Viewport viewport)
    {
        Vector2 position = (new Vector2(viewport.Width, viewport.Height)
            - textSize) / 2f;
        spriteBatch.DrawString(font, text, position, Color.White);
    }
}
````

### First_game/Game1.cs

````csharp
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
    private readonly List<Sprite> trees = new List<Sprite>();
    private readonly List<Sprite> pines = new List<Sprite>();
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
        hotbar = new Hotbar(inventory);
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
        mouseInteractions = new MouseInteractionController(camera, pickupSystem, inventory, mouseInput);
        fishing = new FishingController(cat, inventory);
        fishing.Register(new FishingSpot(worldGrid, Lake.GetOccupiedCells(), "(O)fish", new FishingSettings()));
        fishingOverlay = new FishingOverlay(gridPixel);
        mouseInteractions.TryInteract = fishing.TryInteract;

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

        // A right-click may have started an action after the door update.
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
            foreach (Sprite tree in trees)
                worldRenderer.Submit(tree.SortY, tree.Draw);
            foreach (Sprite pine in pines)
                worldRenderer.Submit(pine.SortY, pine.Draw);
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
````

### Tests/GameplayChecks/InventoryDragChecks.cs

````csharp
using System;
using First_game.Input;
using First_game.Inventory;
using First_game.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary.Input;

static class InventoryDragChecks
{
    public static void Run(Action<bool, string> check)
    {
        var h = new Harness();
        h.Items.AddItem("(O)wood", 9);
        ItemInstance original = h.Items.GetSlot(0);
        h.Frame(0, true);
        check(h.Drag.IsDragging && ReferenceEquals(h.Drag.DraggedItem, original)
            && h.Drag.GetDisplayedItem(h.Items, 0) == null,
            "mouse-down picks up the full stack and hides the source");
        h.Frame(12, true);
        check(h.Items.GetSlot(12) == null && h.Drag.IsDragging
            && h.Drag.CursorPosition == InventoryLayout.ForInventory(h.Viewport).GetSlotBounds(12).Center,
            "holding and moving follows the cursor without committing");
        h.Frame(12, false);
        check(!h.Drag.IsDragging && h.Items.GetSlot(0) == null
            && ReferenceEquals(h.Items.GetSlot(12), original),
            "mouse-up commits the full stack once");
        h.Frame(15, false);
        check(h.Items.GetSlot(15) == null && h.Items.GetItemCount("(O)wood") == 9,
            "repeated mouse-up cannot repeat a transfer");
        h.Frame(12, true);
        h.Frame(12, false);
        h.Frame(15, true);
        h.Frame(15, false);
        check(h.Items.GetSlot(15) == null && h.Items.GetSlot(12).Count == 9,
            "click-then-click does not move items");

        h = new Harness();
        h.Items.AddItem("(O)wood", 9);
        original = h.Items.GetSlot(0);
        h.Frame(0, true, Keys.LeftAlt);
        ItemInstance preview = h.Drag.DraggedItem;
        check(h.Drag.IsSplit && preview.Count == 4 && h.Drag.GetDisplayedItem(h.Items, 0).Count == 5,
            "Alt-drag rounds down and immediately shows the remaining source count");
        var saved = Inventory.LoadFromJson(h.Items.SaveToJson(), h.Definitions, h.Behaviors);
        check(saved.GetItemCount("(O)wood") == 9, "saving mid-drag retains the complete authoritative stack");
        h.Frame(1, true);
        check(h.Drag.IsSplit && ReferenceEquals(h.Drag.DraggedItem, preview),
            "releasing Alt mid-drag preserves split mode and its preview");
        h.Frame(1, false);
        check(ReferenceEquals(h.Items.GetSlot(0), original) && original.Count == 5
            && h.Items.GetSlot(1).Count == 4 && !ReferenceEquals(h.Items.GetSlot(1), preview),
            "split drop commits real items without inserting the visual preview");

        foreach (bool split in new[] { false, true })
        {
            foreach (int destination in new[] { -1, 0, 30 })
            {
                h = new Harness();
                h.Items.AddItem("(O)wood", 9);
                original = h.Items.GetSlot(0);
                h.Drag.BeginDrag(h.Items, 0, split);
                check(!h.Drag.Drop(h.Items, destination) && !h.Drag.IsDragging
                    && ReferenceEquals(h.Items.GetSlot(0), original) && original.Count == 9,
                    $"same/invalid drop restores source (split={split}, slot={destination})");
            }
            h = new Harness();
            h.Items.AddItem("(O)wood", 9);
            h.Frame(0, true, split ? Keys.RightAlt : Keys.None);
            h.Frame(-1, false);
            check(!h.Drag.IsDragging && h.Items.GetSlot(0).Count == 9,
                "pointer release outside inventory snaps back, split=" + split);

            foreach (Keys closeKey in new[] { Keys.Escape, Keys.Tab, Keys.P, Keys.C })
            {
                h = new Harness();
                h.Items.AddItem("(O)wood", 9);
                h.Frame(0, true, split ? Keys.LeftAlt : Keys.None);
                h.Frame(1, false, closeKey);
                check(!h.Drag.IsDragging && h.Items.GetSlot(0).Count == 9 && h.Items.GetSlot(1) == null
                    && h.Menus.ConsumedInputThisFrame,
                    $"closing/switching on release cancels (split={split}, key={closeKey})");
                h.Menus.Open(MenuType.Inventory);
                h.Frame(2, true);
                h.Frame(2, false);
                check(h.Items.GetSlot(2) == null, "reopened inventory cannot commit an old gesture");
            }
        }

        h = new Harness();
        h.Items.AddItem("(O)wood", 118); // 99,19 -> 20,98
        h.Items.SplitStack(0, 2, 79);
        h.Items.MoveItem(2, 1);
        h.Frame(0, true, Keys.LeftAlt);
        h.Frame(1, false);
        check(h.Items.GetSlot(0).Count == 19 && h.Items.GetSlot(1).Count == 99
            && h.Items.GetItemCount("(O)wood") == 118,
            "partial split merge fills destination and returns all overflow to source");
        h.Frame(0, true, Keys.LeftAlt);
        h.Frame(1, false);
        check(h.Items.GetSlot(0).Count == 19 && h.Items.GetSlot(1).Count == 99,
            "split onto a full compatible stack cancels without swapping");
        h.Frame(0, true);
        h.Frame(1, false);
        check(h.Items.GetSlot(0).Count == 99 && h.Items.GetSlot(1).Count == 19,
            "full drag preserves existing MoveItem full-destination swap behavior");

        h = new Harness();
        h.Items.AddItem("(O)wood", 9);
        h.Items.SplitStack(0, 1, 2);
        h.Frame(0, true, Keys.LeftAlt);
        h.Frame(1, false);
        check(h.Items.GetSlot(0).Count == 4 && h.Items.GetSlot(1).Count == 5,
            "split merges its entire half when destination has space");

        foreach (int mismatch in new[] { 0, 1, 2 })
        {
            h = new Harness();
            h.Items.AddItem("(O)wood", 9);
            h.Items.AddItem(mismatch == 0 ? "(O)fish" : "(O)wood", 3,
                quality: mismatch == 1 ? 1 : 0, durability: mismatch == 2 ? 10 : null);
            original = h.Items.GetSlot(0);
            ItemInstance destination = h.Items.GetSlot(1);
            h.Frame(0, true, Keys.LeftAlt);
            h.Frame(1, false);
            check(ReferenceEquals(h.Items.GetSlot(0), original) && original.Count == 9
                && ReferenceEquals(h.Items.GetSlot(1), destination) && destination.Count == 3,
                "split rejects incompatible ID/quality/durability case " + mismatch);
            h.Frame(0, true);
            h.Frame(1, false);
            check(ReferenceEquals(h.Items.GetSlot(1), original) && ReferenceEquals(h.Items.GetSlot(0), destination),
                "full drag swaps incompatible ID/quality/durability case " + mismatch);
        }

        h = new Harness();
        h.Items.AddItem("(T)iron_pickaxe", 1, quality: 2, durability: 17);
        h.Frame(0, true, Keys.LeftAlt);
        check(!h.Drag.IsDragging, "Alt on a singleton does not start an empty split");
        h.Frame(1, false);
        original = h.Items.GetSlot(0);
        h.Frame(0, true);
        h.Frame(1, false);
        check(ReferenceEquals(h.Items.GetSlot(1), original) && original.Quality == 2 && original.Durability == 17,
            "full tool drag preserves reference, quality, and durability");

        h = new Harness();
        h.Items.AddItem("(O)wood", h.Items.Capacity * 99);
        h.Frame(0, true, Keys.RightAlt);
        check(h.Drag.IsSplit && h.Drag.DraggedItem.Count == 49,
            "split preview needs no spare inventory slot and accepts right Alt");
        h.Frame(1, true, right: true);
        check(!h.Drag.IsDragging && h.Items.GetSlot(0).Count == 99,
            "right-click cancels a split in a full inventory");
        h.Frame(2, true);
        h.Frame(2, false);
        check(h.Items.GetItemCount("(O)wood") == h.Items.Capacity * 99,
            "continued hold after cancellation never starts another drag");

        h = new Harness(36);
        h.Items.AddItem("(O)wood", 9);
        h.Frame(0, true, Keys.LeftAlt);
        h.Frame(12, true, Keys.PageDown);
        h.Frame(12, false);
        check(!h.Drag.IsDragging && h.Items.GetSlot(0).Count == 9 && h.Items.GetSlot(32) == null,
            "changing storage pages cancels the drag");

        h = new Harness();
        h.Items.AddItem("(O)wood", 9);
        h.Frame(-1, true);
        h.Frame(0, true);
        h.Frame(1, false);
        check(h.Items.GetSlot(1) == null && !h.Drag.IsDragging,
            "press outside then enter a slot while held cannot start a drag");
        h.Frame(0, true, Keys.LeftAlt);
        h.Items.RemoveItem("(O)wood", 1);
        h.Frame(1, false);
        check(h.Items.GetSlot(0).Count == 8 && h.Items.GetSlot(1) == null,
            "source count changed externally cancels without resurrecting items");
        h.Frame(0, true);
        h.Items.MoveItem(0, 2);
        h.Items.AddItem("(O)fish", 3);
        h.Frame(1, false);
        check(h.Items.GetSlot(1) == null && h.Items.GetSlot(0).QualifiedId == "(O)fish"
            && h.Items.GetSlot(2).Count == 8,
            "source replacement cancels without moving the replacement item");

        h = new Harness();
        h.Items.AddItem("(O)wood", 9, quality: 1, durability: 20);
        h.Frame(0, true, Keys.LeftAlt);
        h.Items.GetSlot(0).SetDurability(19);
        h.Frame(1, false);
        check(h.Items.GetSlot(0).Count == 9 && h.Items.GetSlot(1) == null,
            "source durability changed during split cancels stale preview");

        h = new Harness();
        var chest = new StorageChest(h.Definitions, h.Behaviors);
        h.Items.AddItem("(O)wood", 20);
        chest.Contents.AddItem("(O)wood", 98);
        h.Drag.BeginDrag(h.Items, 0, true);
        check(h.Drag.Drop(chest.Contents, 0) && h.Items.GetSlot(0).Count == 19
            && chest.Contents.GetSlot(0).Count == 99,
            "split merges across player/chest inventories even with equal slot indexes");
        h.Drag.BeginDrag(chest.Contents, 0, true);
        check(h.Drag.Drop(h.Items, 1) && h.Items.GetSlot(1).Count == 49
            && chest.Contents.GetSlot(0).Count == 50,
            "chest split can return to an empty player slot");
        h.Items.AddItem("(O)fish", 3);
        original = h.Items.GetSlot(2);
        h.Drag.BeginDrag(h.Items, 2, false);
        check(h.Drag.Drop(chest.Contents, 0) && ReferenceEquals(chest.Contents.GetSlot(0), original)
            && h.Items.GetSlot(2).Count == 50, "full cross-inventory drag swaps incompatible stacks");
        h.Drag.BeginDrag(chest.Contents, 0, false);
        check(h.Drag.Drop(h.Items, 3) && chest.Contents.GetSlot(0) == null
            && ReferenceEquals(h.Items.GetSlot(3), original), "full chest drag moves to an empty player slot");

        foreach (bool split in new[] { false, true })
        {
            h = new Harness();
            h.Items.AddItem("(O)wood", 9);
            h.Frame(0, true, split ? Keys.LeftAlt : Keys.None);
            var input = new UIInput(default, default, h.Mouse);
            var time = new GameTime();
            // Warm up every path before measuring only held-input updates and preview reads.
            h.Mouse.Update(Harness.Sample(new Point(100, 100), true, false));
            for (int frame = 0; frame < 100; frame++) h.Panel.Update(time, input, h.Viewport);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 0; frame < 1000; frame++)
            {
                h.Mouse.Update(Harness.Sample(new Point(100 + frame % 300, 100), true, false));
                h.Panel.Update(time, input, h.Viewport);
                _ = h.Drag.DraggedItem.CountText;
                _ = h.Drag.GetDisplayedItem(h.Items, 0)?.CountText;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            check(allocated == 0, $"held drag updates allocate zero bytes (split={split}, bytes={allocated})");
        }
    }

    private sealed class Harness
    {
        public readonly ItemDefinitionRegistry Definitions = SampleItemCatalog.CreateDefinitions();
        public readonly ItemCategoryBehaviorRegistry Behaviors = SampleItemCatalog.CreateBehaviors();
        public readonly Inventory Items;
        public readonly InventoryDragController Drag = new();
        public readonly InventoryPanel Panel;
        public readonly UIManager Menus = new(null);
        public readonly MouseInput Mouse = new();
        public readonly Viewport Viewport = new(0, 0, 1280, 720);
        private KeyboardState previousKeyboard;

        public Harness(int capacity = Inventory.PlayerCapacity)
        {
            Items = new Inventory(Definitions, Behaviors, capacity);
            Panel = new InventoryPanel(Items, new Hotbar(Items), null, null, Drag);
            Menus.Register(MenuType.Inventory, Panel);
            Menus.Register(MenuType.Pause, new PauseMenu(null));
            Menus.Register(MenuType.Crafting, new CraftingPanel(null));
            Menus.Open(MenuType.Inventory);
        }

        public void Frame(int visibleSlot, bool down, Keys key = Keys.None, bool right = false)
        {
            Point position = visibleSlot < 0 ? Point.Zero
                : InventoryLayout.ForInventory(Viewport).GetSlotBounds(visibleSlot).Center;
            Mouse.Update(Sample(position, down, right));
            var keyboard = key == Keys.None ? default : new KeyboardState(key);
            Menus.Update(new GameTime(), new UIInput(keyboard, previousKeyboard, Mouse), Viewport);
            previousKeyboard = keyboard;
        }

        public static MouseState Sample(Point position, bool down, bool right) =>
            new(position.X, position.Y, 0, down ? ButtonState.Pressed : ButtonState.Released,
                ButtonState.Released, right ? ButtonState.Pressed : ButtonState.Released,
                ButtonState.Released, ButtonState.Released);
    }
}
````

### Tests/GameplayChecks/InventoryChecks.cs

````csharp
using System;
using First_game.Input;
using First_game.Inventory;
using First_game.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary.Input;

static class InventoryChecks
{
    public static void Run(Action<bool, string> check)
    {
        var definitions = SampleItemCatalog.CreateDefinitions();
        var behaviors = SampleItemCatalog.CreateBehaviors();
        definitions.ResolveUseEffects(UseEffectRegistry.CreateBuiltIns(), behaviors);
        var inventory = new Inventory(definitions, behaviors, Inventory.PlayerCapacity);
        var hotbar = new Hotbar(inventory);
        var bindings = new InputBindings();
        var panel = new InventoryPanel(inventory, hotbar, null, null, bindings: bindings);
        var viewport = new Viewport(0, 0, 1280, 720);
        var mouse = new MouseInput();
        void Pointer(int slot, bool down)
        {
            Point position = InventoryLayout.ForInventory(viewport).GetSlotBounds(slot).Center;
            mouse.Update(new MouseState(position.X, position.Y, 0,
                down ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released,
                ButtonState.Released, ButtonState.Released, ButtonState.Released));
            panel.Update(new GameTime(), new UIInput(default, default, mouse), viewport);
        }
        check(inventory.Capacity == 30 && hotbar.SelectedItem == null, "new player has 30 slots and an empty selected slot");

        Keys[] keys = { Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9, Keys.D0 };
        Keys[] numpad = { Keys.NumPad1, Keys.NumPad2, Keys.NumPad3, Keys.NumPad4, Keys.NumPad5,
            Keys.NumPad6, Keys.NumPad7, Keys.NumPad8, Keys.NumPad9, Keys.NumPad0 };
        for (int slot = 0; slot < 10; slot++)
        {
            check(HotbarInput.GetPressedSlot(new UIInput(new KeyboardState(keys[slot]), default, null)) == slot
                && HotbarInput.GetPressedSlot(new UIInput(new KeyboardState(numpad[slot]), default, null)) == slot,
                "number key and numpad select slot " + (slot + 1));
        }
        check(HotbarInput.GetPressedSlot(new UIInput(new KeyboardState(Keys.D0), new KeyboardState(Keys.D0), null)) == -1,
            "holding a key does not repeat selection");
        check(HotbarInput.GetPressedSlot(new UIInput(new KeyboardState(Keys.E), default, null)) == -1,
            "unrelated keys do not select a slot");

        inventory.AddItem("(O)wood", 12);
        inventory.MoveItem(0, 29);
        Pointer(29, true);
        Pointer(9, false);
        check(hotbar.SelectedSlot == 9 && hotbar.SelectedItem == inventory.GetSlot(9)
            && hotbar.SelectedItem.Count == 12 && inventory.GetSlot(29) == null,
            "last storage slot moves into hotbar without duplicating items");

        inventory.AddItem("(O)blackberry", 2);
        Pointer(0, true);
        Pointer(9, false);
        check(hotbar.SelectedItem.QualifiedId == "(O)blackberry" && inventory.GetSlot(0).QualifiedId == "(O)wood",
            "swapping hotbar items immediately changes the selected item");
        check(!hotbar.TryUseSelectedItem(null) && hotbar.SelectedItem.Count == 2,
            "failed use preserves selected stack");
        check(hotbar.TryUseSelectedItem(new UseContext()) && inventory.GetSlot(9).Count == 1,
            "using selected item updates the shared inventory stack");
        check(hotbar.TryUseSelectedItem(new UseContext()) && hotbar.SelectedItem == null,
            "consuming last item leaves selected slot empty");

        inventory.SplitStack(0, 10, 5);
        Pointer(10, true);
        Pointer(0, false);
        check(inventory.GetSlot(0).Count == 12 && inventory.GetSlot(10) == null,
            "matching stacks merge across storage and hotbar");
        inventory.AddItem("(O)wood", 180); // 99, 93: merge only the six items that fit.
        Pointer(0, true);
        Pointer(1, false);
        check(inventory.GetSlot(0).Count == 93 && inventory.GetSlot(1).Count == 99
            && inventory.GetItemCount("(O)wood") == 192, "partial merge keeps all leftover items");

        // Closing or replacing a menu must cancel any pending item transfer.
        var menus = new UIManager(null, bindings);
        menus.Register(MenuType.Inventory, panel);
        menus.Register(MenuType.Pause, new PauseMenu(null));
        menus.Open(MenuType.Inventory);
        Pointer(0, true);
        menus.Open(MenuType.Pause);
        menus.Open(MenuType.Inventory);
        Pointer(20, false);
        check(inventory.GetSlot(20) == null && inventory.GetSlot(0).Count == 93,
            "switching menus cancels pending move without losing items");
        Pointer(0, true);
        menus.Close();
        menus.Open(MenuType.Inventory);
        Pointer(20, false);
        check(inventory.GetSlot(20) == null, "closing inventory cancels pending move");

        menus.Update(new GameTime(), new UIInput(new KeyboardState(Keys.D0), default, mouse), viewport);
        check(hotbar.SelectedSlot == 9, "number keys select while inventory is open");
        menus.Open(MenuType.Pause);
        menus.Update(new GameTime(), new UIInput(new KeyboardState(Keys.D1), default, mouse), viewport);
        check(hotbar.SelectedSlot == 9 && menus.ConsumedInputThisFrame, "pause blocks hotbar selection");
        menus.Update(new GameTime(), new UIInput(new KeyboardState(Keys.Escape), default, mouse), viewport);
        check(menus.ActiveMenu == MenuType.None && menus.ConsumedInputThisFrame,
            "menu closing frame still blocks world input");

        var time = new GameTime();
        var tab = new KeyboardState(Keys.Tab);
        menus.Update(time, new UIInput(tab, default, mouse), viewport);
        check(menus.ActiveMenu == MenuType.Inventory && panel.CloseHint.EndsWith("Tab: close."),
            "default Tab opens inventory and matches the cached close hint");
        menus.Update(time, new UIInput(tab, tab, mouse), viewport);
        check(menus.ActiveMenu == MenuType.Inventory, "held Tab does not close inventory");
        menus.Update(time, new UIInput(new KeyboardState(Keys.E), default, mouse), viewport);
        check(menus.ActiveMenu == MenuType.Inventory && menus.ConsumedInputThisFrame,
            "E leaves inventory open and remains consumed by the menu");
        menus.Update(time, new UIInput(new KeyboardState(Keys.I), default, mouse), viewport);
        check(menus.ActiveMenu == MenuType.Inventory, "old I binding no longer toggles inventory");
        menus.Update(time, new UIInput(default, tab, mouse), viewport);
        menus.Update(time, new UIInput(tab, default, mouse), viewport);
        check(menus.ActiveMenu == MenuType.None && menus.ConsumedInputThisFrame,
            "fresh Tab closes inventory and consumes the closing frame");
        menus.Update(time, new UIInput(tab, tab, mouse), viewport);
        check(menus.ActiveMenu == MenuType.None, "held Tab does not reopen inventory");
        bindings.Inventory = Keys.O;
        check(panel.CloseHint.EndsWith("O: close."), "rebinding refreshes the inventory hint");
        string cachedHint = panel.CloseHint;
        bindings.Inventory = Keys.O;
        check(ReferenceEquals(cachedHint, panel.CloseHint), "unchanged binding preserves cached hint");
        menus.Update(time, new UIInput(tab, default, mouse), viewport);
        check(menus.ActiveMenu == MenuType.None, "old default stops toggling after rebind");
        menus.Update(time, new UIInput(new KeyboardState(Keys.O), default, mouse), viewport);
        check(menus.ActiveMenu == MenuType.Inventory, "configured inventory key toggles the menu");
        menus.Close();
        bindings.Inventory = Keys.Tab;

        var restored = Inventory.LoadFromJson(inventory.SaveToJson(), definitions, behaviors);
        check(restored.Capacity == 30 && restored.GetSlot(0).Count == 93 && restored.GetSlot(1).Count == 99,
            "30-slot save round trip preserves slot positions and counts");
        foreach (int capacity in new[] { 12, 24, 36 })
        {
            var legacy = new Inventory(definitions, behaviors, capacity);
            check(Inventory.LoadFromJson(legacy.SaveToJson(), definitions, behaviors).Capacity == capacity,
                "legacy inventory capacity still loads: " + capacity);
        }

        foreach (var size in new[] { new Point(800, 600), new Point(1280, 720), new Point(1920, 1080), new Point(3440, 1440) })
        {
            var screen = new Viewport(0, 0, size.X, size.Y);
            var layout = InventoryLayout.ForInventory(screen);
            var bar = InventoryLayout.ForHotbar(screen);
            bool valid = screen.Bounds.Contains(layout.Bounds) && screen.Bounds.Contains(bar.Bounds);
            for (int slot = 0; slot < 30; slot++)
                valid &= layout.Bounds.Contains(layout.GetSlotBounds(slot))
                    && layout.HitTest(layout.GetSlotBounds(slot).Center) == slot;
            for (int slot = 0; slot < 10; slot++)
                valid &= bar.Bounds.Contains(bar.GetSlotBounds(slot))
                    && bar.HitTest(bar.GetSlotBounds(slot).Center) == slot;
            check(valid && layout.HitTest(Point.Zero) == -1, "slot drawing and hit testing agree at " + size);
        }
        check(InventoryLayout.GetInventorySlot(9, 1) == 9 && InventoryLayout.GetInventorySlot(15, 1) == 35,
            "legacy storage paging preserves the top row and reaches slot 36");
    }

    private sealed class UseContext : IItemUseContext
    {
        public bool RestoreVitals(float health, float stamina) => true;
        public bool ApplyBuff(string buffId, float durationSeconds, float magnitude) => false;
        public bool PlaceWorldObject(string objectId) => false;
        public bool PerformToolAction(string actionId, ItemInstance tool) => false;
    }
}
````

### First_game/UI/README.md

````markdown
# Inventory, hotbar, and menus

## Controls

- **1, 2, 3, 4, 5, 6, 7, 8, 9, 0:** select hotbar slots 1 through 10. Numpad keys also work.
- **Left-click the hotbar:** select that slot.
- **E:** interact with an in-range door.
- **Tab:** open or close the inventory. Its top row contains the same ten slots as the hotbar.
- **Inside the inventory:** hold the left mouse button on an item, drag, and release over a destination. Compatible stacks merge; different items swap.
- **Alt+drag:** move half a stack, rounded down (a single item cannot split). The source immediately displays the remaining half. Split drops merge up to the limit; excess stays at the source. Incompatible or full destinations cancel the split.
- **Right-click, release outside the slots, or release over the source:** cancel the drag. Closing/switching menus, changing pages, and losing window focus also cancel.
- **C:** crafting. **P:** pause. **Escape:** close an open menu; from the world, exit the game.

The player has 30 slots: ten in the hotbar and twenty below it. Collected items first fill compatible stacks, then empty slots from left to right, starting in the hotbar. Empty hotbar slots can be selected. Number keys select items without consuming them.

The supplied `Hudbar.png`, `Highlight.png`, and `Inventory.png` are used directly. The UI scales with the viewport. Stack counts, slot key labels, and the selected item's name are drawn over/near the artwork.

The selection highlight appears only on the bottom hotbar. Inventory slots have no selection or drag-source highlight.

## Where the code lives

| File | Responsibility |
| --- | --- |
| `Inventory/Inventory.cs` | Owns item stacks, moves, stacking, item use, and serialization. `PlayerCapacity` is 30; legacy 12/24/36-slot saves and chest sizes remain supported. |
| `Inventory/Hotbar.cs` | Tracks the selected slot and reads its current item from the inventory. No duplicate item collection. |
| `Input/HotbarInput.cs` | Maps fresh number-key presses to slot indexes; 0 maps to index 9. |
| `UI/InventoryLayout.cs` | Measures the PNG slot positions, scales them, and uses those same rectangles for mouse hits. |
| `UI/ItemSlotRenderer.cs` | Draws icons, counts, key labels, selection frames, and captions for both panels. |
| `UI/HotbarPanel.cs` | Draws the bottom bar and handles its keyboard/mouse selection. |
| `UI/InventoryPanel.cs` | Hit-tests mouse-down/up, draws source previews, and draws the cursor stack last. |
| `UI/UIManager.cs` | Opens one menu at a time, routes menu input, and draws the dim overlay. |
| `Game1.cs` | Creates the shared inventory/hotbar and connects update/draw calls. |

Slots are zero-based in code. Inventory indexes 0-9 are always the hotbar. `hotbar.SelectedItem` returns the live selected stack (or null), including after a move, pickup, or consumption. Gameplay actions can call `hotbar.TryUseSelectedItem(context)` with an `IItemUseContext` implementation. Health, stamina, and tool effects still require their gameplay systems; selecting an item does not invent an effect or consume it.

The 30-slot player inventory fits on one page. When displaying an older 36-slot inventory, Page Up/Page Down switch the lower storage rows while keeping the hotbar row fixed. Pending moves are canceled when closing/switching menus or changing pages, so no item is left on a cursor or lost.

## Input and drawing order

Game1 samples the mouse once, updates menus, then updates the hotbar if no menu consumed input. Inventory and Crafting block movement and collection while allowing respawn timers to continue. Pause also stops world timers and camera updates. Tab/C do not switch menus while paused.

Hovering over the hotbar blocks mouse interactions with the world behind it while allowing movement. Closing a menu consumes that frame's input to prevent clicks from reaching world objects.

Game1 owns SpriteBatch Begin/End. The hotbar draws after the world, followed by the menu dim overlay and active panel. UI panels use screen coordinates without the world camera transform.

## Add a menu

1. Add a value to `MenuType`.
2. Create a class implementing `IMenuPanel`, passing dependencies through its constructor.
3. Register it in `Game1.LoadContent()` using `uiManager.Register(...)`.
4. Open it with `uiManager.Open(...)`, or add a shortcut in `UIManager.Update()`.
5. Implement `OnClosed()` if the panel has temporary interaction state to clear.

Crafting recipes and pause/settings buttons remain placeholders. Keep gameplay rules in their systems; panels should call those systems when the player chooses an action.

## Checks

`Tests/GameplayChecks/InventoryChecks.cs` covers all ten key bindings, shared slot data, movement/stacking/swapping, selection after consumption, menu cancellation, save compatibility, paging, and layout hit testing at several screen sizes.

```powershell
dotnet build First_game.sln --no-restore -m:1
dotnet build Tests/GameplayChecks/GameplayChecks.csproj --no-restore -m:1
dotnet run --project Tests/GameplayChecks/GameplayChecks.csproj --no-build
```

## Drag transactions

Input/InventoryDragController.cs keeps one gesture state, source inventory/index/reference,
and the split mode captured on mouse-down. Releasing Alt does not change that mode.
The inventory remains authoritative until release: source and cursor rendering use previews,
then MoveItem or SplitStack commits once. Cancelling only clears previews, so no restore
operation can duplicate an item or fail because another slot filled up. Saves during a drag
also retain all items. If the source reference, count, or durability changes externally,
the stale drag is cancelled without overwriting that change.

Preview objects/count strings are created on mouse-down. Mouse movement reuses them.
Icons still come from ItemSlotRenderer and the item's definition. Hotbar rendering shares
the same preview controller. Dropping outside slots currently snaps back; a TODO marks
the future world-drop hook.

There is no chest panel yet. A future combined inventory/chest panel can share this controller,
call Update once with the hovered inventory (chest.Contents for chest slots) and slot index,
use GetDisplayedItem for both grids, and call Cancel when it closes. Both transfer methods
accept a destination inventory; transfers require the game's shared definition and behavior
registries. Existing within-inventory overloads remain available.
````

### First_game/Doors/README.md

````markdown
# House doors

## Architecture

Player.PositionChanged drives DoorTriggers' spatial buckets. Only buckets touched by the
player's final collision bounds are queried. Enter/exit callbacks change the prompt.
DoorSystem owns the fade, input lock, active ResidentArea and spawn placement.
DoorOverlay caches prompt measurements when its text changes and draws the final fade.
DoorConfiguration resolves all area/spawn references once from Content/doors.json.

The full loop is: resolve movement -> overlap callbacks -> fresh interaction press ->
lock input and hide prompt -> fade out -> validate destination -> change active area,
move the same player and snap camera -> fade in -> release input. A failed destination
keeps the source area and player position, logs the error, and fades back in.
Startup preload failures leave the exterior playable with door interaction disabled.

Both areas remain resident. Interior geometry uses the existing white pixel texture;
preloading is small synchronous initialization during LoadContent, before play begins.
There is no disk access, task wait or GPU resource creation during transitions.
Inactive exterior updates and its respawn clock are paused. Inventory, hotbar, actions
and the player remain the same objects. This project has no health system to recreate.

The door update, overlap callbacks and successful transitions allocate no managed
memory after initialization. DoorChecks measures this over 1,000 warmed-up transitions.
Existing inventory/actions/renderer code can allocate outside this door path; the test
is not a claim that the entire application allocates zero bytes. Game1 now reuses its
world GameTime, and player facing uses cached idle-animation literals.

## Setup in this MonoGame project

1. Build First_game/First_game.csproj. C# files are included automatically by the SDK.
   The project copies Content/doors.json to both build and publish output.
2. No scene editor nodes, attached components, new textures or MGCB entries are needed.
   Game1.LoadContent constructs and connects the systems using the existing player,
   font, pixel texture, house sprite and exterior WorldGrid.
3. Open Content/doors.json. InteractionKey defaults to E and InventoryKey to Tab.
   Use distinct MonoGame Keys names. FadeSeconds is the duration of each half of the fade.
4. The exterior house footprint is x=400..1500, y=100..500. Its front-door trigger
   is [870, 500, 160, 90], directly in front of the artwork's central door.
   Rectangles use [x, y, width, height], in world pixels. Change width/height to tune range.
   Overlap uses the player's feet collision rectangle, not its full artwork.
5. The interior floor is [0, 0, 1000, 700] with 32-pixel walls.
   The spawn's Ground is the player's feet anchor. Facing is a direction vector:
   [0, -1] looks into the room; [0, 1] faces away from the exterior front door.
6. Each door assigns SourceArea, Trigger, TargetArea, TargetSpawn, and IsExit.
   Exterior doors also assign AccessBounds covering their approach and return spawn.
   These cells are reserved before scenery placement but remain walkable.
7. There are no engine collision layers. Exterior movement uses WorldGrid's
   BlocksMovement cells. Interior movement is constrained to WalkableBounds.
   DoorTriggers is a separate nonblocking spatial index for the active area only.
8. Add another room by adding an Areas entry and named Spawns, then a door pair
   targeting those names. Add entrances to already placed artwork by adding Doors
   entries. The door system needs no C# changes. New exterior building artwork still
   uses the project's existing GridPlacer setup.
9. Keep spawn collision bounds outside triggers. If a spawn does overlap a trigger,
   it stays disarmed until the player leaves and re-enters, even after releasing E.
10. Run the game, approach the center of the house's south wall, and press E.
    The exit is the brown threshold at the bottom of the room.

Diagnostics go to Trace (the IDE's Debug output) and standard error.
To test an unavailable resident area in the debugger, set its IsReady to false.
An invalid/blocked destination is checked at the black frame and safely rejected.

## Manual test checklist

- Approach the front door: see "Press E to enter"; walk away: prompt disappears.
- Hold E before approaching: no entry until release and another press.
- Move out of range while pressing E: no transition.
- Press E in range: smooth fade out/in, one player inside, no lingering prompt.
- Spam E and movement/menu/hotbar keys during both fades: no double swap or input.
- Walk against all four interior walls and corners: collision contains the player.
- Walk to the brown exit threshold: see "Press E to exit". Exit and verify the
  player stands at ground [950, 675], faces down, and has no prompt.
- Collect or move an inventory item before entry. Return and confirm inventory,
  hotbar selection, collected objects and exterior placement are preserved.
- Repeat entry/exit quickly; controls and prompts recover after each fade.
- Set the entry spawn to [500, 630] temporarily, rebuild and enter: the exit stays
  disarmed until you leave its region and walk back in. Restore [500, 480].
- Change InteractionKey to F, rebuild: prompt and interaction both use F.
- Temporarily rename doors.json in the output directory, or break its JSON:
  startup logs a preload error and exterior movement/inventory still work.
- While inside, set the exterior target IsReady to false in the debugger:
  exit fades back to the room and releases input. Restore true and retry.
- Open/close inventory or pause near the door and alt-tab during a fade:
  prompts remain hidden while input is unavailable; no buffered E press fires.

## Automated checks

```powershell
dotnet build Tests/GameplayChecks/GameplayChecks.csproj --no-restore -m:1
dotnet --roll-forward Major Tests/GameplayChecks/bin/Debug/net9.0/GameplayChecks.dll
```

DoorChecks covers the full round trip, actual configured bounds, held/rebound input,
movement on the interaction frame, fade spam, failed destinations, long frames,
spawn overlap suppression, prompt change counts, state identity and allocations.
````

## 3. Manual test checklist

1. Press Tab: inventory opens and its bottom hint ends with "Tab: close."
2. Hold Tab: inventory stays open. Release and press again: it closes.
3. While inventory is open beside a door, press E: inventory stays open and no fade starts.
4. Close inventory, approach the front door, and compare the prompt font/size with
   the inventory footer. It remains centered and readable over its dark background.
5. Press E to enter, then E at the interior exit to return outside.
6. Press/hold Tab during either fade: no inventory opens during or after the fade
   until you release and press Tab again.
7. Open crafting with C: its hint says "Tab: Inventory".
8. Set InventoryKey to another unused key in doors.json and rebuild: both hints
   and the toggle follow it. Restore Tab afterward.
9. Compare the footer and door prompt at a narrower viewport: they use the same
   fitted scale and stay centered.

Validation: build succeeded with zero warnings/errors. All 157 gameplay checks pass,
including the existing allocation checks. Tests ran on the installed .NET 10 runtime
using --roll-forward Major. Visual appearance has not been manually tested.

