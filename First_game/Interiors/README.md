# Enterable places

The house and placeables use `Enterable` destinations passed through the existing
`DoorSystem`. That system remains the scene transition service: movement notifications,
spatial triggers, E binding, prompt overlay, fade, collision switch, player spawn and
camera snap all follow the existing house path. There is no second Tent transition loop.

## House verification

`Content/doors.json` is unchanged. Its exterior trigger remains `[870,500,160,90]`,
interior ground spawn `(500,480)`, exterior return `(950,675)`, interior floor 1000x700,
walls 32 pixels, E interaction, and each fade half 0.25 seconds. House overlap still
uses the feet collision rectangle. The house's fixed return remains protected by its
reserved access area; its existing blocked-destination failure behavior is unchanged.

`Door` wraps fixed house destinations in the same `Enterable` resolver used by placed
objects. `DoorSystem` now permits registering/removing lazy rooms, publishes map events,
and supports restoring a saved location. `ResidentArea` preserves the house rug and
colors; templates can omit the rug. The house can now be saved/loaded while inside.
The existing `DoorChecks` still pass, including the real house footprint, camera-event
trigger, held-key suppression and zero-allocation transition tests.

Interaction ownership now also applies to the house: while placing, E places the
selected item; cancel placement or select a non-placeable before entering any building.

## Tent data and template

The project already defines items in a C# data catalog. Its optional `enterable` block
in `Inventory/SampleItemCatalog.cs` is:

```csharp
placeable: new PlaceableData(5, 3, "Images/Tent",
    allowedGroundTypes: new[] { GroundType.Grass },
    maxRangeTiles: 6f, spriteScale: .15f, baseInsetPixels: 667f, groundOffsetX: -200f,
    enterable: new EnterableData(
        entranceOffset: new Point(2, 2),
        interiorTemplateId: "tent_small",
        interiorSpawnTile: new Point(3, 2)))
```

Offsets are zero-based relative to the footprint top-left. The outward direction defaults
to `(0,1)` and `shared` defaults to `false`. The entrance must be an edge tile pointing
outward. Both its offset and direction rotate clockwise when `rotatable: true`; the
existing Tent keeps its nonrotating artwork setting. The approach trigger is the single
tile immediately outside the entrance. For placeables, only the player's ground point
activates that tile, so a wide actor overlapping a neighboring tile cannot enter there.

`Content/interiors.json` supplies the template:

```json
[
  { "id": "tent_small", "widthTiles": 6, "heightTiles": 5,
    "tileSize": 100, "wallThickness": 16, "exitTile": [3, 4] }
]
```

The selected room size is **6x5 tiles** (600x500 pixels), with solid perimeter walls,
spawn ground `(350,250)`, and a brown exit tile near the south wall. Stand on that tile
and press E to exit, as for the house. Named `PlaceholderFloor`, `PlaceholderWall` and
`PlaceholderExit` colors in `ResidentArea` use the existing pixel texture. No new bitmap
assets or content pipeline entries are required. The room is empty; furniture rendering
can later be added to the area, and stored items already have a persistent inventory.
There is no storage/furniture UI in this change; future interactions can use
`InteriorManager.ActiveContents` through the normal inventory APIs.

To add a cabin or shed, add its ordinary item data with an `EnterableData` block and an
interior template JSON entry. No controller, Game1, transition, or save-code changes are
needed. Existing item/art registration rules still apply. Templates and spawn/exit bounds
are validated and cached at startup; malformed template references fail registration.

## State, saves and lifecycle

Every placement receives a stable `InstanceId`, included in placement saves. First entry
creates its state under `instance:<placement ID>`; placing alone creates no interior.
With `shared: true`, instances referencing the same template share `shared:<template ID>`.
The actual entrance placement ID is recorded separately, so shared interiors exit through
the Tent that was entered, including after loading.

Version-2 world saves include active map ID, active placement ID (or null), the existing
player position, and each created interior's template ID and inventory state. Definitions,
textures and templates are never embedded. F5/F9 work inside both Tent and house, with
menus closed and no action/transition running. Restore validates references, item stacks
and collision before switching maps. The existing world transaction rolls back inventory,
placements, resources, pickups, room state and player location together on failure.
Version-1 saves remain exterior saves; missing placement IDs are generated on migration.

Room objects and trigger subscriptions are released on exit; only the compact inventory
state remains until re-entry. Removing the last owning placeable releases that state too.
The exterior stays resident and retains its respawn clock, but heavy exterior updates,
fishing, world clicks and exterior rendering are inactive while inside.

`Player.MapId` is updated before location notifications. Systems must use this with the
player position, or `DoorSystem.ActiveArea`, rather than interpreting interior coordinates
as exterior coordinates. There are currently no NPC map trackers to migrate. The service
publishes `OnEnterInterior`, `OnExitInterior`, `AreaChanged` and `TransitionStarted` for
future saving, music and sleeping hooks.

## Edge cases and input

- Pickup is denied through `PlacementWorld.TryPickUp` while in any interior or transition.
  Outside, pickup is denied if that interior inventory has items; shared contents block
  every owner. Empty interiors can be picked up normally, subject to range/inventory space.
- `IsPlacementValid` checks the outside tile and the actor's complete collision bounds.
  Occupancy, water/walls, neighboring collisions and grid edges can make the preview red.
  Commit uses exactly the same check. Restore intentionally permits an already placed
  Tent's approach to have become blocked, so it can still be loaded and exited safely.
- Exit first tries the entrance's approach position, adjusted for wide actors at rotated
  side entrances. If blocked, it chooses the nearest free tile by squared world distance
  with deterministic ties. If the entire exterior is blocked, it fades back inside and
  logs an error, preserving state and releasing input.
- Placement owns E and left-click, including invalid placement. A simultaneous E/click
  can place only once. Escape cancels placement; selecting an ordinary/empty slot also
  ends it. Inside, placement is cancelled so it cannot suppress the exit.
- Inventory/pause/crafting menus consume interaction, preserving the house's behavior;
  close them before entering. No interaction is buffered. Starting a transition cancels
  drag previews, closes any UI and cancels placement. Drag previews never reserve or
  remove real items, so cancellation loses or duplicates nothing.
- A transition consumes world input for its complete fade. An action that finishes on
  this frame retains input ownership for that frame, preventing the same press from
  completing an action and entering/placing/using a tool.
- Return/spawn overlap disarms that trigger until the player leaves and comes back.

## Demo scenario

1. On a fresh game, collect the two Tent items near `(250,1450)` by right-clicking nearby.
   They normally fill hotbar slot 2 after the starting axe. Select that slot.
2. Aim at nearby clear grass. Confirm the footprint preview is green and place with
   left-click or E. Aim so the entrance approach touches an obstacle to verify red rejection.
3. Press Escape once to cancel placement. Walk to the tile just south of the footprint's
   bottom-center tile; confirm `Press E to enter`. Adjacent tiles must show no prompt.
4. Press E. Confirm the same fade as the house, the 6x5 room and feet at `(350,250)`.
   Walk into the walls and verify collision.
5. Walk onto the brown exit tile and press E. Confirm the return immediately outside
   this Tent and no automatic re-entry. Step away/back before trying another entry.
6. Enter again; walk to a different interior position and press F5. Exit, then press F9.
   Confirm the saved Tent interior and exact player position return. Restart and F9 to
   test the on-disk case. Exit, then right-click the footprint to recover the empty Tent.
7. Place and enter both Tents separately. Automated checks insert stored items using the
   inventory API to verify independent state and the nonempty pickup rule; no storage UI
   is required for this empty-room demo.
8. Repeat the existing house checklist in `../Doors/README.md`; additionally save/load
   inside the house. Check E plus a simultaneous mouse click, inventory drags near an
   entrance, and selecting a Tent while inside before exiting.

## Verification and tradeoffs

```powershell
dotnet build Tests/GameplayChecks/GameplayChecks.csproj --no-restore -m:1
dotnet --roll-forward Major Tests/GameplayChecks/bin/Debug/net9.0/GameplayChecks.dll
```

`InteriorChecks` covers four rotated entrance/return paths with the real 135x32 actor,
entrance-only prompts, water/neighbor/grid-boundary clearance, preview/commit agreement,
nearest valid return and fully blocked exits, lazy creation, distinct/shared inventories,
inside saves, corrupt-load rollback, pickup rules, cleanup and fixed-house restoration.
Examples: `(2,2)` in a 5x3 footprint rotates to `(0,2)` facing left; water on the outside
tile rejects placement; blocking `(150,150)` in a 3x3 grid selects `(150,50)` as the nearest
free return on a distance tie. Visual artwork alignment, fade appearance and mouse feel
still need the manual demo; the automated suite does not create a graphics window.

Normal prompt checks remain event-driven spatial bucket queries on player movement.
Metadata lookups and trigger-index rebuilds occur on registration or placement changes,
not in a per-frame loop over objects. House transitions retain their zero-allocation path.
Tent first entry allocates state; subsequent entry allocates lightweight room/trigger
objects because inactive rooms are released rather than pooled. This bounds resident room
memory and avoids pooled ownership bugs. Exit fallback scans the finite grid only when
its preferred return is blocked. Save/restore uses staging and rollback allocations in
exchange for consistency. Trigger index rebuilds cost O(number of placed portals) per
placement change, a simpler tradeoff than an incremental mutable spatial index.

## File map

| File | Change |
| --- | --- |
| `Interiors/Enterable.cs` | Shared destination component for fixed and lazy portals |
| `Interiors/EnterableData.cs` | Immutable definition, rotated entrance and return geometry |
| `Interiors/InteriorRegistry.cs` | Cached templates and validation |
| `Interiors/InteriorManager.cs` | Instance ownership, inventories, lazy rooms, persistence and cleanup |
| `Interiors/ReturnPosition.cs` | Full-bounds clearance and deterministic nearest return |
| `Content/interiors.json` | 6x5 Tent template |
| `Inventory/SampleItemCatalog.cs` | Tent enterable data and control hint |
| `Placement/PlaceableData.cs` | Optional enterable block |
| `Placement/PlacementObject.cs` | Persistent instance ID |
| `Placement/PlacementWorld.cs` | Clearance validation, pickup guard and lifecycle notifications |
| `Doors/Door.cs` | House/static and placeable resolver adapters |
| `Doors/DoorSystem.cs` | Existing fade service generalized for dynamic rooms, map events and load |
| `Doors/DoorTriggers.cs` | Rebuildable spatial index and entrance-only ground checks |
| `Doors/ResidentArea.cs` | Named placeholder art and optional rug |
| `Doors/DoorConfiguration.cs` | Clarified fixed-house preload comment; configuration unchanged |
| `Entities/Player.cs` | Explicit map identity |
| `World/WorldSaveStore.cs` | Version-2 interior snapshot and coordinated restore/rollback |
| `Game1.cs` | Service wiring, input priority, UI cleanup and inside save/load |
| `First_game.csproj` | Copy interior templates to build/publish output |
| `Tests/GameplayChecks/InteriorChecks.cs`, `Program.cs` | New regression coverage and registration |
| `Placement/README.md`, `Doors/README.md`, `Interiors/README.md` | Updated controls, persistence, file map and demo |
