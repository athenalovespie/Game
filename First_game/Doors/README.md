# House doors

## Architecture

Player.PositionChanged drives DoorTriggers' spatial buckets. Only buckets touched by the
player's final collision bounds are queried. Enter/exit callbacks change the prompt.
DoorSystem owns the fade, input lock, active ResidentArea and spawn placement.
DoorOverlay caches prompt measurements when its text changes and draws the final fade.
DoorConfiguration resolves all area/spawn references once from Content/doors.json.

The full loop is: resolve movement -> overlap callbacks -> fresh interaction press ->
lock input and hide prompt -> fade out -> validate destination -> change active area,
move the same player and snap camera -> fade in -> release input. A failed destination
keeps the source area and player position, logs the error, and fades back in.
Startup preload failures leave the exterior playable with door interaction disabled.

Both areas remain resident. Interior geometry uses the existing white pixel texture;
preloading is small synchronous initialization during LoadContent, before play begins.
There is no disk access, task wait or GPU resource creation during transitions.
Inactive exterior updates and its respawn clock are paused. Inventory, hotbar, actions
and the player remain the same objects. This project has no health system to recreate.

The door update, overlap callbacks and successful transitions allocate no managed
memory after initialization. DoorChecks measures this over 1,000 warmed-up transitions.
Existing inventory/actions/renderer code can allocate outside this door path; the test
is not a claim that the entire application allocates zero bytes. Game1 now reuses its
world GameTime, and player facing uses cached idle-animation literals.

## Setup in this MonoGame project

1. Build First_game/First_game.csproj. C# files are included automatically by the SDK.
   The project copies Content/doors.json to both build and publish output.
2. No scene editor nodes, attached components, new textures or MGCB entries are needed.
   Game1.LoadContent constructs and connects the systems using the existing player,
   font, pixel texture, house sprite and exterior WorldGrid.
3. Open Content/doors.json. InteractionKey defaults to E and InventoryKey to Tab.
   Use distinct MonoGame Keys names. FadeSeconds is the duration of each half of the fade.
4. The exterior house footprint is x=400..1500, y=100..500. Its front-door trigger
   is [870, 500, 160, 90], directly in front of the artwork's central door.
   Rectangles use [x, y, width, height], in world pixels. Change width/height to tune range.
   Overlap uses the player's feet collision rectangle, not its full artwork.
5. The interior floor is [0, 0, 1000, 700] with 32-pixel walls.
   The spawn's Ground is the player's feet anchor. Facing is a direction vector:
   [0, -1] looks into the room; [0, 1] faces away from the exterior front door.
6. Each door assigns SourceArea, Trigger, TargetArea, TargetSpawn, and IsExit.
   Exterior doors also assign AccessBounds covering their approach and return spawn.
   These cells are reserved before scenery placement but remain walkable.
7. There are no engine collision layers. Exterior movement uses WorldGrid's
   BlocksMovement cells. Interior movement is constrained to WalkableBounds.
   DoorTriggers is a separate nonblocking spatial index for the active area only.
8. Add another room by adding an Areas entry and named Spawns, then a door pair
   targeting those names. Add entrances to already placed artwork by adding Doors
   entries. The door system needs no C# changes. New exterior building artwork still
   uses the project's existing GridPlacer setup.
9. Keep spawn collision bounds outside triggers. If a spawn does overlap a trigger,
   it stays disarmed until the player leaves and re-enters, even after releasing E.
10. Run the game, approach the center of the house's south wall, and press E.
    The exit is the brown threshold at the bottom of the room.

Diagnostics go to Trace (the IDE's Debug output) and standard error.
To test an unavailable resident area in the debugger, set its IsReady to false.
An invalid/blocked destination is checked at the black frame and safely rejected.

## Manual test checklist

- Approach the front door: see "Press E to enter"; walk away: prompt disappears.
- Hold E before approaching: no entry until release and another press.
- Move out of range while pressing E: no transition.
- Press E in range: smooth fade out/in, one player inside, no lingering prompt.
- Spam E and movement/menu/hotbar keys during both fades: no double swap or input.
- Walk against all four interior walls and corners: collision contains the player.
- Walk to the brown exit threshold: see "Press E to exit". Exit and verify the
  player stands at ground [950, 675], faces down, and has no prompt.
- Collect or move an inventory item before entry. Return and confirm inventory,
  hotbar selection, collected objects and exterior placement are preserved.
- Repeat entry/exit quickly; controls and prompts recover after each fade.
- Set the entry spawn to [500, 630] temporarily, rebuild and enter: the exit stays
  disarmed until you leave its region and walk back in. Restore [500, 480].
- Change InteractionKey to F, rebuild: prompt and interaction both use F.
- Temporarily rename doors.json in the output directory, or break its JSON:
  startup logs a preload error and exterior movement/inventory still work.
- While inside, set the exterior target IsReady to false in the debugger:
  exit fades back to the room and releases input. Restore true and retry.
- Open/close inventory or pause near the door and alt-tab during a fade:
  prompts remain hidden while input is unavailable; no buffered E press fires.

## Automated checks

```powershell
dotnet build Tests/GameplayChecks/GameplayChecks.csproj --no-restore -m:1
dotnet --roll-forward Major Tests/GameplayChecks/bin/Debug/net9.0/GameplayChecks.dll
```

DoorChecks covers the full round trip, actual configured bounds, held/rebound input,
movement on the interaction frame, fade spam, failed destinations, long frames,
spawn overlap suppression, prompt change counts, state identity and allocations.
