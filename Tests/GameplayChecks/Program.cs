using System;
using First_game.Actions;
using First_game.Entities;
using First_game.Fishing;
using First_game.Inventory;
using First_game.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

static class Checks
{
    static int passed;
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAILED: " + name);
        passed++;
        Console.WriteLine("PASS: " + name);
    }
    static GameTime Time(float seconds) => new(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));
    static Player NewPlayer() => new((Texture2D)null, Vector2.Zero) { Scale = 1 };
    static Inventory NewInventory() => new(SampleItemCatalog.CreateDefinitions(), SampleItemCatalog.CreateBehaviors());
    static FishingSpot Spot(FishingSettings settings = null) => new(
        new WorldGrid(100, Vector2.Zero, 10, 10), new[] { new Point(1, 0) }, "(O)fish", settings ?? new());
    static readonly Vector2 Water = new(150, 50);

    static void Main()
    {
        Player player = NewPlayer();
        int hits = 0;
        var swing = new TimedEffectAction("Chop", null, Vector2.Zero, 100, 1, .5f, () => true, () => hits++);
        Check(player.Actions.TryStart(swing, out _), "start action without artwork");
        Check(player.Actions.BlocksMovement, "action locks movement");
        Check(!player.Actions.TryStart(new ConversationAction(new[] { "Hello" }, Vector2.Zero), out _), "reject overlapping actions");
        player.Actions.Update(Time(.9f), default);
        player.Actions.Update(Time(.9f), default);
        Check(hits == 1 && !player.Actions.IsBusy, "long frame crosses impact once and releases player");
        Check(!player.Actions.TryStart(swing, out _), "reject reusing completed instance");

        var cancelled = new TimedEffectAction("Mine", null, Vector2.Zero, 100, 1, .5f, () => true, () => hits++);
        player.Actions.TryStart(cancelled, out _);
        player.Actions.Update(Time(.1f), new(false, false, true));
        Check(hits == 1 && !player.Actions.IsBusy, "cancel before impact produces no reward");
        bool valid = true;
        player.Actions.TryStart(new TimedEffectAction("Attack", null, Vector2.Zero, 100, 1, .5f, () => valid, () => hits++), out _);
        valid = false;
        player.Actions.Update(Time(1), default);
        Check(hits == 1, "target validity is checked again at impact");

        var talk = new ConversationAction(new[] { "One", "Two" }, Vector2.Zero);
        player.Actions.TryStart(talk, out _);
        player.Actions.Update(Time(.1f), new(false, true, false));
        Check(talk.CurrentLine == "One", "holding confirm does not skip dialogue");
        player.Actions.Update(Time(.1f), new(true, true, false));
        Check(talk.CurrentLine == "Two", "fresh confirm advances dialogue");
        player.Actions.Update(Time(.1f), new(true, true, false));
        Check(!player.Actions.IsBusy, "dialogue completion releases player");

        var inventory = NewInventory();
        var fishing = new FishingController(player, inventory);
        fishing.Register(Spot());
        Check(!fishing.TryInteract(Vector2.Zero), "land falls through to other interactions");
        player.Position = new Vector2(900, 900);
        Check(fishing.TryInteract(Water) && !player.Actions.IsBusy, "distant water consumed without starting");
        player.Position = Vector2.Zero;
        var scaffold = new FishingAction(Spot(), Water, inventory);
        player.Actions.TryStart(scaffold, out _);
        player.Actions.Update(Time(1), default);
        Check(scaffold.State == FishingState.Catching, "cast hands off to custom catch stage");
        player.Actions.Update(Time(100), new(true, true, false));
        Check(player.Actions.IsBusy && inventory.GetItemCount("(O)fish") == 0,
            "scaffold waits without built-in rules or automatic reward");
        player.Actions.Cancel();
        Check(scaffold.State == FishingState.Cancelled, "scaffold cancels");

        var custom = new CustomFishing(Spot(), Water, inventory);
        player.Actions.TryStart(custom, out _);
        player.Actions.Update(Time(1), default);
        custom.Resolve(true);
        custom.Resolve(true);
        player.Actions.Update(Time(.01f), default);
        Check(!player.Actions.IsBusy && inventory.GetItemCount("(O)fish") == 1,
            "custom success awards once and releases player");

        var failure = new CustomFishing(Spot(), Water, inventory);
        player.Actions.TryStart(failure, out _);
        player.Actions.Update(Time(1), default);
        failure.Resolve(false);
        player.Actions.Update(Time(.01f), default);
        Check(failure.State == FishingState.Failed && inventory.GetItemCount("(O)fish") == 1,
            "custom failure awards nothing");

        var full = NewInventory();
        full.AddItem("(O)wood", 12 * 99);
        Check(!player.Actions.TryStart(new FishingAction(Spot(), Water, full), out _), "full inventory rejects cast");
        full.RemoveItem("(O)wood", 99);
        full.AddItem("(O)fish", 98);
        Check(full.HasSpaceFor("(O)fish"), "fish fits existing stack without empty slot");

        var lateFull = NewInventory();
        var lateCatch = new CustomFishing(Spot(), Water, lateFull);
        player.Actions.TryStart(lateCatch, out _);
        player.Actions.Update(Time(1), default);
        lateFull.AddItem("(O)wood", 12 * 99);
        lateCatch.Resolve(true);
        player.Actions.Update(Time(.01f), default);
        Check(lateCatch.State == FishingState.Failed && lateFull.GetItemCount("(O)fish") == 0,
            "inventory filling during attempt handled honestly");

        for (int stage = 0; stage < 2; stage++)
        {
            var cancelFish = new CustomFishing(Spot(), Water, inventory);
            player.Actions.TryStart(cancelFish, out _);
            cancelFish.Resolve(true);
            Check(inventory.GetItemCount("(O)fish") == 1, "cannot reward before custom catch stage");
            if (stage == 1) player.Actions.Update(Time(1), default);
            player.Actions.Update(Time(.01f), new(false, false, true));
            cancelFish.Resolve(true);
            Check(cancelFish.State == FishingState.Cancelled && !player.Actions.IsBusy
                && inventory.GetItemCount("(O)fish") == 1, "cancel prevents late reward at stage " + stage);
        }
        fishing.CreateAction = (spot, target, items) => new CustomFishing(spot, target, items);
        fishing.TryInteract(Water);
        Check(fishing.Active is CustomFishing, "factory starts user-defined fishing action");
        player.Actions.Cancel();
        InventoryChecks.Run(Check);
        Console.WriteLine($"{passed} gameplay checks passed.");
    }
}

// Test-only catch rules. Production scaffold intentionally has none.
sealed class CustomFishing : FishingAction
{
    public CustomFishing(FishingSpot spot, Vector2 target, Inventory inventory) : base(spot, target, inventory) { }
    public void Resolve(bool success) => FinishCatch(success);
}
