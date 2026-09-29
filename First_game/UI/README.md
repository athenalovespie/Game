# Menus

- `UIManager` opens one menu at a time, handles shortcuts, and draws the shared dim overlay.
- `InventoryPanel` draws the existing 12-slot artwork and reads items through `Inventory.GetSlot()`.
- `CraftingPanel` is a placeholder for recipe selection and crafting controls.
- `PauseMenu` is a placeholder for pause/settings controls. Pause stops player movement, camera updates, and pickup respawn time.
- `IMenuPanel` defines the update and draw methods for each menu.
- `UIInput` provides the current keyboard, previous keyboard, and shared mouse input.

## Controls

E toggles Inventory. C toggles Crafting. P toggles Pause. Escape closes an open menu; from the world, it exits the game as before. E and C do not switch menus while paused.

## Add a menu

1. Add a value to `MenuType`.
2. Create a class implementing `IMenuPanel`. Pass its textures and gameplay services through its constructor.
3. Register it in `Game1.LoadContent()` using `uiManager.Register(...)`.
4. Open it with `uiManager.Open(...)`, or add a shortcut in `UIManager.Update()`.

Panels draw inside Game1's UI SpriteBatch. Do not call Begin/End inside a panel or use the world camera transform.

Keep gameplay rules in their own systems. For example, a Craft button should ask a crafting system to check ingredients and inventory space before making changes.

## Input and future controls

Game1 samples mouse input once per frame, then updates the UI before world interactions. Panels can use `input.Mouse.ScreenPosition`, `LeftClicked`, and `RightClicked`. Use the same screen rectangles to draw controls and check mouse hits.

`ConsumedInputThisFrame` also blocks world actions on the frame a menu closes, preventing clicks from reaching objects behind it. Inventory and Crafting block movement and collection but allow respawn timers to continue.

Slot dragging, crafting recipes, buttons, and layered confirmation popups are not implemented yet. The inventory artwork still supports exactly 12 slots; add paging or another layout before displaying an upgraded inventory.
