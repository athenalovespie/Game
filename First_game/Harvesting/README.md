# Harvesting

The player starts with a Basic Axe in hotbar slot 1. Select it with **1**, walk
within **180 world units of a tree's base**, and **left-click the visible tree**.
Both tree varieties take three hits. Release and click again for each swing.
Right-click fishing and pickup interactions keep their existing behavior.

Each swing plays the supplied four frames once over 0.6 seconds. Damage occurs
at 0.3 seconds (the start of frame three); the player stays busy through recovery.
Q cancels before impact. After impact, recovery must finish so cancellation cannot
increase chopping speed. Menus and loss of focus freeze both action and animation.
Clicks while input is blocked are sampled and discarded, never queued.

## Ownership

| Type | Responsibility |
| --- | --- |
| `ToolDefinition` | Immutable damage, range, swing duration, impact fraction, tool kind and animation keys |
| `ResourceDefinition` | Immutable resource ID, required tool kind and maximum health |
| `HarvestCatalog` | Initial tool/species balance and item-to-tool lookup |
| `ResourceNode` | One resource's health, compatible-hit validation, hit/depletion notifications |
| `ResourceWorld` | Placed resource lifetime, visible-pixel selection, rendering and collision cleanup |
| `HarvestController` | Click targeting, selected hotbar item lookup, action creation and failure reason |
| `HarvestAction` | One swing's timing, target/tool/range validation, one impact and recovery |
| Existing `PlayerActionController` | Exclusive action ownership, movement lock, cancellation and idle restoration |

`Game1` composes these objects, registers placed trees and submits resource drawing.
`Player` handles movement/facing/animation and has no harvesting or tree-health rules.
`MouseInteractionController.TryPrimaryInteract` routes fresh left presses in world
coordinates; `TryInteract` retains right-click interaction routing.

```text
Fresh left press -> select resource + equipped tool -> start HarvestAction
  -> wind-up -> revalidate -> ResourceNode.TryHit (once) -> recovery -> idle
                                  |
                                  +-> health reaches zero
                                      -> remove sprite and collision
                                      -> ResourceWorld.Depleted notification
```

The selected resource and item instance are captured when the swing starts. Moving
the cursor cannot retarget the swing. At impact the system checks that the resource
still belongs to this world, is alive, accepts the tool, is in range, and that the
same item is selected and is not broken. A failed impact spends the swing but deals
no damage. Large elapsed times cross the impact only once, even if they also cross
completion. Damage never runs in `Draw`.

## Change the balance

Edit `HarvestCatalog.cs`. Currently both `Tree` and `Pine` have 3 health and the
Basic Axe has 1 damage. `SwingSeconds` controls both action duration and animation
playback speed without modifying shared animation definitions. `ImpactProgress`
is a fraction of the swing: `0.5f` starts frame three in a four-frame clip; `1f`
applies the hit at completion. Range is measured from the player's feet to the
resource's grid base, independently of the clicked canopy pixel.

To add a Copper Axe:

1. Register its item and icon in `SampleItemCatalog` (stack size 1).
2. Add a `ToolDefinition` keyed by that same item ID in `HarvestCatalog.Tools`.
3. Use kind `"axe"` and choose damage, speed, range and animation keys.
4. Grant the item through inventory, crafting or your future shop system.

A rock can use `new ResourceDefinition("rock", "pickaxe", 6)` and a pickaxe tool
definition. The same controller/action/health logic works. A tree species just
needs a resource definition and artwork. The current world adapter adopts one-cell
plants placed by `GridPlacer`; multi-cell resources would extend that adapter's
placement record and cleanup, without changing tool or health rules.

## Artwork and targeting

`ChopRight` loads `Left_Axe`, and `ChopLeft` loads `Right_Axe`: the supplied art faces
opposite to its filenames. Each sheet is 2405 x 725; the horizontal slicer uses four
601-pixel frames and leaves one trailing column unused. `Animation.DrawOffset`
compensates for the extra axe space around the character. Adjust these art offsets
in `Game1.LoadContent` if the sheets are replaced. They never move collision.
Above/below targets use a side clip chosen from the target's horizontal position
until front/back chopping artwork exists.

Selection checks the sprite rectangle, then its alpha mask, so transparent canvas
around a tree does not intercept another tree's click. Alpha masks are read once
per texture on first selection and cached as bits. The first read temporarily
allocates a full pixel buffer; a larger game can preload or build masks in the
content pipeline. Overlapping resources use the same Y order as drawing.
This is resource picking, not general occlusion or line-of-sight testing against
buildings; add that requirement to action validation if the game needs it.

## Extension points

- **Sound, particles, hit reactions:** subscribe to `ResourceNode.Hit`. It reports
  actual damage (clamped to remaining health), once per accepted hit. Presentation
  should not apply more damage. Node depletion/cleanup is notified before the final
  hit notification, so handlers must allow an already-depleted resource.
- **Wood and other drops:** subscribe to `ResourceWorld.Depleted` and resolve drops
  from `node.Definition.Id`. This fires once, after occupancy is freed. Use a drop
  service for inventory overflow/world pickups; no wood reward is implemented yet.
- **Stamina, durability, skills:** extend action validation and the single impact
  path with the desired cost/damage policy. Keep runtime durability on the existing
  `ItemInstance`, not on `ToolDefinition`. The system rejects zero durability but
  does not spend durability or stamina.
- **Falling animations:** retain a separate visual after depletion, then remove it
  when its animation finishes. Decide when collision is released in `ResourceWorld`.
- **Regrowth:** record depleted placements in a world scheduler and create a fresh
  node only when its cells are available. Do not resurrect a captured swing target.
- **Save/load:** definition IDs and per-node health are separated. A future save DTO
  should contain stable placement IDs/cells, definition IDs, health and any respawn
  timestamp; reconstruct sprites and subscriptions on load. Persistence is not
  implemented, and random initial placement remains the startup behavior.
- **Additional requirements:** keep start/impact validation in the action (or extract
  a policy once shared requirements exist). `HarvestController.LastFailure` is ready
  for UI feedback; this change does not add a message overlay.

## Validation

Run `dotnet run --project Tests/GameplayChecks/GameplayChecks.csproj -p:BuildInParallel=false`.
`HarvestChecks` covers multiple hits, once-only impact under long frames, held mouse
input, cancelled wind-up, protected recovery, tool changes, missing/broken tools,
range changes, removed targets, collision cleanup, tool tiers, resource kinds,
overlapping targets and animation playback speed.

For a visual smoke test, approach each tree variety, click its trunk/canopy, and
watch four frames per click. Hold the button through completion, switch slots during
wind-up, open/close inventory mid-swing, and walk through the cleared cell after the
third hit. Check left/right alignment and transparent-space clicks at game resolution.
