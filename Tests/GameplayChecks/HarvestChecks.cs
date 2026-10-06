using System;
using First_game.Actions;
using First_game.Entities;
using First_game.Harvesting;
using First_game.Input;
using First_game.Inventory;
using First_game.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary.Graphics;
using MonoGameLibrary.Input;

static class HarvestChecks
{
    static GameTime Time(float seconds) => new(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));

    public static void Run(Action<bool, string> check)
    {
        var h = new Harness();
        int hits = 0, depleted = 0;
        h.Node.Hit += (_, _) => hits++;
        h.World.Depleted += _ => depleted++;
        check(h.Controller.TryInteract(h.Click) && h.Player.Actions.IsBusy, "tree click starts selected axe swing");
        check(h.Player.Actions.BlocksMovement && h.Node.Health == 3, "wind-up locks movement without damage");
        h.Step(.29f);
        check(h.Node.Health == 3, "no hit before impact");
        h.Step(.02f);
        check(h.Node.Health == 2 && hits == 1 && h.Player.Actions.IsBusy, "third frame applies one hit and retains recovery");
        check(!h.Player.Actions.Cancel(), "cannot cancel recovery to speed up chopping");
        h.Controller.TryInteract(h.Click);
        h.Step(.3f);
        check(h.Node.Health == 2 && !h.Player.Actions.IsBusy, "clicking while busy cannot overlap swings");
        check(h.Grid.IsOccupied(h.Cell) && h.World.Count == 1, "surviving tree keeps sprite and collision");
        for (int i = 0; i < 2; i++)
        {
            h.Controller.TryInteract(h.Click);
            h.Step(10);
        }
        check(h.Node.IsDepleted && hits == 3 && depleted == 1, "basic axe requires three successful swings, including long frames");
        check(h.World.Count == 0 && !h.Grid.IsOccupied(h.Cell)
            && !h.Grid.IntersectsBlockedCell(new Rectangle(0, 0, 100, 100)),
            "depletion removes drawing registration and frees collision");
        check(!h.Node.TryHit(HarvestCatalog.BasicAxe) && depleted == 1 && !h.Controller.TryInteract(h.Click),
            "depleted resources cannot be clicked or depleted twice");

        h = new Harness();
        h.Controller.TryInteract(h.Click);
        h.Player.Actions.Update(Time(.1f), new ActionInput(false, false, true));
        h.Step(10);
        check(h.Node.Health == 3 && !h.Player.Actions.IsBusy, "cancel before impact leaves health unchanged");

        h = new Harness();
        h.Player.Position = new Vector2(1000, 1000);
        h.Controller.TryInteract(h.Click);
        check(!h.Player.Actions.IsBusy && h.Controller.LastFailure != null, "out-of-range click rejects swing");
        h.Player.Position = Vector2.Zero;
        h.Controller.TryInteract(h.Click);
        h.Player.Position = new Vector2(1000, 1000);
        h.Step(10);
        check(h.Node.Health == 3, "range rechecked at impact after teleport");

        h = new Harness();
        h.Hotbar.SelectSlot(1);
        h.Controller.TryInteract(h.Click);
        check(!h.Player.Actions.IsBusy, "empty selected slot cannot chop");
        h.Hotbar.SelectSlot(0);
        h.Controller.TryInteract(h.Click);
        h.Hotbar.SelectSlot(1);
        h.Step(10);
        check(h.Node.Health == 3, "switching away from captured tool prevents impact");

        h = new Harness();
        h.Hotbar.SelectedItem.SetDurability(0);
        h.Controller.TryInteract(h.Click);
        check(!h.Player.Actions.IsBusy, "broken tool rejects swing");
        h.Hotbar.SelectedItem.SetDurability(null);
        h.Controller.TryInteract(h.Click);
        h.World.Remove(h.Node);
        h.Step(10);
        check(h.Node.Health == 3, "removed target is invalidated before impact");

        h = new Harness();
        var replacement = new object();
        h.Grid.GetCell(h.Cell).Occupant = replacement;
        h.World.Remove(h.Node);
        check(ReferenceEquals(h.Grid.GetCell(h.Cell).Occupant, replacement), "removal preserves reassigned grid occupancy");

        h = new Harness();
        var pickaxe = new ToolDefinition("test_pickaxe", "pickaxe", 1, 180, .6f, .5f, "MineLeft", "MineRight");
        check(!h.Node.TryHit(pickaxe), "tree rejects incompatible tool kind");
        var rock = new ResourceNode(new ResourceDefinition("rock", "pickaxe", 2), Vector2.Zero);
        check(rock.TryHit(pickaxe) && rock.Health == 1, "same resource health supports rocks and pickaxes");
        var strongAxe = new ToolDefinition("test_axe", "axe", 20, 180, .2f, 1f, "ChopLeft", "ChopRight");
        int actualDamage = 0;
        h.Node.Hit += (_, damage) => actualDamage = damage;
        h.Player.Actions.TryStart(new HarvestAction(h.Node, strongAxe, () => true), out _);
        h.Step(.19f);
        check(h.Node.Health == 3, "tool can defer impact until completion");
        h.Step(.02f);
        check(h.Node.Health == 0 && actualDamage == 3 && !h.Player.Actions.IsBusy,
            "stronger, faster tool uses its definition and clamps overkill");

        h = new Harness();
        var frontSprite = new Sprite(null) { Position = h.Click, SourceRectangle = new Rectangle(0, 0, 100, 100), GroundOffsetY = 70 };
        var frontCell = new Point(1, 0);
        h.Grid.Occupy(frontCell, CellType.Plant, frontSprite);
        ResourceNode front = h.World.Register(frontSprite, frontCell, HarvestCatalog.Pine);
        check(ReferenceEquals(h.World.FindAt(h.Click), front), "overlapping resources select frontmost sprite");
        h.World.Remove(front);
        check(ReferenceEquals(h.World.FindAt(h.Click), h.Node), "removed foreground resource reveals resource behind it");

        // Exercise the real input router with explicit mouse samples and camera conversion.
        h = new Harness();
        var mouse = new MouseInput();
        var camera = new Camera2D(h.Click);
        var viewport = new Viewport(0, 0, 800, 600);
        var router = new MouseInteractionController(camera, null, h.Items, mouse)
            { TryPrimaryInteract = h.Controller.TryInteract };
        int requests = 0;
        router.TryPrimaryInteract = point => { requests++; return h.Controller.TryInteract(point); };
        void Frame(bool pressed, bool blocked = false)
        {
            mouse.Update(new MouseState(400, 300, 0,
                pressed ? ButtonState.Pressed : ButtonState.Released,
                ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            router.Update(Time(.1f), viewport, h.Player.GroundPosition, blocked);
        }
        Frame(true);
        h.Step(10);
        for (int i = 0; i < 20; i++) Frame(true);
        check(requests == 1 && h.Node.Health == 2, "holding left button cannot repeat damage after swing ends");
        Frame(false);
        Frame(true);
        h.Step(10);
        check(requests == 2 && h.Node.Health == 1, "release and fresh press starts exactly one more swing");
        Frame(false);
        Frame(true, blocked: true);
        Frame(true);
        check(requests == 2, "UI-blocked click is consumed and never buffered");

        var animation = new Animation(null, 4) { IsLooping = false, FrameDuration = .1f };
        var manager = new AnimationManager(animation);
        manager.Play(animation, restart: true, playbackRate: .4f / .8f);
        manager.Update(Time(.19f));
        check(manager.CurrentFrame == 0, "slower tool stretches animation playback");
        manager.Update(Time(.22f));
        check(manager.CurrentFrame == 2 && manager.IsPlaying, "third frame matches halfway impact for slower swing");
        manager.Update(Time(.4f));
        check(manager.CurrentFrame == 3 && !manager.IsPlaying, "all four frames finish once without looping");
        manager.Play(animation, restart: true);
        manager.Update(Time(.11f));
        check(manager.CurrentFrame == 1, "normal playback resets tool speed override");
    }

    sealed class Harness
    {
        public readonly WorldGrid Grid = new(100, Vector2.Zero, 5, 5);
        public readonly Point Cell = Point.Zero;
        public readonly Vector2 Click = new(50, 50);
        public readonly Player Player = new((Texture2D)null, Vector2.Zero) { Scale = 1 };
        public readonly Inventory Items = new(SampleItemCatalog.CreateDefinitions(), SampleItemCatalog.CreateBehaviors());
        public readonly Hotbar Hotbar;
        public readonly ResourceWorld World;
        public readonly ResourceNode Node;
        public readonly HarvestController Controller;
        public Harness()
        {
            Items.AddItem(HarvestCatalog.BasicAxeId);
            Hotbar = new Hotbar(Items);
            World = new ResourceWorld(Grid);
            var sprite = new Sprite(null) { Position = Click, SourceRectangle = new Rectangle(0, 0, 100, 100) };
            Grid.Occupy(Cell, CellType.Plant, sprite);
            Node = World.Register(sprite, Cell, HarvestCatalog.Tree);
            Controller = new HarvestController(Player, Hotbar, World, HarvestCatalog.Tools);
        }
        public void Step(float seconds) => Player.Actions.Update(Time(seconds), default);
    }
}
