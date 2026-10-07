using System;
using System.Collections.Generic;
using System.Text.Json;
using First_game.Harvesting;
using First_game.Input;
using First_game.Inventory;
using First_game.Placement;
using First_game.UI;
using First_game.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary.Graphics;
using MonoGameLibrary.Input;

static class PlacementChecks
{
    const string TentId = "(F)tent";
    static readonly Point Origin = new(4, 4);
    static readonly Vector2 Cursor = new(650, 650); // Bottom-center of the 5x3 footprint at (4,4).
    static bool Rejects(Action action)
    {
        try { action(); return false; } catch (InvalidOperationException) { return true; }
    }
    public static void Run(Action<bool, string> check)
    {
        var h = new Harness();
        var data = h.Tent.Placeable;
        check(data.Width == 5 && data.Height == 3 && data.SpriteAsset == "Images/Tent"
            && data.ConsumeOnPlacement && !data.AllowOverlap, "Tent declares existing art and 5x3 footprint with safe defaults");
        check(data.OriginFromCursor(new Point(6, 6), 0) == Origin, "bottom-center cursor resolves footprint top-left");
        var rotated = new PlaceableData(3, 2, "test", rotatable: true);
        check(rotated.Size(0) == new Point(3, 2) && rotated.Size(1) == new Point(2, 3)
            && rotated.Size(2) == new Point(3, 2) && rotated.Size(3) == new Point(2, 3), "quarter turns swap footprint dimensions");
        check(new PlaceableData(3, 2, "test", PlacementAnchor.TopLeft).OriginFromCursor(new Point(1, 2), 0) == new Point(1, 2), "top-left anchor stays on cursor tile");
        check(h.Grid.WorldToCell(new Vector2(-1, -1)) == new Point(-1, -1), "negative coordinates snap down rather than truncate");
        check(h.Controller.IsActive && h.Hotbar.SelectedSlot == 0, "collecting Tent into selected empty hotbar activates via event");
        h.Controller.Update(Cursor, false);
        check(h.Controller.IsValid && h.Controller.OriginTile == Origin, "Tent preview valid over nearby grass");
        int validations = h.Controller.ValidationCount;
        h.Controller.Update(Cursor + new Vector2(10, 10), false);
        check(h.Controller.ValidationCount == validations, "mouse movement inside same tile does not revalidate");
        h.Controller.Rotate();
        check(h.Controller.Rotation == 0, "Tent ignores rotation without opt-in");
        h.Grid.Occupy(new Point(8, 6), CellType.Plant, new object());
        h.Controller.Update(Cursor, false);
        check(!h.Controller.IsValid && h.Controller.ValidationCount == validations + 1, "distant footprint corner occupation invalidates cached preview");
        int rejected = 0; h.World.PlacementRejected += () => rejected++;
        check(h.Controller.TryPrimaryInteract(Cursor) && h.World.Objects.Count == 0 && h.Items.GetItemCount(TentId) == 2 && rejected == 1,
            "invalid preview consumes click, not inventory, and invokes feedback hook");
        h.Grid.ClearCell(new Point(8, 6));
        h.Controller.Update(Cursor, false);
        check(h.Controller.IsValid, "clearing obstacle turns preview valid");
        h.Grid.GetCell(new Point(8, 6)).Ground = GroundType.Water;
        h.Controller.Update(Cursor, false);
        check(!h.Controller.IsValid, "water invalidates whole footprint even if empty");
        h.Grid.GetCell(new Point(8, 6)).Ground = GroundType.Dirt;
        check(!h.World.IsPlacementValid(h.Tent, Origin, 0), "Tent rejects disallowed ground");
        h.Grid.GetCell(new Point(8, 6)).Ground = GroundType.Wall;
        check(!h.World.IsPlacementValid(h.Tent, Origin, 0), "wall ground rejects placement");
        h.Grid.GetCell(new Point(8, 6)).Ground = GroundType.Grass;
        check(!h.World.IsPlacementValid(h.Tent, new Point(-1, 0), 0)
            && !h.World.IsPlacementValid(h.Tent, new Point(19, 19), 0)
            && !h.World.IsPlacementValid(h.Tent, new Point(int.MaxValue, 0), 0), "negative, edge-crossing and overflowing footprints are invalid");
        check(!h.World.IsPlacementValid(h.Tent, Origin, 1), "actual placement also rejects unsupported rotation");
        h.World.PlayerGround = Vector2.Zero;
        h.Controller.Update(Cursor, false);
        check(!h.Controller.IsValid, "moving player out of range invalidates a stationary ghost");
        h.World.PlayerGround = new Vector2(650, 800);
        h.World.PlayerBounds = new Rectangle(600, 600, 50, 20);
        h.Controller.Update(Cursor, false);
        check(!h.Controller.IsValid, "cannot place through player bounds");
        h.World.PlayerBounds = Rectangle.Empty;
        h.Controller.Update(Cursor, false);
        // Change world after green preview without an Update: the commit must still reject it.
        h.Grid.Occupy(Origin, CellType.Building);
        check(!h.World.TryPlace(h.Tent, Origin, 0, h.Items, 0, h.Hotbar.SelectedItem), "commit rechecks stale green preview against current world");
        h.Grid.ClearCell(Origin);
        check(h.Controller.TryPrimaryInteract(Cursor) && h.World.Objects.Count == 1 && h.Items.GetItemCount(TentId) == 1,
            "valid click places one Tent and consumes exactly one stack item");
        bool allBlocked = true;
        for (int y = 4; y < 7; y++) for (int x = 4; x < 9; x++)
            allBlocked &= h.Grid.IsOccupied(new Point(x, y)) && h.Grid.GetCell(new Point(x, y)).BlocksMovement
                && ReferenceEquals(h.World.Occupancy.At(new Point(x, y)), h.World.Objects[0]);
        check(allBlocked && h.Grid.IntersectsBlockedCell(new Rectangle(800, 600, 50, 50)), "all fifteen Tent tiles reserve occupancy and movement collision");
        check(!h.Controller.IsValid && !h.Grid.CanPlace(Origin), "placed Tent immediately blocks next preview and legacy grid placement");
        h.Controller.TryPrimaryInteract(Cursor);
        check(h.World.Objects.Count == 1 && h.Items.GetItemCount(TentId) == 1, "repeated click cannot duplicate occupied Tent");
        h.World.TryPickUp(new Point(8, 6), h.Items);
        check(h.World.Objects.Count == 0 && h.Items.GetItemCount(TentId) == 2 && !h.Grid.IsOccupied(Origin)
            && !h.Grid.IntersectsBlockedCell(new Rectangle(400, 400, 500, 300)), "pickup at any footprint tile refunds one Tent and frees every cell");
        check(!h.World.TryPickUp(Origin, h.Items), "second pickup cannot refund again");

        h = new Harness(1);
        h.Controller.Update(Cursor, false); h.Controller.TryPrimaryInteract(Cursor);
        check(!h.Controller.IsActive && !h.Controller.IsVisible && h.Hotbar.SelectedItem == null, "last item consumption exits mode immediately");
        h.Items.AddItem("(O)wood", 30 * 99);
        h.World.TryPickUp(Origin, h.Items);
        check(h.World.Objects.Count == 1 && h.Grid.IsOccupied(Origin), "full inventory leaves Tent placed and blocked");
        h.Items.RemoveItem("(O)wood", 99);
        h.World.TryPickUp(Origin, h.Items);
        check(h.World.Objects.Count == 0 && h.Hotbar.SelectedItem.QualifiedId == TentId && h.Controller.IsActive, "freeing inventory space allows lossless pickup");

        h = new Harness();
        h.Items.AddItem(TentId, 23);
        check(h.Items.GetSlot(0).Count == 10 && h.Items.GetSlot(1).Count == 10 && h.Items.GetSlot(2).Count == 5,
            "placeable furniture obeys declared stack size through normal AddItem");
        var old = h.Hotbar.SelectedItem;
        h.Items.MoveItem(0, 29);
        check(!h.Controller.IsActive && !h.World.TryPlace(h.Tent, Origin, 0, h.Items, 0, old), "dragging selected stack away exits mode and rejects stale instance");
        h.Items.MoveItem(29, 0);
        check(h.Controller.IsActive, "moving placeable into selected slot activates via inventory event");
        h.Hotbar.SelectSlot(9);
        check(!h.Controller.IsActive, "empty hotbar selection exits placement");
        h.Hotbar.SelectSlot(0); h.Controller.Cancel(); h.Controller.Update(Cursor, false);
        check(!h.Controller.IsActive, "Escape cancellation persists across preview updates");
        h.Items.AddItem("(O)wood");
        check(!h.Controller.IsActive, "unrelated inventory change does not reactivate cancelled mode");
        h.Hotbar.SelectSlot(0);
        check(h.Controller.IsActive, "explicit reselection re-enters mode after Escape");
        h.Controller.Update(Cursor, true); h.Controller.TryPrimaryInteract(Cursor);
        check(h.World.Objects.Count == 0 && !h.Controller.IsVisible, "UI-blocked preview cannot place even with direct click");
        var drag = new InventoryDragController();
        drag.BeginDrag(h.Items, 0, false);
        h.Controller.Update(Cursor, true); drag.Drop(h.Items, 28);
        check(!h.Controller.IsActive && h.Items.GetSlot(28).Count == 10, "drag-drop transfers stack while placement stays suppressed");
        h.Items.MoveItem(28, 0); h.Controller.Update(Cursor, false);
        check(h.Controller.IsValid, "closing inventory recomputes live preview after drag");

        var mouse = new MouseInput();
        var camera = new Camera2D(Cursor);
        var viewport = new Viewport(0, 0, 800, 600);
        var router = new MouseInteractionController(camera, null, h.Items, mouse);
        int toolCalls = 0;
        router.TryPrimaryInteract = point => h.Controller.TryPrimaryInteract(point) || ++toolCalls > 0;
        void Frame(bool pressed, bool blocked = false)
        {
            mouse.Update(new MouseState(400, 300, 0, pressed ? ButtonState.Pressed : ButtonState.Released,
                ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            router.Update(new GameTime(), viewport, h.World.PlayerGround, blocked);
        }
        Frame(true); Frame(true);
        check(h.World.Objects.Count == 1 && toolCalls == 0 && h.Items.GetItemCount(TentId) == 24, "one physical click places once and never invokes tool handler");
        Frame(false); Frame(true);
        check(h.World.Objects.Count == 1 && toolCalls == 0, "invalid second click also cannot fall through to tool");
        h.World.TryPickUp(Origin, h.Items);
        Frame(false); Frame(true, true); Frame(true);
        check(h.World.Objects.Count == 0, "UI closing while mouse is held cannot replay blocked click");

        h = new Harness();
        var menus = new UIManager(null) { TryCancelWorldMode = () => { h.Controller.Cancel(); return true; } };
        menus.Update(new GameTime(), new UIInput(new KeyboardState(Keys.Escape), default, mouse), viewport);
        check(!h.Controller.IsActive && menus.ConsumedInputThisFrame && !menus.ExitRequested, "Escape cancels placement without exiting game or leaking input");
        h.Hotbar.SelectSlot(0);
        var inventoryDrag = new InventoryDragController();
        var inventoryPanel = new InventoryPanel(h.Items, h.Hotbar, null, null, inventoryDrag);
        menus.Register(MenuType.Inventory, inventoryPanel); menus.Open(MenuType.Inventory);
        inventoryDrag.BeginDrag(h.Items, 0, false);
        menus.Update(new GameTime(), new UIInput(new KeyboardState(Keys.Escape), default, mouse), viewport);
        check(!h.Controller.IsActive && !inventoryDrag.IsDragging && h.Items.GetItemCount(TentId) == 2,
            "Escape during inventory drag closes menu, cancels placement and preserves stack");
        h.Hotbar.SelectSlot(0);
        var bar = new HotbarPanel(h.Items, h.Hotbar, null, null);
        mouse.Update(new MouseState(0, 0, 120, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
        bar.Update(new UIInput(default, default, mouse), viewport);
        check(h.Hotbar.SelectedSlot == 9 && !h.Controller.IsActive, "scroll wheel wraps hotbar and exits placeable selection");

        h = new Harness();
        h.Controller.Update(Cursor, false);
        for (int i = 0; i < 100; i++) h.Controller.Update(Cursor + new Vector2(i % 2 * 100, 0), false);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) h.Controller.Update(Cursor + new Vector2(i % 2 * 100, 0), false);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        check(allocated == 0, "moving placement preview allocates zero bytes (bytes=" + allocated + ")");

        PersistenceAndOverlap(check);
    }

    static void PersistenceAndOverlap(Action<bool, string> check)
    {
        var h = new Harness();
        h.Items.RemoveItem(TentId, 2); h.Items.AddItem(TentId, 2, quality: 3, durability: 7);
        h.Controller.Update(Cursor, false); h.Controller.TryPrimaryInteract(Cursor);
        string saved = h.World.SaveToJson();
        check(saved.Contains(TentId) && !saved.Contains("SpriteAsset") && !saved.Contains("Placeable"), "placement saves contain IDs and runtime state, not full definitions");
        h.World.TryPickUp(Origin, h.Items);
        h.World.LoadFromJson(saved);
        check(h.World.Objects.Count == 1 && h.Grid.IsOccupied(new Point(8, 6)) && h.World.Objects[0].Quality == 3
            && h.World.Objects[0].Durability == 7, "placement load rebuilds every occupied tile and preserves item metadata");
        h.World.LoadFromJson(saved);
        check(h.World.Objects.Count == 1, "restoring placement snapshot twice replaces rather than appends");
        string previous = h.World.SaveToJson();
        check(Rejects(() => h.World.LoadFromJson("[{\"QualifiedItemId\":\"(F)missing\"}]")) && h.World.SaveToJson() == previous,
            "unknown saved ID rejects entire load without altering placements");
        var entries = h.World.Capture();
        check(Rejects(() => h.World.Restore(new[] { entries[0], entries[0] })) && h.World.SaveToJson() == previous,
            "overlapping invalid save rejects atomically");
        entries[0].Rotation = 1;
        check(Rejects(() => h.World.Restore(entries)), "invalid saved rotation cannot bypass metadata rules");

        var overlap = new ItemDefinition("(F)overlap", "Overlap", "", "test", ItemCategory.Furniture, 10, 0,
            placeable: new PlaceableData(1, 1, "test", allowOverlap: true, maxRangeTiles: 20));
        var free = new ItemDefinition("(T)builder", "Builder", "", "test", ItemCategory.Tool, 1, 0,
            placeable: new PlaceableData(2, 1, "test", consumeOnPlacement: false, rotatable: true, maxRangeTiles: 20, onPlacedEffect: "effect"));
        h.Definitions.Register(overlap); h.Definitions.Register(free);
        int effects = 0;
        var world = new PlacementWorld(new WorldGrid(100, Vector2.Zero, 20, 20), h.Definitions,
            new Dictionary<string, Action<PlacementObject>> { ["effect"] = _ => effects++ });
        var items = new Inventory(h.Definitions, h.Behaviors);
        items.AddItem(overlap.QualifiedId, 2);
        var staticObject = new object(); world.Grid.Occupy(Origin, CellType.Plant, staticObject);
        world.TryPlace(overlap, Origin, 0, items, 0, items.GetSlot(0));
        world.TryPlace(overlap, Origin, 0, items, 0, items.GetSlot(0));
        check(world.Objects.Count == 2 && ReferenceEquals(world.Grid.GetCell(Origin).Occupant, staticObject), "opt-in overlap preserves underlying static object and both placements");
        world.TryPickUp(Origin, items);
        check(world.Objects.Count == 1 && world.Grid.GetCell(Origin).BlocksMovement, "removing top overlap keeps lower occupancy blocking");
        world.TryPickUp(Origin, items);
        check(world.Objects.Count == 0 && ReferenceEquals(world.Grid.GetCell(Origin).Occupant, staticObject)
            && world.Grid.GetCell(Origin).BlocksMovement, "removing all placements preserves original static collision");
        world.Grid.ClearCell(Origin);
        world.Grid.GetCell(Origin).PlacementForbidden = true;
        check(!world.IsPlacementValid(overlap, Origin, 0), "overlap-enabled objects cannot block reserved door access");
        world.Grid.GetCell(Origin).PlacementForbidden = false;
        world.Grid.GetCell(Origin).Ground = GroundType.Water;
        check(!world.IsPlacementValid(overlap, Origin, 0), "overlap permission cannot bypass water restriction");
        world.Grid.GetCell(Origin).Ground = GroundType.Grass;
        items.AddItem(free.QualifiedId);
        world.TryPlace(free, Origin, 1, items, 1, items.GetSlot(1));
        check(world.Objects.Count == 1 && items.GetItemCount(free.QualifiedId) == 1 && effects == 1
            && world.Grid.IsOccupied(Origin + new Point(0, 1)) && !world.Grid.IsOccupied(Origin + new Point(1, 0)),
            "rotatable non-consuming item reserves rotated shape and resolves effect once");
        world.LoadFromJson(world.SaveToJson());
        check(effects == 1 && world.Objects[0].Rotation == 1, "loading rotation never replays on-placed effect");
        world.TryPickUp(Origin, items);
        check(items.GetItemCount(free.QualifiedId) == 1 && world.Objects.Count == 0, "removing non-consuming placement does not duplicate builder item");

        h = new Harness();
        var resources = new ResourceWorld(h.Grid);
        var sprite = new Sprite(null) { SourceRectangle = new Rectangle(0, 0, 10, 10) };
        var treeCell = new Point(12, 12);
        h.Grid.Occupy(treeCell, CellType.Plant, sprite);
        var tree = resources.Register(sprite, treeCell, HarvestCatalog.Tree);
        tree.TryHit(HarvestCatalog.BasicAxe);
        var rules = new WorldSpawnRuleRegistry(h.Definitions); WorldSpawnCatalog.RegisterRules(rules);
        var pickups = new WorldPickupSystem(h.Grid, h.Definitions, rules, _ => null, new Random(1));
        h.Controller.Update(Cursor, false); h.Controller.TryPrimaryInteract(Cursor);
        var snapshot = WorldSaveStore.Capture(h.Items, h.World, pickups, resources, new Vector2(400, 800), TimeSpan.FromSeconds(12));
        snapshot = JsonSerializer.Deserialize<WorldSaveStore.Snapshot>(JsonSerializer.Serialize(snapshot));
        h.World.TryPickUp(Origin, h.Items); resources.Remove(tree);
        WorldSaveStore.Restore(snapshot, h.Items, h.Definitions, h.Behaviors, h.World, pickups, resources);
        check(h.Items.GetItemCount(TentId) == 1 && h.World.Objects.Count == 1 && resources.Count == 1
            && resources.Capture()[0].Health == 2 && snapshot.ElapsedSeconds == 12, "world snapshot restores matching inventory, placements, resource health and clock");
        var bad = JsonSerializer.Deserialize<WorldSaveStore.Snapshot>(JsonSerializer.Serialize(snapshot));
        bad.Placements[0].QualifiedItemId = "(F)missing";
        check(Rejects(() => WorldSaveStore.Restore(bad, h.Items, h.Definitions, h.Behaviors, h.World, pickups, resources))
            && h.Items.GetItemCount(TentId) == 1 && h.World.Objects.Count == 1 && resources.Count == 1,
            "failed world load rolls back inventory, resources and occupancy together");
    }

    sealed class Harness
    {
        public readonly ItemDefinitionRegistry Definitions = SampleItemCatalog.CreateDefinitions();
        public readonly ItemCategoryBehaviorRegistry Behaviors = SampleItemCatalog.CreateBehaviors();
        public readonly WorldGrid Grid = new(100, Vector2.Zero, 20, 20);
        public readonly Inventory Items;
        public readonly Hotbar Hotbar;
        public readonly PlacementWorld World;
        public readonly PlacementController Controller;
        public ItemDefinition Tent => Definitions.GetRequired(TentId);
        public Harness(int count = 2)
        {
            Items = new Inventory(Definitions, Behaviors, Inventory.PlayerCapacity);
            Hotbar = new Hotbar(Items);
            World = new PlacementWorld(Grid, Definitions) { PlayerGround = new Vector2(650, 800) };
            Controller = new PlacementController(World, Hotbar, Items);
            Items.AddItem(TentId, count);
        }
    }
}
