# Regrowing resources

Loose blackberry spawning is replaced by blocking 1x1 plants. The inventory definition in
`Inventory/SampleItemCatalog.cs` is unchanged: ID `(O)blackberry`, icon `Images/Blackberry`,
food behavior, stack size, and existing recipe/shop references all remain valid.

## Data and spawn entries

Edit `Content/harvestables.json` to change sprites, footprint, yield, prompt, range, or timing.
The existing compiled sprites are `Images/BlackBerryBush1` (ripe) and `Images/Bush1` (empty);
their PNGs are already registered in `Content/Content.mgcb`. Asset names are case-sensitive
on some platforms. SpriteScale is 0.1 and the bottom-center footprint anchor stays fixed.

The `blackberry_bush_drop` rule and `blackberry_bush` node configuration moved out of
`World/WorldSpawnCatalog.cs` into the JSON. The rule now targets the `blackberry_bush` resource
type instead of creating a pickup. It retains five nodes, 85% chance, the rectangle
(-600, 400) through (1200, 1600), 16 placement attempts within two tiles, and a 90-second
retry after failed chance/placement. Bushes persist after harvesting, so the old pickup
respawn delay is replaced by 60-second berry regrowth. Yield remains 1–3 Blackberries.
The shared placement check additionally rejects water, walls, reserved entrances, occupied
tiles, and the actor's current bounds. These validity constraints take precedence over density.

Add another Plants entry with a unique type ID, unique spawn rule/node type, states, yield,
and Spawns entries; add its artwork to the content build. No core-code changes are required.
Set Minimum equal to Maximum for a fixed yield. BonusChance (0–1) and BonusQuantity are optional.
The generic transition is RipeState -> AfterHarvestState -> RipeState; the state list supports
extra artwork states but arbitrary multi-stage crop progression is not implemented.

## Runtime behavior

The shared MouseInput.RightClicked edge handles harvesting a nearby bush. No harvest prompt is drawn.
Movement notifications query spatial buckets, and state changes refresh only local candidates.
There is no world-wide per-frame interaction scan. A ripe nearby plant is preferred over an
empty neighbor. Placement mode has priority; otherwise a plant consumes right-click before pickups
and blocks simultaneous world actions. Empty plants consume the click without harvesting.
E remains available for doors and placement. UI and hotbar clicks cannot harvest.

The full-yield rule is all-or-nothing: roll once, check Inventory.HasSpaceFor, then use the
public Inventory.AddItem. Insufficient capacity retains ripe state and the rolled yield,
including across saves, and invokes PackFull. No slot arrays are mutated. A reentrancy guard
and immediate state transition prevent inventory event callbacks or rapid presses from
awarding twice. OnHarvested(instance, quantity), OnRegrown(instance), and PackFull(instance)
are optional sound/particle/feedback hooks.

One manager owns a pre-sized priority queue. It peeks the earliest deadline and processes
all due records; it never updates individual countdowns. Each registered node owns one
reusable instance and Sprite, including nodes awaiting placement. Cached definitions,
textures, and draw delegates are resolved at registration/load. Regrowth swaps the cached
texture and state, refreshes the local candidate, and invokes OnRegrown.

The clock measures elapsed gameplay seconds across maps. P pauses it; focus loss and door
fades also freeze it. Inventory/crafting menus do not pause gameplay in this project.
Quitting the application does not advance this clock. Off-screen and inactive-map plants
remain in the shared manager and continue regrowing while the player is elsewhere.
This project retains logical grid/resource state even when an interior presentation is
released; there is no per-map resource update dependency. RegisterMap accepts any map ID
and logical WorldGrid; register its nodes and call RebuildTriggers after the batch. Retain
that logical state while its scene is unloaded. On deserializing an area, Restore processes
overdue timestamps immediately against the saved gameplay clock. Register its logical grid
before restoring plants. Game1 currently configures plants only in the exterior.

Version-3 world saves include the shared clock and per-instance ID, type ID, map, tile, state,
regrow timestamp, and any pending initial spawn/yield. They do not serialize definitions or
sprites. Inventory, occupancy, plants and interiors participate in the same rollback.
Version-1/2 loose blackberry pickups migrate to ripe plants at their saved tiles; pending
pickup respawns become pending plant spawns. A legacy saved tile that cannot accommodate
the new blocking footprint causes a load rejection with rollback, rather than overlap.

## Files

- HarvestableData.cs: validated immutable state/yield/footprint metadata and O(1) registry.
- HarvestableCatalog.cs and ../Content/harvestables.json: registration and editable definitions/spawns.
- HarvestableInstance.cs: identity, tile, cached Sprite and mutable state/timestamps.
- RegrowScheduler.cs: earliest-deadline queue and validated timestamp calculation.
- HarvestableManager.cs: lifecycle, occupancy, yields, events, map-independent clock, migration.
- HarvestableInteraction.cs: movement/state callbacks, nearby candidates and shared right-click routing.
- HarvestableSaveData.cs: runtime-only persistence records.
- ../Input/ISpatialTrigger.cs and SpatialTriggers.cs: shared spatial trigger index.
- ../Doors/Door.cs and DoorTriggers.cs: door compatibility with shared trigger index.
- ../Doors/DoorSystem.cs: additional map resource collision checks, including arrivals.
- ../Placement/PlacementValidity.cs and PlacementWorld.cs: shared placement validation.
- ../World/WorldSpawnCatalog.cs, WorldSpawnRule.cs, WorldSpawnRuleRegistry.cs and
  WorldPickupSystem.cs: typed resource-spawn dispatch instead of blackberry pickups.
- ../World/WorldSaveStore.cs: version-3 persistence, migration and rollback.
- ../Game1.cs: data loading, clock, interaction ownership, drawing, save/load and disposal.
- ../First_game.csproj: copy the JSON to output/publish directories.
- ../../Tests/GameplayChecks/RegrowingChecks.cs and Program.cs: regression scenarios.

## Demo and edge cases

1. Run the game and walk through the blackberry rectangle near the starting area. There
   should be bushes instead of loose berry icons. Five nodes may not all be ripe/spawned
   initially because their original 85% chance is preserved.
2. Select the axe or an empty hotbar slot (so placement mode is inactive). Approach a bush;
   confirm no harvest prompt appears and that its tile blocks walking.
3. Right-click once while in range. Open inventory with Tab and verify 1–3 ordinary Blackberry items. The
   bush must now use Bush1 and have no harvest prompt. Holding right-click and repeated clicks must
   award nothing until regrowth; a simultaneous click must not swing/place.
4. Wait 60 seconds of active gameplay. The berry sprite must return, with no text prompt. Harvest again.
   In a debugger, calling regrowing.Advance(60, false) skips the wait without a shipping cheat key.
5. Fill inventory, including leaving only one space in a nearly-full Blackberry stack.
   A yield that cannot fit in full must leave the bush ripe and preserve all items. Free a
   slot and harvest the same rolled yield.
6. Harvest, wait about 20 seconds, F5 save, then F9 load. It should still need about 40
   gameplay seconds. Loading again must replace instances rather than duplicate them.
7. Harvest, enter the house/tent, spend at least 60 active seconds there, and return. The
   bush must be ripe. Saving/loading while inside must also preserve the exterior deadline.
8. Harvest, press P, wait a minute, then unpause. The paused minute must not count.
9. Verify E still enters a nearby door without harvesting, and right-click over the hotbar
   or an open menu never harvests a bush.

Automated checks cover scheduler ordering, timestamp validation (12.5 + 60 = 72.5), fixed/
ranged/bonus yields, state transitions, whole/partially-full inventories, callback reentrancy,
held/repeated input, competing doors, movement exit, inactive interior maps, mid-regrow
round trips, overdue loads, old-save migration, transactional rollback, blocked placement
retries, and hot-path allocation counts.

Run from the repository root:

```powershell
dotnet build Tests/GameplayChecks/GameplayChecks.csproj --no-restore -m:1
dotnet --roll-forward Major Tests/GameplayChecks/bin/Debug/net9.0/GameplayChecks.dll
```

The runtime roll-forward is needed on this machine because it has .NET 10 and the project
targets .NET 9. Automated checks are headless; the visual walkthrough is provided for an
in-game art/interaction review.

## Flexibility and performance

JSON parsing, validation, sprite loading, instance creation and spatial-index rebuilding
allocate only at registration/load. Queue capacity is reserved for every node, including
failed spawns. Retry spawning, regrowth and harvesting into existing stacks allocate no
managed objects in the new system. The existing public Inventory.AddItem still allocates
an ItemInstance when creating a new inventory stack; inventory UI count text and user-added
event handlers can allocate as well. Thus this does not claim zero allocations for the
entire inventory/UI operation. Avoiding those would require changing inventory object
identity/pooling semantics, which are shared with dragging and placement.

Logical instances/grids remain resident across scene unloading, trading some memory for
simple correct deadlines and cached rendering data. A future large-world streamer can
serialize these runtime records and restore against the same gameplay clock. Rendering
still submits visible-map bushes each frame, as MonoGame requires; it does not rebuild
sprites or textures. No decorative regrowth effect or bush-object churn is introduced.
