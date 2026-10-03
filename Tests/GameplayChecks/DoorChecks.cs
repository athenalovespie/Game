using System;
using System.IO;
using First_game.Doors;
using First_game.Entities;
using First_game.UI;
using First_game.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

static class DoorChecks
{
    private static readonly UIInput Press = new(new KeyboardState(Keys.E), default, null);
    private static readonly UIInput Held = new(new KeyboardState(Keys.E), new KeyboardState(Keys.E), null);
    private static readonly UIInput Release = new(default, new KeyboardState(Keys.E), null);

    public static void Run(Action<bool, string> check)
    {
        Player player = new((Texture2D)null, Vector2.Zero)
        {
            Scale = 1, CollisionSize = new Vector2(20, 20)
        };
        var grid = new WorldGrid(100, new Vector2(-1000), 30, 30);
        var exterior = new ResidentArea("exterior", grid);
        var interior = new ResidentArea("house", new Rectangle(0, 0, 400, 400), 20);
        var enter = new Door("enter", new Rectangle(90, 90, 60, 60), interior,
            new AreaSpawn(new Vector2(200, 200), new Vector2(0, -1)), false);
        var exit = new Door("exit", new Rectangle(170, 300, 60, 60), exterior,
            new AreaSpawn(new Vector2(120, 210), new Vector2(0, 1)), true);
        exterior.SetDoors(new[] { enter });
        interior.SetDoors(new[] { exit });
        int errors = 0;
        using var system = new DoorSystem(player, new[] { exterior, interior }, exterior, .25f, _ => errors++);
        int promptChanges = 0;
        int swaps = 0;
        system.PromptChanged += _ => promptChanges++;
        system.AreaChanged += () => swaps++;

        player.Position = new Vector2(120, 120);
        check(system.Prompt == "Press E to enter", "door overlap shows enter prompt");
        int changes = promptChanges;
        for (int i = 0; i < 10; i++) system.Update(.016f, default);
        check(promptChanges == changes, "stationary updates do not rewrite prompt");
        player.MoveBy(new Vector2(100, 0));
        system.Update(.016f, Press);
        check(system.Prompt == null && !system.IsTransitioning,
            "leaving trigger on the E frame prevents entry");

        player.Position = new Vector2(120, 120);
        system.Update(.016f, Held);
        check(!system.IsTransitioning, "holding E when entering range requires a fresh press");
        system.Update(.016f, Press);
        check(system.IsTransitioning && player.InputLocked && system.Prompt == null,
            "entry locks input and hides prompt");
        Vector2 lockedPosition = player.Position;
        player.MoveBy(new Vector2(100, 0));
        check(player.Position == lockedPosition, "transition also blocks direct movement");
        system.Update(.125f, Press);
        check(system.ActiveArea == exterior && system.FadeOpacity == .5f,
            "fade advances without an early area swap");
        system.Update(.125f, Press);
        check(system.ActiveArea == interior && player.GroundPosition == new Vector2(200, 200)
            && player.Facing == "Back" && swaps == 1 && system.FadeOpacity == 1,
            "black frame commits one swap at the configured spawn");
        system.Update(.25f, Held);
        check(!system.IsTransitioning && !player.InputLocked && system.Prompt == null,
            "fade-in releases control without re-triggering");
        check(interior.BlocksMovement(new Rectangle(0, 0, 20, 20))
            && !interior.BlocksMovement(player.Bounds), "room walls contain the player");

        object exteriorState = new object();
        grid.Occupy(new Point(0, 0), CellType.Pickup, exteriorState, false);
        var actions = player.Actions;
        player.Position = new Vector2(200, 330);
        check(system.Prompt == "Press E to exit", "same component shows exit prompt");
        system.Update(.016f, Release);
        system.Update(.016f, Press);
        system.Update(1f, Press);
        check(system.ActiveArea == exterior && system.IsTransitioning && system.FadeOpacity == 1,
            "long frames retain a black frame and do not skip fade-in");
        system.Update(1f, Press);
        check(player.GroundPosition == new Vector2(120, 210) && player.Facing == "Front"
            && system.Prompt == null && !player.InputLocked, "return is outside trigger and facing away");
        check(ReferenceEquals(grid.GetCell(new Point(0, 0)).Occupant, exteriorState)
            && ReferenceEquals(actions, player.Actions), "round trip preserves world and player instances");

        player.Position = new Vector2(120, 120);
        interior.IsReady = false;
        system.Update(.016f, Press);
        system.Update(.25f, default);
        system.Update(.25f, default);
        check(errors == 1 && system.LastError != null && system.ActiveArea == exterior
            && !player.InputLocked && !system.IsTransitioning,
            "unavailable target logs and fades back with controls restored");
        interior.IsReady = true;

        // A collision added after preload must also fail before committing the swap.
        player.Position = new Vector2(120, 120);
        system.Update(.016f, Press);
        system.Update(.25f, default);
        system.Update(.25f, default);
        player.Position = new Vector2(200, 330);
        grid.Occupy(grid.WorldToCell(new Vector2(120, 210)), CellType.Building);
        system.Update(.016f, Press);
        system.Update(.25f, default);
        system.Update(.25f, default);
        check(errors == 2 && system.ActiveArea == interior && !player.InputLocked,
            "blocked return spawn keeps the player safely inside");
        grid.ClearCell(grid.WorldToCell(new Vector2(120, 210)));
        system.Update(.016f, Press);
        system.Update(.25f, default);
        system.Update(.25f, default);

        system.SetInteractionEnabled(false);
        player.Position = new Vector2(120, 120);
        system.Update(.016f, Press);
        check(system.Prompt == null && !system.IsTransitioning, "menus and actions suppress interaction");
        system.SetInteractionEnabled(true);
        player.Input.Interact = Keys.F;
        check(system.Prompt == "Press F to enter", "binding event refreshes cached prompt");
        system.Update(.016f, Press);
        check(!system.IsTransitioning, "old interaction binding no longer fires");
        system.Update(.016f, new UIInput(new KeyboardState(Keys.F), default, null));
        check(system.IsTransitioning, "configured interaction key starts transition");
        system.Update(.25f, default);
        system.Update(.25f, default);
        player.Input.Interact = Keys.E;

        CheckSpawnOverlap(check);
        CheckConfiguration(check);

        // Warm up JIT and exercise moving overlaps plus complete transitions.
        player.Position = new Vector2(200, 330);
        for (int i = 0; i < 100; i++) Cycle(player, system);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) Cycle(player, system);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        check(allocated == 0, "door movement callbacks and transitions allocate zero bytes after initialization");
    }

    private static void Cycle(Player player, DoorSystem system)
    {
        player.Position = system.ActiveArea.IsExterior ? new Vector2(120, 120) : new Vector2(200, 330);
        system.Update(.016f, Press);
        system.Update(.25f, Held);
        system.Update(.25f, Held);
        system.Update(.016f, Release);
    }

    private static void CheckSpawnOverlap(Action<bool, string> check)
    {
        Player player = new((Texture2D)null, new Vector2(110, 110))
        {
            Scale = 1, CollisionSize = new Vector2(20)
        };
        var a = new ResidentArea("a", new Rectangle(0, 0, 400, 400), 20);
        var b = new ResidentArea("b", new Rectangle(0, 0, 400, 400), 20);
        a.SetDoors(new[] { new Door("a-b", new Rectangle(90, 90, 60, 60), b,
            new AreaSpawn(new Vector2(110, 120), Vector2.UnitY), false) });
        b.SetDoors(new[] { new Door("b-a", new Rectangle(90, 90, 60, 60), a,
            new AreaSpawn(new Vector2(110, 120), Vector2.UnitY), true) });
        using var system = new DoorSystem(player, new[] { a, b }, a, .1f, null);
        system.Update(.01f, Press);
        check(!system.IsTransitioning && system.Prompt == null, "initial spawn inside trigger is disarmed");
        player.Position = new Vector2(250, 250);
        player.Position = new Vector2(110, 110);
        system.Update(.01f, Press);
        system.Update(.1f, default);
        system.Update(.1f, default);
        system.Update(.01f, Press);
        check(system.ActiveArea == b && !system.IsTransitioning && system.Prompt == null,
            "destination spawn inside trigger cannot bounce back even with a fresh press");
        player.Position = new Vector2(250, 250);
        player.Position = new Vector2(110, 110);
        check(system.Prompt == "Press E to exit", "leaving and re-entering rearms a spawn-overlapped door");
    }

    private static void CheckConfiguration(Action<bool, string> check)
    {
        Player player = new((Texture2D)null, Vector2.Zero)
        {
            Scale = .5f, CollisionSize = new Vector2(270, 64),
            CollisionOffset = new Vector2(0, 331)
        };
        var grid = new WorldGrid(100, new Vector2(-3000), 60, 60);
        grid.OccupyArea(grid.WorldToCell(new Vector2(400, 150)), 11, 4, CellType.Building);
        int errors = 0;
        string path = Path.Combine(AppContext.BaseDirectory, "Content", "doors.json");
        using (var system = DoorConfiguration.Preload(path, player, grid, _ => errors++))
        {
            check(errors == 0 && system.ActiveArea.Triggers.Doors.Length == 1,
                "shipped configuration preloads beside the real house footprint");
            check(grid.IsOccupied(grid.WorldToCell(new Vector2(950, 675)))
                && !grid.IntersectsBlockedCell(player.BoundsAt(player.PositionForGround(new Vector2(950, 675)))),
                "return approach is reserved but walkable");
            player.Position = player.PositionForGround(new Vector2(950, 540));
            system.Update(.016f, Press);
            system.Update(.25f, default);
            system.Update(.25f, default);
            check(!system.ActiveArea.IsExterior && player.GroundPosition == new Vector2(500, 480),
                "real player bounds enter the configured room exactly");
            player.Position = player.PositionForGround(new Vector2(500, 630));
            system.Update(.016f, Release);
            system.Update(.016f, Press);
            system.Update(.25f, default);
            system.Update(.25f, default);
            check(system.ActiveArea.IsExterior && player.GroundPosition == new Vector2(950, 675)
                && system.Prompt == null, "shipped configuration completes the return trip");
        }
        using var failed = DoorConfiguration.Preload(path + ".missing", player, grid, _ => errors++);
        check(errors == 1 && failed.ActiveArea.IsExterior && !failed.IsTransitioning
            && !player.InputLocked, "missing interior configuration logs and preserves exterior control");
    }
}
