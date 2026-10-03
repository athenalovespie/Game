# Inventory, hotbar, and menus

## Controls

- **1, 2, 3, 4, 5, 6, 7, 8, 9, 0:** select hotbar slots 1 through 10. Numpad keys also work.
- **Left-click the hotbar:** select that slot.
- **E:** interact with an in-range door.
- **I:** open or close the inventory. Its top row contains the same ten slots as the hotbar.
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

Game1 samples the mouse once, updates menus, then updates the hotbar if no menu consumed input. Inventory and Crafting block movement and collection while allowing respawn timers to continue. Pause also stops world timers and camera updates. I/C do not switch menus while paused.

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
