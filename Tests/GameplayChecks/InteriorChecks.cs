using System;
using System.IO;
using System.Text.Json;
using First_game.Doors;
using First_game.Entities;
using First_game.Harvesting;
using First_game.Interiors;
using First_game.Inventory;
using First_game.Placement;
using First_game.UI;
using First_game.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

static class InteriorChecks
{
    private static readonly UIInput Press = new(new KeyboardState(Keys.E), default, null);
    private static bool Rejects(Action action) { try { action(); return false; } catch (InvalidOperationException) { return true; } }
    public static void Run(Action<bool, string> check)
    {
        Geometry(check);
        RotatedPortals(check);
        using var h = new Harness();
        var first = h.Place(new Point(4, 4));
        var second = h.Place(new Point(12, 4));
        check(first.InstanceId != second.InstanceId && h.Interiors.InstanceCount == 0,
            "placed tents have distinct IDs and allocate no interiors until entry");
        h.Move(new Vector2(750, 750));
        check(h.Doors.Prompt == null, "adjacent non-entrance tile never shows Tent prompt for wide actor");
        h.Move(new Vector2(650, 750));
        check(h.Doors.Prompt == "Press E to enter", "Tent approach uses house prompt and binding");
        h.Doors.SetInteractionEnabled(false); h.Doors.Update(.01f, Press);
        check(!h.Doors.IsTransitioning, "disabled interaction cannot enter while UI/placement owns input");
        h.Doors.SetInteractionEnabled(true);
        int enter = 0, exit = 0;
        h.Doors.OnEnterInterior += _ => enter++;
        h.Doors.OnExitInterior += _ => exit++;
        h.Transition();
        check(h.Interiors.ActivePlacementId == first.InstanceId && h.Player.GroundPosition == new Vector2(350, 250)
            && h.Player.MapId == h.Doors.ActiveArea.Id && enter == 1,
            "Tent enters through shared fade at spawn and publishes interior map identity");
        check(h.Doors.ActiveArea.BlocksMovement(new Rectangle(0, 0, 135, 32)), "Tent walls collide with full actor bounds");
        h.Interiors.ActiveContents.AddItem("(O)wood", 3);
        h.World.PlayerGround = h.Grid.CellCenter(first.OriginTile);
        h.World.TryPickUp(first.OriginTile, h.Items);
        check(h.World.Objects.Count == 2, "direct pickup API blocks removing a Tent while inside");
        Vector2 savedPosition = h.Player.Position;
        var snapshot = h.Save();
        string json = JsonSerializer.Serialize(snapshot);
        check(!json.Contains("SpriteAsset") && !json.Contains("WidthTiles") && json.Contains(first.InstanceId),
            "interior save stores IDs and mutable item state without definitions");
        h.Exit();
        check(h.Player.GroundPosition == new Vector2(650, 750) && h.Player.MapId == "exterior" && exit == 1
            && h.Interiors.ResidentRoomCount == 0 && h.Doors.Areas.Count == 1,
            "exit returns outside same entrance and releases inactive room subscriptions");
        h.World.TryPickUp(first.OriginTile, h.Items);
        check(h.World.Objects.Count == 2, "nonempty interior blocks pickup outside");
        h.Enter(second);
        check(h.Interiors.ActiveContents.GetItemCount("(O)wood") == 0 && h.Interiors.InstanceCount == 2,
            "second Tent has independent inventory state");
        h.Load(snapshot);
        check(h.Player.Position == savedPosition && h.Interiors.ActivePlacementId == first.InstanceId
            && h.Interiors.ActiveContents.GetItemCount("(O)wood") == 3 && h.Player.MapId.StartsWith("interior:"),
            "save/load while inside restores owner, exact position and stored items");
        var bad = JsonSerializer.Deserialize<WorldSaveStore.Snapshot>(JsonSerializer.Serialize(snapshot));
        bad.Interiors.ActivePlacementId = "missing";
        check(Rejects(() => h.Load(bad)) && h.Interiors.ActivePlacementId == first.InstanceId
            && h.Player.Position == savedPosition && h.Interiors.ActiveContents.GetItemCount("(O)wood") == 3
            && h.World.Objects.Count == 2, "invalid active owner rolls back complete world and interior state");
        bad = JsonSerializer.Deserialize<WorldSaveStore.Snapshot>(JsonSerializer.Serialize(snapshot));
        bad.PlayerX = -99999;
        check(Rejects(() => h.Load(bad)) && h.Player.Position == savedPosition, "blocked interior load position rolls back");
        h.Interiors.ActiveContents.RemoveItem("(O)wood", 3);
        h.Grid.Occupy(new Point(6, 7), CellType.Building);
        h.Exit();
        check(h.Player.GroundPosition != new Vector2(650, 750) && !h.Grid.IntersectsBlockedCell(h.Player.Bounds),
            "blocked return selects nearest tile fitting the whole actor");
        h.Grid.ClearCell(new Point(6, 7));
        h.World.PlayerGround = h.Grid.CellCenter(first.OriginTile);
        h.World.TryPickUp(first.OriginTile, h.Items);
        check(h.World.Objects.Count == 1 && h.Interiors.InstanceCount == 0
            && h.Exterior.Triggers.Doors.Length == 1, "empty Tent pickup releases its state and entrance trigger");
        h.Load(snapshot);
        h.Interiors.ActiveContents.RemoveItem("(O)wood", 3);
        // All return tiles unavailable: transition fails safely without losing interior identity or items.
        for (int y = 0; y < h.Grid.Rows; y++) for (int x = 0; x < h.Grid.Columns; x++)
            h.Grid.GetCell(new Point(x, y)).Ground = GroundType.Wall;
        h.Exit();
        check(!h.Doors.ActiveArea.IsExterior && !h.Player.InputLocked && h.Doors.LastError != null
            && h.Interiors.ActivePlacementId == first.InstanceId, "fully blocked exterior keeps actor safely inside and releases fade lock");
        SharedAndLegacy(check);
        ShippedHouseSave(check);
    }
    private static void Geometry(Action<bool, string> check)
    {
        var e = new EnterableData(new Point(2, 2), "tent_small", new Point(3, 2));
        var data = new PlaceableData(5, 3, "test", rotatable: true, enterable: e);
        Point[] offsets = { new(2, 2), new(0, 2), new(2, 0), new(2, 2) };
        Point[] directions = { new(0, 1), new(-1, 0), new(0, -1), new(1, 0) };
        for (int r = 0; r < 4; r++)
            check(e.Offset(data, r) == offsets[r] && e.Direction(r) == directions[r], "entrance and facing rotate together: " + r);
        using var h = new Harness();
        var tent = h.Definitions.GetRequired("(F)tent");
        h.World.PlayerGround = new Vector2(650, 900); h.World.PlayerBounds = h.Player.Bounds;
        check(h.World.IsPlacementValid(tent, new Point(4, 4), 0), "clear approach validates with real player width");
        h.Grid.GetCell(new Point(6, 7)).Ground = GroundType.Water;
        check(!h.World.IsPlacementValid(tent, new Point(4, 4), 0), "water on entrance approach invalidates placement");
        h.Grid.GetCell(new Point(6, 7)).Ground = GroundType.Grass;
        h.Grid.Occupy(new Point(5, 7), CellType.Building);
        check(!h.World.IsPlacementValid(tent, new Point(4, 4), 0), "neighbor obstruction intersecting wide actor invalidates approach");
        h.Grid.ClearCell(new Point(5, 7));
        using var hotbar = new Hotbar(h.Items);
        using var controller = new PlacementController(h.World, hotbar, h.Items);
        controller.Update(new Vector2(650, 650), false);
        h.Grid.Occupy(new Point(6, 7), CellType.Building);
        controller.Update(new Vector2(650, 650), false);
        check(!controller.IsValid && !h.World.TryPlace(tent, new Point(4, 4), 0, h.Items, 0, h.Items.GetSlot(0)),
            "red entrance-clearance preview and commit reject the same obstruction");
        h.Grid.ClearCell(new Point(6, 7));
        h.World.PlayerGround = new Vector2(650, 2900);
        check(!h.World.IsPlacementValid(tent, new Point(4, 27), 0), "in-bounds footprint with out-of-bounds approach rejects");
        var smallGrid = new WorldGrid(100, Vector2.Zero, 3, 3);
        var shape = new Rectangle(0, 0, 20, 20);
        smallGrid.Occupy(new Point(1, 1), CellType.Building);
        check(ReturnPosition.TryFind(smallGrid, new Vector2(150, 150), shape, out var fallback)
            && fallback == new Vector2(150, 50), "nearest return fallback has deterministic distance tie-breaking");
    }
    private static void RotatedPortals(Action<bool, string> check)
    {
        for (int r = 0; r < 4; r++)
        {
            using var h = new Harness(rotatable: true);
            var obj = h.Place(new Point(4, 4), r);
            h.Enter(obj);
            check(h.Interiors.ActivePlacementId == obj.InstanceId, "rotated portal enters via rotated approach: " + r);
            h.Exit();
            var data = obj.Definition.Placeable;
            check(h.Doors.ActiveArea.IsExterior && !h.Grid.IntersectsBlockedCell(h.Player.Bounds)
                && h.Grid.WorldToCell(h.Player.GroundPosition) == data.Enterable.OutsideTile(data, obj.OriginTile, r),
                "rotated return fits wide actor outside footprint: " + r);
        }
    }
    private static void SharedAndLegacy(Action<bool, string> check)
    {
        using var h = new Harness(shared: true);
        var a = h.Place(new Point(4, 4)); var b = h.Place(new Point(12, 4));
        h.Enter(a); h.Interiors.ActiveContents.AddItem("(O)wood"); h.Exit(); h.Enter(b);
        check(h.Interiors.InstanceCount == 1 && h.Interiors.ActiveContents.GetItemCount("(O)wood") == 1,
            "shared data option reuses state across instances");
        var saved = h.Save(); h.Load(saved); h.Exit();
        check(h.Player.GroundPosition == new Vector2(1450, 750), "shared interior save remembers the actual entrance owner");
        h.Enter(a); h.Interiors.ActiveContents.RemoveItem("(O)wood", 1); h.Exit();
        h.World.PlayerGround = h.Grid.CellCenter(a.OriginTile); h.World.TryPickUp(a.OriginTile, h.Items);
        check(h.Interiors.InstanceCount == 1, "shared state survives removing one of its owners");
        h.World.PlayerGround = h.Grid.CellCenter(b.OriginTile); h.World.TryPickUp(b.OriginTile, h.Items);
        check(h.Interiors.InstanceCount == 0 && h.Doors.Areas.Count == 1, "last shared owner removal releases state");
        var legacy = h.Save(); legacy.Version = 1; legacy.Interiors = null;
        h.Load(legacy);
        check(h.Player.MapId == "exterior", "version-1 world saves load as exterior locations");
    }
    private static void ShippedHouseSave(Action<bool, string> check)
    {
        var definitions = SampleItemCatalog.CreateDefinitions(); var behaviors = SampleItemCatalog.CreateBehaviors();
        var grid = new WorldGrid(100, new Vector2(-3000), 60, 60);
        var player = new Player((Texture2D)null, Vector2.Zero) { Scale = .5f, CollisionSize = new Vector2(270, 64), CollisionOffset = new Vector2(0, 331) };
        grid.OccupyArea(grid.WorldToCell(new Vector2(400, 150)), 11, 4, CellType.Building);
        using var doors = DoorConfiguration.Preload(Path.Combine(AppContext.BaseDirectory, "Content", "doors.json"), player, grid, _ => { });
        var world = new PlacementWorld(grid, definitions);
        using var interiors = new InteriorManager(player, doors, world,
            InteriorRegistry.Load(Path.Combine(AppContext.BaseDirectory, "Content", "interiors.json")), definitions, behaviors);
        player.Position = player.PositionForGround(new Vector2(950, 540));
        doors.Update(.01f, Press); doors.Update(.25f, default); doors.Update(.25f, default);
        var state = interiors.Capture(); var position = player.Position;
        player.Position = player.PositionForGround(new Vector2(500, 630));
        doors.Update(.01f, Press); doors.Update(.25f, default); doors.Update(.25f, default);
        interiors.Restore(state, position);
        check(player.MapId == "house" && player.GroundPosition == new Vector2(500, 480)
            && interiors.ActivePlacementId == null, "fixed house also saves and restores through shared location service");
    }
    private sealed class Harness : IDisposable
    {
        public readonly ItemDefinitionRegistry Definitions = SampleItemCatalog.CreateDefinitions();
        public readonly ItemCategoryBehaviorRegistry Behaviors = SampleItemCatalog.CreateBehaviors();
        public readonly WorldGrid Grid = new(100, Vector2.Zero, 30, 30);
        public readonly Player Player = new((Texture2D)null, Vector2.Zero) { Scale = .5f, CollisionSize = new Vector2(270, 64), CollisionOffset = new Vector2(0, 331) };
        public readonly Inventory Items;
        public readonly PlacementWorld World;
        public readonly ResidentArea Exterior;
        public readonly DoorSystem Doors;
        public readonly InteriorManager Interiors;
        public readonly ResourceWorld Resources;
        public readonly WorldPickupSystem Pickups;
        private readonly ItemDefinition tent;
        public Harness(bool shared = false, bool rotatable = false)
        {
            if (shared || rotatable) Definitions.Register(new ItemDefinition("(F)shared_tent", "Shared tent", "", "Images/Tent",
                ItemCategory.Furniture, 10, 0, placeable: new PlaceableData(5, 3, "Images/Tent", maxRangeTiles: 20, rotatable: rotatable,
                    enterable: new EnterableData(new Point(2, 2), "tent_small", new Point(3, 2), shared: shared))));
            tent = Definitions.GetRequired(shared || rotatable ? "(F)shared_tent" : "(F)tent");
            Items = new Inventory(Definitions, Behaviors); Items.AddItem(tent.QualifiedId, 5);
            World = new PlacementWorld(Grid, Definitions);
            Exterior = new ResidentArea("exterior", Grid);
            Doors = new DoorSystem(Player, new[] { Exterior }, Exterior, .25f, _ => { });
            Interiors = new InteriorManager(Player, Doors, World,
                InteriorRegistry.Load(Path.Combine(AppContext.BaseDirectory, "Content", "interiors.json")), Definitions, Behaviors);
            Resources = new ResourceWorld(Grid);
            var rules = new WorldSpawnRuleRegistry(Definitions); WorldSpawnCatalog.RegisterRules(rules);
            Pickups = new WorldPickupSystem(Grid, Definitions, rules, _ => null, new Random(1));
        }
        public PlacementObject Place(Point origin, int rotation = 0)
        {
            var data = tent.Placeable;
            Point direction = data.Enterable.Direction(rotation);
            Move(data.Enterable.ReturnGround(Grid, data, origin, rotation, Player.Bounds) + new Vector2(direction.X, direction.Y) * 100);
            World.PlayerGround = Player.GroundPosition; World.PlayerBounds = Player.Bounds;
            if (!World.TryPlace(tent, origin, rotation, Items, 0, Items.GetSlot(0))) throw new Exception("Test placement failed");
            return World.Objects[World.Objects.Count - 1];
        }
        public void Move(Vector2 ground) => Player.Position = Player.PositionForGround(ground);
        public void Transition() { Doors.Update(.01f, Press); Doors.Update(.25f, default); Doors.Update(.25f, default); }
        public void Enter(PlacementObject obj)
        {
            var data = obj.Definition.Placeable;
            Point direction = data.Enterable.Direction(obj.Rotation);
            Vector2 ground = data.Enterable.ReturnGround(Grid, data, obj.OriginTile, obj.Rotation, Player.Bounds);
            Move(ground + new Vector2(direction.X, direction.Y) * 100); Move(ground); Transition();
        }
        public void Exit() { Move(new Vector2(350, 450)); Transition(); }
        public WorldSaveStore.Snapshot Save() => JsonSerializer.Deserialize<WorldSaveStore.Snapshot>(JsonSerializer.Serialize(
            WorldSaveStore.Capture(Items, World, Pickups, Resources, Player.Position, TimeSpan.FromSeconds(12), Interiors)));
        public void Load(WorldSaveStore.Snapshot snapshot) => WorldSaveStore.Restore(snapshot, Items, Definitions, Behaviors, World, Pickups, Resources, Interiors);
        public void Dispose() { Interiors.Dispose(); Doors.Dispose(); }
    }
}
