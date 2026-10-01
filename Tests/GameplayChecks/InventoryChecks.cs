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
        var panel = new InventoryPanel(inventory, hotbar, null, null);
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
        panel.ClickSlot(29);
        panel.ClickSlot(9);
        check(hotbar.SelectedSlot == 9 && hotbar.SelectedItem == inventory.GetSlot(9)
            && hotbar.SelectedItem.Count == 12 && inventory.GetSlot(29) == null,
            "last storage slot moves into hotbar without duplicating items");

        inventory.AddItem("(O)blackberry", 2);
        panel.ClickSlot(0);
        panel.ClickSlot(9);
        check(hotbar.SelectedItem.QualifiedId == "(O)blackberry" && inventory.GetSlot(0).QualifiedId == "(O)wood",
            "swapping hotbar items immediately changes the selected item");
        check(!hotbar.TryUseSelectedItem(null) && hotbar.SelectedItem.Count == 2,
            "failed use preserves selected stack");
        check(hotbar.TryUseSelectedItem(new UseContext()) && inventory.GetSlot(9).Count == 1,
            "using selected item updates the shared inventory stack");
        check(hotbar.TryUseSelectedItem(new UseContext()) && hotbar.SelectedItem == null,
            "consuming last item leaves selected slot empty");

        inventory.SplitStack(0, 10, 5);
        panel.ClickSlot(10);
        panel.ClickSlot(0);
        check(inventory.GetSlot(0).Count == 12 && inventory.GetSlot(10) == null,
            "matching stacks merge across storage and hotbar");
        inventory.AddItem("(O)wood", 180); // 99, 93: merge only the six items that fit.
        panel.ClickSlot(0);
        panel.ClickSlot(1);
        check(inventory.GetSlot(0).Count == 93 && inventory.GetSlot(1).Count == 99
            && inventory.GetItemCount("(O)wood") == 192, "partial merge keeps all leftover items");

        // Closing or replacing a menu must cancel any pending item transfer.
        var menus = new UIManager(null);
        menus.Register(MenuType.Inventory, panel);
        menus.Register(MenuType.Pause, new PauseMenu(null));
        menus.Open(MenuType.Inventory);
        panel.ClickSlot(0);
        menus.Open(MenuType.Pause);
        menus.Open(MenuType.Inventory);
        panel.ClickSlot(20);
        check(inventory.GetSlot(20) == null && inventory.GetSlot(0).Count == 93,
            "switching menus cancels pending move without losing items");
        panel.ClickSlot(0);
        menus.Close();
        menus.Open(MenuType.Inventory);
        panel.ClickSlot(20);
        check(inventory.GetSlot(20) == null, "closing inventory cancels pending move");

        var viewport = new Viewport(0, 0, 1280, 720);
        var mouse = new MouseInput();
        menus.Update(new GameTime(), new UIInput(new KeyboardState(Keys.D0), default, mouse), viewport);
        check(hotbar.SelectedSlot == 9, "number keys select while inventory is open");
        menus.Open(MenuType.Pause);
        menus.Update(new GameTime(), new UIInput(new KeyboardState(Keys.D1), default, mouse), viewport);
        check(hotbar.SelectedSlot == 9 && menus.ConsumedInputThisFrame, "pause blocks hotbar selection");
        menus.Update(new GameTime(), new UIInput(new KeyboardState(Keys.Escape), default, mouse), viewport);
        check(menus.ActiveMenu == MenuType.None && menus.ConsumedInputThisFrame,
            "menu closing frame still blocks world input");

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
