# Adding player activities

## Try the fishing scaffold

Right-click lake water within 350 world units of the player's feet. The action faces
the water, plays the placeholder cast, then enters `Catching`. It waits there for your
own catch rules. Q cancels. Menus freeze the action and its animation.

There is no built-in minigame, bite timer, reaction window, or automatic catch. The
rod, line, and bobber are placeholder world visuals. The shared action system handles
movement restrictions, cancellation, and animation ownership for all activities.

The temporary F/G test controls have been removed. Fish no longer spawn as ordinary
ground pickups; wood and berries still do. No rod item is required by default.

## Ownership: where things belong

| File | Responsibility |
| --- | --- |
| `Actions/PlayerAction.cs` | Contract for one action attempt |
| `Actions/PlayerActionController.cs` | One active action, validation, completion, cancellation |
| `Actions/ActionInput.cs` | Shared confirm/hold/cancel input snapshot |
| `Entities/Player.cs` | Movement, facing, and animation playback |
| `Actions/TimedEffectAction.cs` | A timed swing/strike with one impact effect |
| `Actions/ConversationAction.cs` | Dialogue lines advanced with Space |
| `Fishing/FishingController.cs` | Water-click routing and result messages |
| `Fishing/FishingAction.cs` | Casting, custom catch hooks, and reward delivery |
| `Fishing/FishingSettings.cs` | Cast distance/duration and optional tool requirement |
| `Fishing/FishingSpot.cs` | Water cells, fish item, settings per spot |
| `UI/FishingOverlay.cs` | Placeholder rod, line, and bobber |
| `Game1.cs` | Creates objects; calls input, update, and draw in order |

`Player.Actions` is the authority for whether the player is busy. `ActionState` is a
read-only view of it. The old tutorial methods `TryBeginAction` and `EndAction` have
been replaced by `Actions.TryStart` and `Actions.Cancel`. Don't maintain a second busy flag.

## Create a new action

1. Put mechanic-specific files in a folder such as `Combat`, `Harvesting`, or `Dialogue`.
2. Reuse `TimedEffectAction` or `ConversationAction` when they fit. Otherwise subclass `PlayerAction`.
3. `CanStart`: check range, target validity, tools, stamina, etc. Return a useful reason on failure.
   Validation must not spend items or change the world.
4. `Begin`: face the target and start the initial animation. Spend any up-front costs here.
5. `Update`: use elapsed seconds and the supplied `ActionInput`. Apply gameplay effects here.
6. Call `Complete()` when done. `End` runs once for completion or cancellation; release any
   mechanic-specific resources here. The controller restores the player's idle pose.
7. Start a **fresh instance** with `cat.Actions.TryStart(action, out string reason)`.
8. Route the appropriate world interaction to it. Display `reason` if starting fails.

All actions block movement by default. Override `BlocksMovement` for attacks that permit
movement; their action animation still takes precedence over walking. Override `CanCancel`
for short committed actions. Cancellation never rolls back an impact that already happened.
Do not poll keyboard/mouse from an action, call `Player.Update` yourself, or award items in Draw.
Avoid starting another action from an action's lifecycle callbacks; sequence stages inside
the same action, or start the next action from your controller after the current one finishes.

`IsActionAnimationComplete` is available when a stage needs to follow actual animation
completion. `PlayActionAnimation` restarts the requested animation explicitly; call it
on stage entry, not every frame. It returns false and uses idle when artwork is missing.
Provide a timer fallback if a stage relies on completion and its artwork is optional.

## Example: chopping, mining, or fighting

The following is an integration example: `tree`, its health, and tool checks are objects
you supply. Existing trees are decorative `Sprite` objects, not damageable resource nodes.

```csharp
var chop = new TimedEffectAction(
    name: "Chopping",
    animation: "ChopRight",
    target: tree.GroundPosition,
    range: 180f,
    duration: 0.8f,
    effectTime: 0.45f,
    canInteract: () => tree.Health > 0 && HasAxe(),
    effect: () => tree.TakeDamage(1));

cat.Actions.TryStart(chop, out string reason);
```

Use the same class for `Mining` and `Attacking`, with different animations, checks, and
damage callbacks. The effect runs once when elapsed time crosses the impact time—even
on a long frame. Range and validity are checked again at impact. For moving enemies,
write a specialized action that queries the enemy's current position instead of using
the fixed target position in this helper.

Tree/rock health, loot tables, destruction, removing collision cells, and respawn belong
to the resource object/system. Award drops when health first crosses zero, not on every
strike. Tool durability or stamina spending also belongs in the action/effect you define.
The framework supports these mechanics; it does not invent enemies, NPCs, rocks, or
damageable trees in the existing world.

To support holding a button for repeated swings, request a NEW action after the prior
one ends and while input is still held. Never reset the active swing each frame.

## Example: talking

```csharp
var conversation = new ConversationAction(
    new[] { "Hello!", "Try fishing at the lake." },
    npcPosition,
    range: 200f);
cat.Actions.TryStart(conversation, out string reason);
```

There is no built-in action text overlay. Your own UI can read `CurrentLine`;
Space advances and Q leaves. An optional
`available` callback ends conversation if the NPC is removed. For portraits, wrapping,
branching choices, and quests, add a dedicated dialogue view/action. Keep dialogue
content separate from the shared action controller.

## Routing interactions

`MouseInteractionController.TryInteract` is a callback that receives a world position.
It currently points at `fishing.TryInteract`. To add targets, replace that assignment
in `Game1.LoadContent` with a method that checks water, NPCs, resource nodes, etc.
Return true when an interaction owns the click, including a rejected attempt on a
recognized target. Return false to allow the existing pickup code to handle it.
`Game1` suppresses ordinary interactions while an action or menu owns input.

## Add your own fishing minigame

The simplest option is to implement the two empty methods in `FishingAction.cs`:

- `BeginCatch(Player player)`: initialize your minigame after casting finishes.
- `UpdateCatch(Player player, GameTime time, ActionInput input)`: advance your rules.
- Call `SetMessage(...)` to store instructions for your own UI to read. No text is drawn automatically.
- Call `FinishCatch(true)` when your rules declare success, or `FinishCatch(false)`
  when they declare failure. These methods are protected: use them inside the action.

You can also keep the scaffold intact and create `MyFishingAction : FishingAction`:

```csharp
public class MyFishingAction : FishingAction
{
    public MyFishingAction(FishingSpot spot, Vector2 target,
        First_game.Inventory.Inventory inventory)
        : base(spot, target, inventory) { }

    protected override void BeginCatch(Player player)
    {
        // Initialize your own challenge here.
    }

    protected override void UpdateCatch(Player player, GameTime time, ActionInput input)
    {
        // Update your challenge here. Call FinishCatch when its result is known.
    }
}
```

Use the namespaces `First_game.Fishing`, `First_game.Entities`, `First_game.Actions`,
and `Microsoft.Xna.Framework` in that file. Then set the factory after constructing
`fishing` in `Game1.LoadContent`:

```csharp
fishing.CreateAction = (spot, target, inventory) =>
    new MyFishingAction(spot, target, inventory);
```

The factory must create a fresh action each time. The controller and world-click code
will then use your subclass automatically. Keep any minigame UI in its own UI file;
read your action's display properties when drawing. Draw must not award items.
If you allocate anything requiring cleanup, override `End` and call `base.End(player, reason)`
as well as your cleanup. Use the provided input snapshot and elapsed time so menus
continue to pause your rules correctly.

`FinishCatch` accepts results only during `Catching`, prevents duplicate rewards,
and checks the actual count accepted by inventory. Cancellation prevents late rewards.
Success adds one fish; failure adds none. The action controller restores movement on
its next update. Until you implement catch rules, fishing stays active until Q is pressed.

## Configure spots and artwork

Create or change `FishingSettings` when registering a spot in `Game1.LoadContent`.
Each spot defines water cells, a registered item ID, cast range, and cast duration.
The lake's occupied cells provide the initial targeting area; refine them if needed.
To require a rod, register a tool item, provide a way to obtain it, and set `RequiredToolId`.
This checks ownership, not equipment selection.

Inventory capacity is checked before casting and when delivering a successful catch.
If another system fills inventory during the attempt, the fish is released and a result
message is stored for your own UI. The framework does not draw that message.

For character artwork:

1. Put horizontal sprite sheets in `Content/Images/Fishing` and register them in `Content.mgcb`.
2. Add dictionary entries in `Game1.LoadContent` named `FishCastRight`, `FishCastLeft`,
   `FishCastFront`, `FishCastBack`, and equivalent `FishWait` entries.
3. Make casting non-looping and match `CastSeconds` to its duration. Waiting may loop.
4. Your custom rules can request additional animation keys with `player.PlayActionAnimation`.
5. Adjust hand/tip offsets in `FishingOverlay.DrawWorld`, or replace these placeholder visuals.

## Checks

Build: `dotnet build First_game/First_game.csproj -m:1`

Run logic checks (no graphics window required):
`dotnet run --project Tests/GameplayChecks/GameplayChecks.csproj -p:BuildInParallel=false`

Manual checks: cast on land/distant water; cancel during casting and catching; open/close
menus during an action; confirm movement returns. The automated checks also exercise
custom result delivery, duplicate/late rewards, inventory limits, and the custom action factory.
