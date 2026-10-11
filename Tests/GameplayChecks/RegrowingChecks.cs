using System;
using System.IO;
using System.Text.Json;
using First_game.Doors;
using First_game.Entities;
using First_game.Harvesting;
using First_game.Inventory;
using First_game.Placement;
using First_game.Regrowing;
using First_game.UI;
using First_game.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary.Input;

static class RegrowingChecks
{
    static readonly Point Tile = new(6, 6);
    static bool Rejects(Action action) { try { action(); return false; } catch { return true; } }
    static HarvestableSnapshot Copy(HarvestableSnapshot s) =>
        JsonSerializer.Deserialize<HarvestableSnapshot>(JsonSerializer.Serialize(s));
    public static void Run(Action<bool, string> check)
    {
        var h = new Harness();
        var plant = h.Plant;
        check(plant.IsRipe && plant.Data.Footprint.Width == 1 && plant.Data.Footprint.Height == 1
            && h.Grid.GetCell(plant.Tile).BlocksMovement, "plant starts ripe with blocking 1x1 occupancy");
        check(!PlacementValidity.CanOccupy(h.Grid, plant.Data.Footprint, plant.Tile), "placement rejects occupied bush tile");
        int harvested = 0, regrown = 0, full = 0;
        h.Manager.OnHarvested += (_, count) => harvested += count;
        h.Manager.OnRegrown += _ => regrown++;
        h.Manager.PackFull += _ => full++;
        check(h.Manager.TryHarvest(plant, h.Items) && harvested == 2 && h.Items.GetItemCount("(O)blackberry") == 2
            && plant.StateId == "empty" && plant.RegrowAt == 60, "harvest adds unchanged Blackberry ID and schedules 60 seconds");
        check(plant.Data.State(plant.StateIndex).SpriteAsset == "Images/Bush1",
            "empty state selects existing Bush1 asset");
        check(!h.Manager.TryHarvest(plant, h.Items) && harvested == 2, "rapid second harvest and empty interaction cannot award twice");
        h.Manager.Advance(300, paused: true);
        check(h.Manager.Now == 0 && !plant.IsRipe, "paused gameplay leaves timestamp and clock unchanged");
        h.Manager.Advance(20, false);
        var saved = Copy(h.Manager.Capture());
        h.Manager.Advance(40, false);
        check(plant.IsRipe && regrown == 1 && plant.RegrowAt == null, "deadline regrows exactly once");
        h.Manager.Restore(saved); plant = h.Manager.Instances[0];
        h.Manager.Advance(39, false);
        check(!plant.IsRipe && h.Manager.Now == 59, "save/load mid-regrow preserves remaining time");
        h.Manager.Advance(1, false);
        check(plant.IsRipe, "restored plant regrows at original deadline");
        saved.GameplaySeconds = 80;
        h.Manager.Restore(saved);
        check(h.Manager.Instances[0].IsRipe && h.Manager.Instances[0].RegrowAt == null,
            "loading an overdue area immediately reconciles state");

        h = new Harness(); plant = h.Plant;
        h.Items.AddItem("(O)wood", 11 * 99); h.Items.AddItem("(O)blackberry", 98);
        int blocked = 0; h.Manager.PackFull += _ => blocked++;
        check(!h.Manager.TryHarvest(plant, h.Items) && plant.IsRipe && plant.RegrowAt == null
            && h.Items.GetItemCount("(O)blackberry") == 98 && blocked == 1,
            "partial capacity blocks whole yield, preserves ripe state and fires pack-full hook");
        var blockedSave = Copy(h.Manager.Capture());
        check(blockedSave.Instances[0].PendingYield == 2, "failed harvest preserves rolled yield in saves");
        h.Items.RemoveItem("(O)wood", 99);
        bool reentered = true;
        h.Items.Changed += () => reentered = h.Manager.TryHarvest(plant, h.Items);
        check(h.Manager.TryHarvest(plant, h.Items) && !reentered && h.Items.GetItemCount("(O)blackberry") == 100,
            "full harvest crosses stack boundary and inventory callback cannot reenter");
        var allFull = new Inventory(SampleItemCatalog.CreateDefinitions(), SampleItemCatalog.CreateBehaviors());
        allFull.AddItem("(O)wood", 12 * 99);
        h.Manager.Advance(60, false);
        check(!h.Manager.TryHarvest(plant, allFull) && plant.IsRipe
            && allFull.GetItemCount("(O)blackberry") == 0, "completely full inventory leaves all berries on plant");
        h.Manager.TryHarvest(plant, h.Items);
        var secondGrid = new WorldGrid(100, Vector2.Zero, 20, 20);
        h.Manager.RegisterMap("interior:test", secondGrid);
        var second = h.Manager.RegisterNode("interior:test", new WorldSpawnNode("test_node", new Vector2(650, 650)), h.Rule);
        h.Manager.RebuildTriggers();
        h.Manager.Advance(10, false); h.Manager.TryHarvest(second, h.Items);
        h.Manager.Advance(50, false);
        check(plant.IsRipe && !second.IsRipe, "shared scheduler orders deadlines across exterior and interior maps");
        h.Manager.Advance(10, false);
        check(second.IsRipe && h.Manager.BlocksMovement("interior:test", second.FootprintBounds),
            "inactive interior regrows without area Update and retains collision");
        h.Manager.Restore(Copy(h.Manager.Capture()));
        check(h.Manager.Instances.Count == 2 && h.Manager.Instances[1].MapId == "interior:test",
            "save round trip preserves cross-map identity");

        var queue = new RegrowScheduler(); queue.Reserve(2);
        queue.Schedule(plant, 90); queue.Schedule(second, 30);
        check(!queue.TryTakeDue(29, out _) && queue.TryTakeDue(30, out var first) && first == second
            && queue.TryTakeDue(100, out var last) && last == plant && queue.Count == 0, "scheduler processes earliest and all due deadlines");
        check(RegrowScheduler.Deadline(12.5, 60) == 72.5
            && Rejects(() => RegrowScheduler.Deadline(double.NaN, 60)), "deadline calculation validates clock");
        var random = new Random(4);
        var yield = new HarvestYield("(O)blackberry", 1, 3, 1, 2);
        bool range = true;
        for (int i = 0; i < 100; i++) { int n = yield.Roll(random); range &= n >= 3 && n <= 5; }
        check(range && new HarvestYield("(O)blackberry", 2, 2).Roll(random) == 2, "yield supports fixed, range, and guaranteed bonus");

        h = new Harness(); plant = h.Plant;
        var player = new Player((Texture2D)null, Vector2.Zero) { Scale = 1 };
        player.Position = player.PositionForGround(new Vector2(650, 750));
        var area = new ResidentArea("exterior", h.Grid);
        area.SetDoors(new[] { new Door("nearby", new Rectangle(500, 700, 300, 200),
            new First_game.Interiors.Enterable(() => new First_game.Interiors.PortalDestination(
                area, new AreaSpawn(new Vector2(650, 750), Vector2.UnitY))), false) });
        using var doors = new DoorSystem(player, new[] { area }, area, .25f, _ => { });
        // Clear initial spawn suppression to expose the competing door.
        area.Triggers.Reset(); area.Triggers.Observe(player.Bounds);
        using var interaction = new HarvestableInteraction(h.Manager, player, doors, h.Items);
        var mouse = new MouseInput();
        UIInput MouseFrame(bool down)
        {
            mouse.Update(new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released,
                down ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released));
            return new UIInput(default, default, mouse);
        }
        var keyPress = new UIInput(new KeyboardState(player.Input.Interact), default, mouse);
        check(interaction.HasCandidate && !interaction.TryInteract(keyPress, true) && plant.IsRipe,
            "E no longer harvests a nearby bush");
        var press = MouseFrame(true);
        check(!interaction.TryInteract(press, false) && plant.IsRipe,
            "UI or hotbar blocking prevents right-click harvesting");
        bool owned = interaction.TryInteract(press, true);
        doors.SetInteractionEnabled(!owned); doors.Update(.01f, press);
        check(owned && !doors.IsTransitioning && h.Items.GetItemCount("(O)blackberry") == 2,
            "right-click harvests once and consumes the world click");
        check(!interaction.TryInteract(MouseFrame(true), true),
            "held right mouse button does not trigger another harvest");
        MouseFrame(false);
        press = MouseFrame(true);
        check(interaction.TryInteract(press, true) && h.Items.GetItemCount("(O)blackberry") == 2,
            "fresh empty-bush right-click is consumed without reward");
        player.Position += new Vector2(2000, 0);
        check(!interaction.TryInteract(press, true), "movement exit clears interaction candidate");
        h.Manager.Advance(60, false);
        player.Position -= new Vector2(2000, 0);
        MouseFrame(false);
        check(interaction.HasCandidate && interaction.TryInteract(MouseFrame(true), true),
            "returning after a minute allows right-click harvesting again");
        doors.SetInteractionEnabled(true);
        check(!interaction.TryInteract(new UIInput(keyPress.Keyboard, default, null), true),
            "keyboard-only input does not harvest");
        doors.Update(.01f, keyPress);
        check(doors.IsTransitioning, "E still enters a door beside a bush");

        // Integrated catalog/spawner and transactional world save.
        var defs = SampleItemCatalog.CreateDefinitions();
        var rules = new WorldSpawnRuleRegistry(defs); WorldSpawnCatalog.RegisterRules(rules);
        var registry = new HarvestableRegistry(defs, _ => null);
        var nodes = HarvestableCatalog.Load(Path.Combine(AppContext.BaseDirectory, "Content", "harvestables.json"), registry, rules);
        check(nodes.Length == 1 && nodes[0].Count == 5 && nodes[0].MinimumPosition == new Vector2(-600, 400)
            && rules.GetRequired("blackberry_bush_drop").Chance == .85f, "JSON preserves blackberry spawn count, zone and chance");
        var grid = new WorldGrid(100, new Vector2(-3000, -3000), 60, 60);
        var manager = new HarvestableManager(registry, rules, new Random(1)); manager.RegisterMap("exterior", grid);
        var pickups = new WorldPickupSystem(grid, defs, rules, _ => null, new Random(2)) { Harvestables = manager };
        pickups.RegisterNodes(nodes); manager.RebuildTriggers();
        check(manager.Instances.Count == 5 && pickups.Capture().Active.Count == 0,
            "spawner creates plant nodes and no loose blackberry pickups");
        var items = new Inventory(defs, SampleItemCatalog.CreateBehaviors());
        var placements = new PlacementWorld(grid, defs); var resources = new ResourceWorld(grid);
        var snapshot = WorldSaveStore.Capture(items, placements, pickups, resources, Vector2.Zero, TimeSpan.Zero, harvestables: manager);
        var legacy = JsonSerializer.Deserialize<WorldSaveStore.Snapshot>(JsonSerializer.Serialize(snapshot));
        legacy.Version = 2; legacy.Harvestables = null;
        legacy.Pickups.Active.Add(new PickupSaveEntry { RuleId = "blackberry_bush_drop",
            NodeX = 50, NodeY = 50, TileX = 30, TileY = 30, Count = 2 });
        WorldSaveStore.Restore(legacy, items, defs, SampleItemCatalog.CreateBehaviors(), placements, pickups, resources, harvestables: manager);
        check(manager.Instances.Count == 1 && manager.Instances[0].IsRipe && pickups.Capture().Active.Count == 0,
            "legacy loose blackberry migrates into a ripe bush");
        manager.TryHarvest(manager.Instances[0], items); manager.Advance(17, false);
        string before = JsonSerializer.Serialize(manager.Capture());
        var bad = WorldSaveStore.Capture(items, placements, pickups, resources, Vector2.Zero, TimeSpan.Zero, harvestables: manager);
        bad.Harvestables.Instances[0].TypeId = "missing";
        check(Rejects(() => WorldSaveStore.Restore(bad, items, defs, SampleItemCatalog.CreateBehaviors(),
            placements, pickups, resources, harvestables: manager)) && before == JsonSerializer.Serialize(manager.Capture()),
            "invalid world load restores plant occupancy, identity and clock atomically");

        h = new Harness();
        // Cover blocked retries using the same cached plant and trigger buckets.
        var waitingSave = h.Manager.Capture();
        waitingSave.Instances[0].Spawned = false;
        waitingSave.Instances[0].SpawnAt = 90;
        h.Manager.Restore(waitingSave);
        for (int y = 4; y <= 8; y++) for (int x = 4; x <= 8; x++)
            h.Grid.Occupy(new Point(x, y), CellType.Building);
        h.Manager.Advance(90, false);
        check(!h.Manager.Instances[0].IsSpawned && h.Manager.Capture().Instances[0].SpawnAt == 180,
            "blocked spawn retries keep one reserved instance and never overlap obstacles");
        for (int y = 4; y <= 8; y++) for (int x = 4; x <= 8; x++) h.Grid.ClearCell(new Point(x, y));
        long spawnStart = GC.GetAllocatedBytesForCurrentThread();
        h.Manager.Advance(90, false);
        long spawnBytes = GC.GetAllocatedBytesForCurrentThread() - spawnStart;
        check(h.Manager.Instances[0].IsRipe && spawnBytes == 0, "retry spawn uses preallocated bush and heap capacity");
        check(h.Grid.GetCell(h.Manager.Instances[0].Tile).BlocksMovement, "retry spawn claims blocking occupancy");

        // Warm up callbacks/JIT, then measure only the scheduler hot path.
        h = new Harness(); h.Items.AddItem("(O)blackberry", 1);
        h.Manager.TryHarvest(h.Plant, h.Items); h.Manager.Advance(60, false);
        h.Manager.TryHarvest(h.Plant, h.Items);
        long start = GC.GetAllocatedBytesForCurrentThread();
        h.Manager.Advance(60, false);
        check(GC.GetAllocatedBytesForCurrentThread() == start, "regrowth hot path allocates zero bytes");
        start = GC.GetAllocatedBytesForCurrentThread();
        h.Manager.TryHarvest(h.Plant, h.Items);
        check(GC.GetAllocatedBytesForCurrentThread() == start, "harvest into existing inventory stack allocates zero bytes");
    }
    sealed class Harness
    {
        public readonly WorldGrid Grid = new(100, Vector2.Zero, 20, 20);
        public readonly Inventory Items;
        public readonly HarvestableManager Manager;
        public readonly HarvestableInstance Plant;
        public readonly WorldSpawnRule Rule;
        public Harness()
        {
            var defs = SampleItemCatalog.CreateDefinitions();
            Items = new Inventory(defs, SampleItemCatalog.CreateBehaviors());
            var registry = new HarvestableRegistry(defs, _ => null);
            registry.Register(new HarvestableData("test", new PlaceableData(1, 1, "Images/BlackBerryBush1"),
                new[] { new HarvestableState("ripe", "Images/BlackBerryBush1"), new HarvestableState("empty", "Images/Bush1") },
                new HarvestYield("(O)blackberry", 2, 2), "ripe", "empty", 60, "harvest test"));
            var rules = new WorldSpawnRuleRegistry(defs);
            Rule = new WorldSpawnRule("test_rule", "test_node", "(O)blackberry", 1, 2, 2, 90, "test"); rules.Register(Rule);
            Manager = new HarvestableManager(registry, rules, new Random(1));
            Manager.RegisterMap("exterior", Grid);
            Plant = Manager.RegisterNode("exterior", new WorldSpawnNode("test_node", Grid.CellCenter(Tile)), Rule);
            Manager.RebuildTriggers();
        }
    }
}
