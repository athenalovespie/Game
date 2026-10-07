# Placeable items

Any item category can opt in through `ItemDefinition.placeable`; no placement controller,
renderer, or input changes are needed for another item. The project uses C# catalog data,
so new entries go in `Inventory/SampleItemCatalog.cs`, following its existing convention.

## Tent definition

```csharp
definitions.Register(new ItemDefinition(
    "(F)tent", "Tent", "Select, then left-click or E to place. Cancel placement before entering.", "Images/Tent",
    ItemCategory.Furniture, maxStackSize: 10, basePrice: 100, spawnerRuleId: "tent_demo_drop",
    placeable: new PlaceableData(5, 3, "Images/Tent",
        allowedGroundTypes: new[] { GroundType.Grass },
        maxRangeTiles: 6f, spriteScale: .15f, baseInsetPixels: 667f, groundOffsetX: -200f,
        enterable: new EnterableData(new Point(2, 2), "tent_small", new Point(3, 2)))));
```

The actual entry uses fully qualified placement/ground types. `Images/Tent` is the existing
MonoGame content asset built from `First_game/Content/Images/Tent.png`; there is no placeholder
path. The 5x3 footprint, scale, and transparent-art offsets preserve the previous static Tent.
The old hardcoded scenery Tent is replaced by a collectible item.

`World/WorldSpawnCatalog.cs` declares `tent_demo_drop`: two Tents at the `tent_demo` node near
world position `(250, 1450)`, with no respawn. A fresh game starts the cat at `(100, 1200)`;
its feet are near `(100, 1381)`. The pickup occupies the center of its tile, `(250, 1450)`.
Normal pickup collection adds to existing stacks, then fills empty inventory slots from
left to right. The starting axe is in slot 1, so the demo Tents normally arrive in slot 2.

## Controls and manual demo

1. Start a fresh game. Walk within 200 world pixels of the Tent pickup near the cat and
   right-click it. Confirm that hotbar slot 2 contains two Tents.
2. Press **2**, click the hotbar slot, or scroll to it. A translucent Tent and all fifteen
   footprint tiles follow the mouse. The cursor chooses the **bottom-center tile**;
   placement origin saved to disk is the footprint's **top-left tile**.
3. Aim at open grass near the player: the sprite and footprint turn green. Aim at a tree,
   house, lake, another pickup, the player's feet, or beyond six tiles: they turn red.
   Every tile center must be in range; a footprint crossing the world boundary is invalid.
4. Left-click green: exactly one Tent appears, the stack goes from 2 to 1, and the footprint
   becomes solid. Try walking through it or placing another Tent there: both are blocked.
   Clicking red leaves the inventory and world unchanged.
5. Right-click any occupied footprint tile while in range. The Tent returns to inventory
   and all fifteen tiles become free. If inventory is full it stays in the world.
6. Place both Tents. The empty stack immediately exits placement. Switch slots and press
   Escape to check cancellation. Escape cancels placement before its usual exit behavior.
   Opening inventory hides/suppresses placement; dragging, closing on release, changing
   menus, and returning while holding the mouse must never place an extra Tent.
7. With menus closed and no active action, press **F5** to save. Pick up or
   move a Tent, then press **F9**: the saved world and inventory return together. Restart
   and press F9 to verify disk restoration. No automatic load/save occurs on launch/exit.

F5/F9 use `%LOCALAPPDATA%/OurGame/world.json`. The snapshot includes inventory, placements,
active pickups, pending respawn deadlines, surviving resource health, player position, and
exterior elapsed time. Static scenery is reconstructed using a fixed random seed. Saves
are for this exterior layout/version; changing the world-generation catalog may invalidate
older saves. Loads that conflict with the world are rejected and rolled back. Saving also works inside the house and Tents. Interior owner/map IDs and stored items
are included. See [enterable places](../Interiors/README.md) for entry, pickup rules and the demo.

## Definition options

- `enterable`: optional entrance offset, interior template/spawn and unique/shared state; see `Interiors/README.md`.

- `width`, `height`: positive tile counts (up to 256 on either axis).
- `spriteAsset`: MonoGame asset name, loaded and cached at startup.
- `anchor`: `BottomCenter` (default; even widths choose the right middle tile) or `TopLeft`.
- `allowedGroundTypes`: defaults to Grass/Dirt/Sand. Water and Wall are always forbidden.
- `allowOverlap`: false by default. When true, object layers may overlap without replacing
  one another; water, walls, player bounds, and reserved door access still cannot be occupied.
- `consumeOnPlacement`: true by default. False retains the item as a reusable building
  tool; removing its objects returns no extra item, preventing duplication.
- `rotatable`: false by default. **R** rotates opted-in items in 90-degree steps, swapping
  width/height and rotating the sprite about its ground anchor. This is a simple art rotation,
  not separate direction-specific art. The Tent deliberately does not rotate.
- `maxRangeTiles`: defaults to six; compared against every occupied tile center.
- `spriteScale`, `baseInsetPixels`, `groundOffsetX`: fit existing art with transparent padding.
- `onPlacedEffect`: optional ID resolved once by `PlacementWorld` from its supplied effect
  registry. Unknown IDs fail startup. Effects run after successful placement, never on load;
  exceptions are logged after the committed placement remains intact. Existing effect IDs
  can be reused entirely through data; implementing a new behavior requires a new callback.

Add a normal world spawn rule and node if the new item should be found as a pickup. Its
item entry must name that spawn rule. Assets newly added to the project also need their
usual Content.mgcb entry; the existing Tent already has one.

## Ownership and validity

`PlacementWorld.IsPlacementValid(itemDefinition, originTile, rotation)` is the common entry
point for preview and actual placement. It checks every footprint tile using O(1) grid
lookups. Actual placement rechecks even if the last displayed ghost was green.

`Hotbar.SelectionChanged` and `SelectedItemChanged` drive mode changes. Inventory operations
publish `Changed`; the hotbar forwards changes only when the selected stack/reference changes.
The controller captures the selected item instance, and `Inventory.TryConsumeSlot` verifies
that exact instance at commit. Slot arrays are never accessed by the placement module.
No items are reserved by previews or drag gestures. UI input wins before world input, the
same mouse sample is shared, and both valid and invalid placement clicks stop tool handling.
The frame that closes a menu is also consumed, so held buttons cannot replay a UI click.

Successful placement reserves the footprint and consumes one item synchronously on the game
thread. Pickup checks inventory capacity first and then removes/refunds once. Quality and
durability survive placement, pickup, and saves. Non-consuming tools never receive a refund.
Post-commit effects must not attempt their own inventory payment.

The placement occupancy layer uses a dictionary of per-tile lists, preserving existing
scenery and optional overlapping objects. A per-cell count feeds existing movement collision
and prevents later random spawns from using those tiles. Removing a layer never clears the
static occupant or another placed object.

## Performance and tradeoffs

The controller is the one reusable ghost state. Snapping uses value types and a cached
immutable definition. Validity is recalculated only after a tile, rotation, player position/
bounds, selection, or grid revision changes. GridCell setters increment the revision, including
legacy public mutation paths. The revision is global: distant world changes can cause one
extra validation, avoiding a more complex per-region subscription system.

Rendering shares the game's existing 1x1 pixel texture for every highlight and preloads
placed textures. A placed object's draw delegate is cached when it is created. Preview
movement and validity checks allocate zero managed bytes after warm-up. Object/list allocations
are confined to registration, placement/removal, and saving/loading. Overlap removal is
O(N*K) for N footprint tiles and K objects on a tile; validity stays O(N). Normal no-overlap
removal is O(N). Save loading intentionally allocates staging state and rollback snapshots.

## Files

| File | Responsibility |
| --- | --- |
| `Placement/PlaceableData.cs` | Immutable definition, anchor and rotated size calculations |
| `Placement/PlacementObject.cs` | Placed instance state and cached draw callback |
| `Placement/OccupancyGrid.cs` | Per-tile placement layers and collision counts |
| `Placement/PlacementWorld.cs` | Shared validity, commit, pickup, effects, compact placement saves |
| `Placement/PlacementController.cs` | Event-driven mode and reusable cached preview |
| `Placement/PlacementRenderer.cs` | Loaded art, world draw submission, shared tile overlay |
| `Inventory/Item.cs` | Optional placeable block on every item definition |
| `Inventory/Inventory.cs` | Mutation events, exact-slot consume, restore, declared placeable stacking |
| `Inventory/Hotbar.cs` | Selection and selected-item events |
| `Inventory/SampleItemCatalog.cs` | Declarative Tent item |
| `World/WorldGrid.cs` | Ground types, revision, placement counts, no-build reservations |
| `World/WorldPickup.cs` | Size large placeable artwork as a compact pickup icon |
| `World/WorldPickupSystem.cs` | Exact node first attempt and pickup/respawn snapshots |
| `World/WorldSpawnCatalog.cs` | Tent pickup rule and node |
| `World/PickupSnapshot.cs` | Pickup save DTOs |
| `World/WorldSaveStore.cs` | Coordinated atomic-file save and rollback-capable world restore |
| `Harvesting/ResourceWorld.cs`, `ResourceNode.cs` | Resource occupancy/health save restoration |
| `UI/HotbarPanel.cs`, `MonoGameLibrary/Input/MouseInput.cs` | Scroll-wheel selection |
| `UI/UIManager.cs` | Escape cancellation before game exit |
| `Doors/DoorConfiguration.cs` | Reserve door access against overlap-enabled placements |
| `Game1.cs` | Wiring, input priority, terrain classification, F5/F9, rendering |
| `Tests/GameplayChecks/PlacementChecks.cs`, `Program.cs` | Placement checks and suite registration |

## Automated checks

```powershell
dotnet build Tests/GameplayChecks/GameplayChecks.csproj --no-restore -m:1
dotnet --roll-forward Major Tests/GameplayChecks/bin/Debug/net9.0/GameplayChecks.dll
```

The roll-forward argument permits the installed .NET 10 runtime to execute these net9.0
logic tests. They exercise footprint/anchor/rotation calculations; water, wall, range,
player and object rejection; occupancy add/remove; stale-green rejection; exact-instance
consumption; stacking; last-item exit; drag/menu/Escape/scroll routing; no double handling;
full-inventory pickup; overlapping layers; non-consuming tools; effects; save replacement,
corruption rejection, and coordinated rollback; and zero allocation while moving the ghost.
Visual tint, sprite alignment and mouse feel are covered by the manual scenario above.
