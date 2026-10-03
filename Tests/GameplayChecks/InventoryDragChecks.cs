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

            foreach (Keys closeKey in new[] { Keys.Escape, Keys.I, Keys.P, Keys.C })
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
