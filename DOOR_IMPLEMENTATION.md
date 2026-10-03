# Door implementation

## 1. Architecture

Player movement emits overlap events through a spatial trigger index.
DoorSystem locks input, fades out, validates and switches the resident area, moves the
existing player to the configured spawn, and fades in. DoorOverlay changes its prompt
only on state changes. DoorConfiguration resolves JSON destinations once at startup.
Game1 updates and draws only the active area's gameplay. The exterior, player,
inventory and hotbar stay resident.

Controls: E interacts with doors; I opens inventory. Both are configurable.
This is 2D MonoGame: setup is in code and JSON, with no editor nodes or collision layers.

## 2. Every created or modified file

Status | File
--- | ---
Created | `First_game/Doors/Door.cs`
Created | `First_game/Doors/DoorTriggers.cs`
Created | `First_game/Doors/ResidentArea.cs`
Created | `First_game/Doors/DoorSystem.cs`
Created | `First_game/UI/DoorOverlay.cs`
Created | `First_game/Doors/DoorConfiguration.cs`
Created | `First_game/Content/doors.json`
Modified | `MonoGameLibrary/Input/InputBindings.cs`
Modified | `First_game/Entities/Player.cs`
Modified | `First_game/UI/UIManager.cs`
Modified | `First_game/First_game.csproj`
Modified | `First_game/Game1.cs`
Created | `Tests/GameplayChecks/DoorChecks.cs`
Modified | `Tests/GameplayChecks/Program.cs`
Modified | `First_game/UI/README.md`
Created | `First_game/Doors/README.md`
Modified | `Tests/GameplayChecks/InventoryDragChecks.cs`
Created | `DOOR_IMPLEMENTATION.md` (this complete source bundle)

## 3. Full code and file contents

All files below are unabridged snapshots of the delivered files. The setup guide and
manual checklist follow the source listings.


### First_game/Doors/Door.cs

````csharp
using System;
using Microsoft.Xna.Framework;

namespace First_game.Doors;

public readonly record struct AreaSpawn(Vector2 GroundPosition, Vector2 FacingDirection);

public sealed class Door
{
    public string Id { get; }
    public Rectangle TriggerBounds { get; }
    public ResidentArea TargetArea { get; }
    public AreaSpawn TargetSpawn { get; }
    public bool IsExit { get; }
    public string DestinationError { get; }
    public string Prompt { get; internal set; }

    internal bool SuppressedUntilExit;
    internal int Visit;

    public Door(string id, Rectangle triggerBounds, ResidentArea targetArea,
        AreaSpawn targetSpawn, bool isExit)
    {
        if (triggerBounds.Width <= 0 || triggerBounds.Height <= 0)
            throw new ArgumentException("A door trigger must have positive dimensions.");
        Id = id;
        TriggerBounds = triggerBounds;
        TargetArea = targetArea ?? throw new ArgumentNullException(nameof(targetArea));
        TargetSpawn = targetSpawn;
        IsExit = isExit;
        DestinationError = "Door '" + id + "' cannot enter '" + targetArea.Id
            + "': destination unavailable or spawn blocked.";
    }
}
````

### First_game/Doors/DoorTriggers.cs

````csharp
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace First_game.Doors;

// MonoGame has no physics triggers. Movement notifications query only touched
// spatial buckets, then emit enter/exit callbacks; stationary players do no work.
public sealed class DoorTriggers
{
    private const int CellSize = 128;
    private readonly Dictionary<Point, Door[]> buckets = new();
    private readonly Door[] overlaps;
    private int count;
    private int visit;
    public Door[] Doors { get; }
    public Door Candidate
    {
        get
        {
            for (int i = 0; i < count; i++)
                if (!overlaps[i].SuppressedUntilExit) return overlaps[i];
            return null;
        }
    }

    public event Action<Door> Entered;
    public event Action<Door> Exited;

    public DoorTriggers(Door[] doors)
    {
        Doors = (Door[])doors.Clone();
        overlaps = new Door[doors.Length];
        var building = new Dictionary<Point, List<Door>>();
        foreach (Door door in Doors)
        {
            GetCells(door.TriggerBounds, out Point first, out Point last);
            for (int x = first.X; x <= last.X; x++)
                for (int y = first.Y; y <= last.Y; y++)
                {
                    var cell = new Point(x, y);
                    if (!building.TryGetValue(cell, out List<Door> list))
                        building.Add(cell, list = new List<Door>());
                    list.Add(door);
                }
        }
        foreach (var entry in building)
            buckets.Add(entry.Key, entry.Value.ToArray());
    }

    public void Reset()
    {
        while (count > 0)
        {
            Door door = overlaps[--count];
            overlaps[count] = null;
            door.SuppressedUntilExit = false;
            Exited?.Invoke(door);
        }
    }

    public void Observe(Rectangle bounds, bool spawning = false)
    {
        for (int i = count - 1; i >= 0; i--)
        {
            Door door = overlaps[i];
            if (door.TriggerBounds.Intersects(bounds)) continue;
            door.SuppressedUntilExit = false;
            overlaps[i] = overlaps[--count];
            overlaps[count] = null;
            Exited?.Invoke(door);
        }

        // Unsigned wrap is harmless: only doors visited in this call share the stamp.
        visit = unchecked(visit + 1);
        if (visit == 0)
        {
            for (int i = 0; i < Doors.Length; i++) Doors[i].Visit = 0;
            visit = 1;
        }
        GetCells(bounds, out Point first, out Point last);
        for (int x = first.X; x <= last.X; x++)
            for (int y = first.Y; y <= last.Y; y++)
            {
                if (!buckets.TryGetValue(new Point(x, y), out Door[] nearby)) continue;
                for (int i = 0; i < nearby.Length; i++)
                {
                    Door door = nearby[i];
                    if (door.Visit == visit) continue;
                    door.Visit = visit;
                    if (!door.TriggerBounds.Intersects(bounds)) continue;
                    bool present = false;
                    for (int j = 0; j < count; j++)
                        if (ReferenceEquals(overlaps[j], door)) { present = true; break; }
                    if (present) continue;
                    door.SuppressedUntilExit = spawning;
                    overlaps[count++] = door;
                    Entered?.Invoke(door);
                }
            }
    }

    private static void GetCells(Rectangle bounds, out Point first, out Point last)
    {
        first = new Point((int)MathF.Floor(bounds.Left / (float)CellSize),
            (int)MathF.Floor(bounds.Top / (float)CellSize));
        last = new Point((int)MathF.Floor((bounds.Right - 1) / (float)CellSize),
            (int)MathF.Floor((bounds.Bottom - 1) / (float)CellSize));
    }
}
````

### First_game/Doors/ResidentArea.cs

````csharp
using First_game.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace First_game.Doors;

public sealed class ResidentArea
{
    private readonly WorldGrid exteriorGrid;
    public string Id { get; }
    public Rectangle Floor { get; }
    public Rectangle WalkableBounds { get; }
    public bool IsExterior => exteriorGrid != null;
    public bool IsReady { get; set; } = true;
    public DoorTriggers Triggers { get; private set; } = new DoorTriggers(System.Array.Empty<Door>());

    public ResidentArea(string id, WorldGrid exteriorGrid)
    {
        Id = id;
        this.exteriorGrid = exteriorGrid ?? throw new System.ArgumentNullException(nameof(exteriorGrid));
    }

    public ResidentArea(string id, Rectangle floor, int wallThickness)
    {
        if (wallThickness <= 0 || floor.Width <= wallThickness * 2
            || floor.Height <= wallThickness * 2)
            throw new System.ArgumentException("The room must have walls and a usable floor.");
        Id = id;
        Floor = floor;
        WalkableBounds = new Rectangle(floor.X + wallThickness, floor.Y + wallThickness,
            floor.Width - wallThickness * 2, floor.Height - wallThickness * 2);
    }

    // Configure once before subscribing to callbacks.
    public void SetDoors(Door[] doors) => Triggers = new DoorTriggers(doors);

    public bool BlocksMovement(Rectangle bounds) => IsExterior
        ? exteriorGrid.IntersectsBlockedCell(bounds)
        : !WalkableBounds.Contains(bounds);

    public void Draw(SpriteBatch batch, Texture2D pixel)
    {
        batch.Draw(pixel, Floor, new Color(77, 57, 49));
        batch.Draw(pixel, WalkableBounds, new Color(186, 151, 105));
        for (int y = WalkableBounds.Top + 48; y < WalkableBounds.Bottom; y += 48)
            batch.Draw(pixel, new Rectangle(WalkableBounds.Left, y, WalkableBounds.Width, 2),
                new Color(155, 122, 83));
        Rectangle rug = new Rectangle(Floor.Center.X - 160, Floor.Center.Y - 100, 320, 200);
        batch.Draw(pixel, rug, new Color(90, 116, 107));
        batch.Draw(pixel, new Rectangle(rug.X + 8, rug.Y + 8, rug.Width - 16, rug.Height - 16),
            new Color(125, 149, 123));
        for (int i = 0; i < Triggers.Doors.Length; i++)
        {
            Rectangle trigger = Triggers.Doors[i].TriggerBounds;
            batch.Draw(pixel, trigger, new Color(131, 83, 56));
            batch.Draw(pixel, new Rectangle(trigger.Center.X - 18, trigger.Center.Y - 3, 36, 6),
                new Color(232, 196, 109));
        }
    }
}
````

### First_game/Doors/DoorSystem.cs

````csharp
using System;
using First_game.Entities;
using First_game.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace First_game.Doors;

public sealed class DoorSystem : IDisposable
{
    private enum TransitionPhase { Idle, FadeOut, FadeIn }
    private readonly Player player;
    private readonly ResidentArea[] areas;
    private readonly Action<string> logError;
    private readonly float fadeSeconds;
    private TransitionPhase phase;
    private float elapsed;
    private Door pendingDoor;
    private bool interactionEnabled = true;
    private bool placingSpawn;

    public ResidentArea ActiveArea { get; private set; }
    public bool IsTransitioning => phase != TransitionPhase.Idle;
    public float FadeOpacity { get; private set; }
    public string Prompt { get; private set; }
    public string LastError { get; private set; }
    public event Action AreaChanged;
    public event Action<string> PromptChanged;

    public DoorSystem(Player player, ResidentArea[] areas, ResidentArea initialArea,
        float fadeSeconds, Action<string> logError)
    {
        if (!float.IsFinite(fadeSeconds) || fadeSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(fadeSeconds));
        this.player = player;
        this.areas = (ResidentArea[])areas.Clone();
        this.fadeSeconds = fadeSeconds;
        this.logError = logError;
        ActiveArea = initialArea;
        for (int i = 0; i < areas.Length; i++)
        {
            areas[i].Triggers.Entered += OnOverlapChanged;
            areas[i].Triggers.Exited += OnOverlapChanged;
        }
        player.PositionChanged += OnPlayerMoved;
        player.Input.InteractChanged += RefreshBindings;
        player.IsMovementBlocked = BlocksMovement;
        RefreshBindings();
        ActiveArea.Triggers.Observe(player.Bounds, spawning: true);
    }

    private bool BlocksMovement(Rectangle bounds) => ActiveArea.BlocksMovement(bounds);
    private void OnPlayerMoved()
    {
        if (!placingSpawn) ActiveArea.Triggers.Observe(player.Bounds);
    }
    private void OnOverlapChanged(Door door) => RefreshPrompt();

    // Bindings change through configuration, so strings are built only on that event.
    private void RefreshBindings()
    {
        string enter = "Press " + player.Input.Interact + " to enter";
        string exit = "Press " + player.Input.Interact + " to exit";
        for (int i = 0; i < areas.Length; i++)
            for (int j = 0; j < areas[i].Triggers.Doors.Length; j++)
            {
                Door door = areas[i].Triggers.Doors[j];
                door.Prompt = door.IsExit ? exit : enter;
            }
        RefreshPrompt();
    }

    public void SetInteractionEnabled(bool enabled)
    {
        if (interactionEnabled == enabled) return;
        interactionEnabled = enabled;
        RefreshPrompt();
    }

    private void RefreshPrompt()
    {
        string next = interactionEnabled && !IsTransitioning
            ? ActiveArea.Triggers.Candidate?.Prompt : null;
        if (ReferenceEquals(next, Prompt)) return;
        Prompt = next;
        PromptChanged?.Invoke(next);
    }

    public void Update(float seconds, UIInput input)
    {
        if (phase == TransitionPhase.Idle)
        {
            if (!interactionEnabled || !input.Pressed(player.Input.Interact)) return;
            Door door = ActiveArea.Triggers.Candidate;
            if (door == null || player.Actions.IsBusy) return;
            LastError = null;
            pendingDoor = door;
            phase = TransitionPhase.FadeOut;
            elapsed = 0;
            player.InputLocked = true;
            player.RestoreIdleAnimation();
            RefreshPrompt();
            return;
        }

        elapsed += MathF.Max(0, seconds);
        float fraction = MathHelper.Clamp(elapsed / fadeSeconds, 0, 1);
        FadeOpacity = phase == TransitionPhase.FadeOut ? fraction : 1 - fraction;
        if (fraction < 1) return;

        if (phase == TransitionPhase.FadeOut)
        {
            SwapArea();
            phase = TransitionPhase.FadeIn;
            elapsed = 0;
            // Keep a fully black frame even when elapsed time exceeds both fades.
            FadeOpacity = 1;
        }
        else
        {
            phase = TransitionPhase.Idle;
            pendingDoor = null;
            player.InputLocked = false;
            FadeOpacity = 0;
            RefreshPrompt();
        }
    }

    private void SwapArea()
    {
        Door door = pendingDoor;
        ResidentArea target = door.TargetArea;
        Vector2 position = player.PositionForGround(door.TargetSpawn.GroundPosition);
        Rectangle bounds = player.BoundsAt(position);

        // Validate before committing anything: failures leave the source area,
        // player and collision delegate intact, then fade back and release input.
        if (!target.IsReady || target.BlocksMovement(bounds))
        {
            LastError = door.DestinationError;
            logError?.Invoke(LastError);
            return;
        }

        ActiveArea.Triggers.Reset();
        ActiveArea = target;
        ActiveArea.Triggers.Reset();
        placingSpawn = true;
        player.Position = position;
        player.FaceTowards(player.GroundPosition + door.TargetSpawn.FacingDirection);
        player.RestoreIdleAnimation();
        placingSpawn = false;
        ActiveArea.Triggers.Observe(player.Bounds, spawning: true);
        AreaChanged?.Invoke();
    }

    public void Dispose()
    {
        player.PositionChanged -= OnPlayerMoved;
        player.Input.InteractChanged -= RefreshBindings;
        for (int i = 0; i < areas.Length; i++)
        {
            areas[i].Triggers.Entered -= OnOverlapChanged;
            areas[i].Triggers.Exited -= OnOverlapChanged;
        }
        player.InputLocked = false;
    }
}
````

### First_game/UI/DoorOverlay.cs

````csharp
using First_game.Doors;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace First_game.UI;

public sealed class DoorOverlay
{
    private readonly DoorSystem doors;
    private readonly Texture2D pixel;
    private readonly SpriteFont font;
    private string prompt;
    private Vector2 promptSize;

    public DoorOverlay(DoorSystem doors, Texture2D pixel, SpriteFont font)
    {
        this.doors = doors;
        this.pixel = pixel;
        this.font = font;
        doors.PromptChanged += SetPrompt;
        SetPrompt(doors.Prompt);
    }

    private void SetPrompt(string value)
    {
        if (ReferenceEquals(prompt, value)) return;
        prompt = value;
        promptSize = value == null ? Vector2.Zero : font.MeasureString(value);
    }

    public void Draw(SpriteBatch batch, Viewport viewport)
    {
        if (prompt != null)
        {
            Vector2 position = new Vector2((viewport.Width - promptSize.X) * .5f, 36);
            batch.Draw(pixel, new Rectangle((int)position.X - 12, 28,
                (int)promptSize.X + 24, (int)promptSize.Y + 16), Color.Black * .8f);
            batch.DrawString(font, prompt, position, Color.White);
        }
        if (doors.FadeOpacity > 0)
            batch.Draw(pixel, viewport.Bounds, Color.Black * doors.FadeOpacity);
    }
}
````

### First_game/Doors/DoorConfiguration.cs

````csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using First_game.Entities;
using First_game.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace First_game.Doors;

public sealed class DoorConfiguration
{
    public Keys InteractionKey { get; set; } = Keys.E;
    public Keys InventoryKey { get; set; } = Keys.I;
    public float FadeSeconds { get; set; } = .25f;
    public AreaDefinition[] Areas { get; set; } = Array.Empty<AreaDefinition>();
    public DoorDefinition[] Doors { get; set; } = Array.Empty<DoorDefinition>();

    public static DoorSystem Preload(string path, Player player, WorldGrid exteriorGrid,
        Action<string> logError)
    {
        var exterior = new ResidentArea("exterior", exteriorGrid);
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new JsonStringEnumConverter());
            DoorConfiguration config = JsonSerializer.Deserialize<DoorConfiguration>(
                File.ReadAllText(path), options)
                ?? throw new InvalidDataException("Door configuration is empty.");
            if (config.InteractionKey == Keys.None || config.InventoryKey == Keys.None
                || config.InteractionKey == config.InventoryKey)
                throw new InvalidDataException("Door and inventory keys must be distinct and enabled.");
            if (!float.IsFinite(config.FadeSeconds) || config.FadeSeconds <= 0)
                throw new InvalidDataException("FadeSeconds must be positive and finite.");

            var areas = new Dictionary<string, ResidentArea>(StringComparer.Ordinal)
            {
                { "exterior", exterior }
            };
            var spawns = new Dictionary<string, Dictionary<string, AreaSpawn>>(StringComparer.Ordinal);
            var doors = new Dictionary<string, List<Door>>(StringComparer.Ordinal);
            foreach (AreaDefinition definition in config.Areas)
            {
                ResidentArea area;
                if (definition.Id == "exterior")
                    area = exterior;
                else
                {
                    area = new ResidentArea(definition.Id, RectangleFrom(definition.Floor),
                        definition.WallThickness);
                    areas.Add(definition.Id, area);
                }
                var areaSpawns = new Dictionary<string, AreaSpawn>(StringComparer.Ordinal);
                foreach (SpawnDefinition spawn in definition.Spawns)
                {
                    Vector2 ground = VectorFrom(spawn.Ground);
                    Vector2 facing = VectorFrom(spawn.Facing);
                    if (facing == Vector2.Zero)
                        throw new InvalidDataException("Spawn facing must be nonzero.");
                    if (area.BlocksMovement(player.BoundsAt(player.PositionForGround(ground))))
                        throw new InvalidDataException("Spawn is blocked: " + definition.Id + "/" + spawn.Id);
                    areaSpawns.Add(spawn.Id, new AreaSpawn(ground, facing));
                }
                spawns.Add(definition.Id, areaSpawns);
                doors.Add(definition.Id, new List<Door>());
            }
            if (!spawns.ContainsKey("exterior"))
                throw new InvalidDataException("An exterior area with a return spawn is required.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (DoorDefinition definition in config.Doors)
            {
                if (!ids.Add(definition.Id))
                    throw new InvalidDataException("Duplicate door: " + definition.Id);
                var door = new Door(definition.Id, RectangleFrom(definition.Trigger),
                    areas[definition.TargetArea], spawns[definition.TargetArea][definition.TargetSpawn],
                    definition.IsExit);
                doors[definition.SourceArea].Add(door);
            }
            var residents = new ResidentArea[areas.Count];
            int index = 0;
            foreach (var entry in areas)
            {
                entry.Value.SetDoors(doors[entry.Key].ToArray());
                residents[index++] = entry.Value;
            }

            // Reserve approach and spawn cells before random scenery/pickups are placed.
            // They remain walkable; existing building collision is never cleared.
            foreach (DoorDefinition definition in config.Doors)
                if (definition.SourceArea == "exterior")
                    Reserve(exteriorGrid, RectangleFrom(definition.AccessBounds));
            foreach (AreaSpawn spawn in spawns["exterior"].Values)
                Reserve(exteriorGrid, player.BoundsAt(player.PositionForGround(spawn.GroundPosition)));

            player.Input.Interact = config.InteractionKey;
            player.Input.Inventory = config.InventoryKey;
            // Rooms use the already-loaded pixel texture. Build them once at startup:
            // no ContentManager or GPU work, disk I/O, or allocations during a fade.
            return new DoorSystem(player, residents, exterior, config.FadeSeconds, logError);
        }
        catch (Exception error)
        {
            logError("House interior preload failed; exterior control remains available. " + error);
            exterior.SetDoors(Array.Empty<Door>());
            return new DoorSystem(player, new[] { exterior }, exterior, .25f, logError);
        }
    }

    private static void Reserve(WorldGrid grid, Rectangle bounds)
    {
        Point first = grid.WorldToCell(new Vector2(bounds.Left, bounds.Top));
        Point last = grid.WorldToCell(new Vector2(bounds.Right - 1, bounds.Bottom - 1));
        for (int x = first.X; x <= last.X; x++)
            for (int y = first.Y; y <= last.Y; y++)
            {
                var cell = new Point(x, y);
                if (grid.IsValidCell(cell) && grid.CanPlace(cell))
                    grid.Occupy(cell, CellType.Building, blocksMovement: false);
            }
    }

    private static Rectangle RectangleFrom(int[] values)
    {
        if (values == null || values.Length != 4 || values[2] <= 0 || values[3] <= 0)
            throw new InvalidDataException("Rectangles require [x, y, positive width, positive height].");
        return new Rectangle(values[0], values[1], values[2], values[3]);
    }

    private static Vector2 VectorFrom(float[] values)
    {
        if (values == null || values.Length != 2
            || !float.IsFinite(values[0]) || !float.IsFinite(values[1]))
            throw new InvalidDataException("Vectors require two finite numbers.");
        return new Vector2(values[0], values[1]);
    }

    public sealed class AreaDefinition
    {
        public string Id { get; set; }
        public int[] Floor { get; set; }
        public int WallThickness { get; set; } = 32;
        public SpawnDefinition[] Spawns { get; set; } = Array.Empty<SpawnDefinition>();
    }

    public sealed class SpawnDefinition
    {
        public string Id { get; set; }
        public float[] Ground { get; set; }
        public float[] Facing { get; set; }
    }

    public sealed class DoorDefinition
    {
        public string Id { get; set; }
        public string SourceArea { get; set; }
        public int[] Trigger { get; set; }
        public int[] AccessBounds { get; set; }
        public string TargetArea { get; set; }
        public string TargetSpawn { get; set; }
        public bool IsExit { get; set; }
    }
}
````

### First_game/Content/doors.json

````json
{
  "InteractionKey": "E",
  "InventoryKey": "I",
  "FadeSeconds": 0.25,
  "Areas": [
    {
      "Id": "exterior",
      "Spawns": [
        {
          "Id": "front-step",
          "Ground": [
            950,
            675
          ],
          "Facing": [
            0,
            1
          ]
        }
      ]
    },
    {
      "Id": "house",
      "Floor": [
        0,
        0,
        1000,
        700
      ],
      "WallThickness": 32,
      "Spawns": [
        {
          "Id": "entry",
          "Ground": [
            500,
            480
          ],
          "Facing": [
            0,
            -1
          ]
        }
      ]
    }
  ],
  "Doors": [
    {
      "Id": "front-door",
      "SourceArea": "exterior",
      "Trigger": [
        870,
        500,
        160,
        90
      ],
      "AccessBounds": [
        770,
        500,
        360,
        300
      ],
      "TargetArea": "house",
      "TargetSpawn": "entry",
      "IsExit": false
    },
    {
      "Id": "house-exit",
      "SourceArea": "house",
      "Trigger": [
        425,
        588,
        150,
        80
      ],
      "TargetArea": "exterior",
      "TargetSpawn": "front-step",
      "IsExit": true
    }
  ]
}
````

### MonoGameLibrary/Input/InputBindings.cs

````csharp
using System;
using Microsoft.Xna.Framework.Input;

namespace MonoGameLibrary.Input;

public class InputBindings
{
    private Keys interact = Keys.E;
    public Keys Down { get; set; } = Keys.S;
    public Keys Left { get; set; } = Keys.A;
    public Keys Right { get; set; } = Keys.D;
    public Keys Up { get; set; } = Keys.W;
    public Keys Inventory { get; set; } = Keys.I;

    public Keys Interact
    {
        get => interact;
        set
        {
            if (interact == value) return;
            interact = value;
            InteractChanged?.Invoke();
        }
    }

    public event Action InteractChanged;
}
````

### First_game/Entities/Player.cs

````csharp
using System;
using System.Collections.Generic;
using System.Linq;
using MonoGameLibrary.Graphics;
using MonoGameLibrary.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using First_game.Actions;


namespace First_game.Entities;


public class Player
{
    protected AnimationManager _animationManager;

    protected Dictionary<string, Animation> _animations;
    private Sprite _sprite;
    private Vector2 _position;

    private Vector2 _velocity;

    public InputBindings Input { get; } = new InputBindings();

    public PlayerActionController Actions { get; }
    public PlayerActionState ActionState => Actions.IsBusy ? PlayerActionState.Acting : PlayerActionState.Free;
    public Vector2 GroundPosition => new Vector2(Bounds.Center.X, Bounds.Bottom);
    public string Facing { get; private set; } = "Right";

    public bool IsActionAnimationComplete
    {
        get
        {
            return ActionState == PlayerActionState.Acting
            && _animationManager != null
            && !_animationManager.IsPlaying;
        }
    }

    public float Speed;
    public float Scale;
    public Vector2 CollisionSize {get; set; }
    public Vector2 CollisionOffset {get; set; }

    public Func<Rectangle, bool> IsMovementBlocked { get; set; }

    public bool InputLocked { get; set; }
    public event Action PositionChanged;

    public Rectangle Bounds => BoundsAt(Position);

    public Rectangle BoundsAt(Vector2 position)
    {
            float width = (CollisionSize.X * Scale);
            float height = (CollisionSize.Y * Scale);

            Vector2 collisionCenter = position + CollisionOffset*Scale;

            return new Rectangle(
                (int)(collisionCenter.X - width * 0.5f),
                (int)(collisionCenter.Y - height * 0.5f),
                (int)width,
                (int)height);
    }

    public Vector2 PositionForGround(Vector2 ground)
    {
        float width = CollisionSize.X * Scale;
        float height = CollisionSize.Y * Scale;
        return new Vector2(ground.X + width * .5f - (int)width / 2,
            ground.Y - (int)height + height * .5f) - CollisionOffset * Scale;
    }

    private string _idleAnimation = "Right_Idle";

    public Vector2 Position
    {
        get { return _position; }
        set
        {
            if (_position == value) return;
            Rectangle previous = Bounds;
            _position = value;
            if (Bounds != previous) PositionChanged?.Invoke();
        }
    }
    public Player(Texture2D texture, Vector2 position)
    {
       Actions = new PlayerActionController(this);
       _sprite = new Sprite(texture);
        _position = position;
    }

    public void Draw(SpriteBatch spriteBatch)
{
        if (_animationManager != null)
            {
                _sprite.Texture = _animationManager.Texture;
                _sprite.SourceRectangle = _animationManager.SourceRectangle;
            }
            _sprite.Position = Position;
            _sprite.Scale = Scale;
            _sprite.Draw(spriteBatch);
        }
    protected virtual void Move(GameTime gameTime)
    {
        _velocity = Vector2.Zero;
        var keyboard = Keyboard.GetState();

        if (keyboard.IsKeyDown(Input.Right)) _velocity.X += 1;
        if (keyboard.IsKeyDown(Input.Left))  _velocity.X -= 1;
        if (keyboard.IsKeyDown(Input.Up))    _velocity.Y -= 1;
        if (keyboard.IsKeyDown(Input.Down))  _velocity.Y += 1;

        if (_velocity != Vector2.Zero)
            _velocity.Normalize();

        _velocity *= Speed;

        float seconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        MoveBy(_velocity * seconds);
    }

    public void MoveBy(Vector2 movement)
    {
        if (InputLocked || movement == Vector2.Zero) return;
        Rectangle previousBounds = Bounds;
        // Test tentative positions without sending trigger events for blocked steps.
        int steps = Math.Max(1, (int)MathF.Ceiling(
            MathF.Max(MathF.Abs(movement.X), MathF.Abs(movement.Y))));
        Vector2 step = movement / steps;
        for (int i = 0; i < steps; i++)
        {
            Vector2 previous = _position;
            _position += new Vector2(step.X, 0);
            if (IsMovementBlocked?.Invoke(Bounds) == true) _position = previous;
            previous = _position;
            _position += new Vector2(0, step.Y);
            if (IsMovementBlocked?.Invoke(Bounds) == true) _position = previous;
        }
        // Report the final bounds before this frame's interaction input is handled.
        if (Bounds != previousBounds) PositionChanged?.Invoke();
    }

    protected virtual void SetAnimations()
    {
        if(_velocity.X > 0)
            {
            _idleAnimation = "Right_Idle";
            Facing = "Right";
            _animationManager.Play(_animations["WalkRight"]);
            }
        else if(_velocity.X < 0)
            {
            _idleAnimation = "Left_Idle";
            Facing = "Left";
            _animationManager.Play(_animations["WalkLeft"]);
            }
        else if(_velocity.Y > 0)
            {
            _idleAnimation = "Front_Idle";
            Facing = "Front";
            _animationManager.Play(_animations["WalkDown"]);
            }
        else if(_velocity.Y < 0)
            {
            _idleAnimation = "Back_Idle";
            Facing = "Back";
            _animationManager.Play(_animations["WalkUp"]);
            }
        else
            {
            _animationManager.Play(_animations[_idleAnimation]);
            }
    }

    public Player(Dictionary<string, Animation> animations)
    {
        Actions = new PlayerActionController(this);
        _animations = animations;
        _animationManager = new AnimationManager(_animations.First().Value);
        _sprite = new Sprite(_animationManager.Texture);
    }

    public void FaceTowards(Vector2 target)
    {
        Vector2 direction = target - GroundPosition;
        Facing = MathF.Abs(direction.X) >= MathF.Abs(direction.Y)
            ? (direction.X >= 0 ? "Right" : "Left")
            : (direction.Y >= 0 ? "Front" : "Back");
        _idleAnimation = Facing switch
        {
            "Left" => "Left_Idle",
            "Front" => "Front_Idle",
            "Back" => "Back_Idle",
            _ => "Right_Idle"
        };
    }

    /// <summary>Missing action artwork falls back to the current facing's idle pose.</summary>
    public bool PlayActionAnimation(string animationName)
    {
        _velocity = Vector2.Zero;
        if (_animationManager == null) return false;
        if (animationName == null || !_animations.TryGetValue(animationName, out Animation animation))
        {
            RestoreIdleAnimation();
            return false;
        }
        _animationManager.Play(animation, restart: true);
        return true;
    }

    public void RestoreIdleAnimation()
    {
        _velocity = Vector2.Zero;
        if (_animationManager != null && _animations.TryGetValue(_idleAnimation, out Animation idle))
            _animationManager.Play(idle);
    }

    public void Update(GameTime gameTime)
    {

        if (InputLocked) return;
        if (!Actions.BlocksMovement){
            Move(gameTime);
        }
        if (_animationManager != null)
        {
            if(ActionState == PlayerActionState.Free){
            SetAnimations();
            }
            _animationManager.Update(gameTime);
        }

    }



}
````

### First_game/UI/UIManager.cs

````csharp
using System;
using MonoGameLibrary.Input;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace First_game.UI;

public sealed class UIManager
{
    private readonly Dictionary<MenuType, IMenuPanel> panels = new();
    private readonly Texture2D pixel;
    private readonly InputBindings bindings;

    public MenuType ActiveMenu { get; private set; }
    public bool BlocksWorldInput => ActiveMenu != MenuType.None;
    public bool PausesWorld => ActiveMenu == MenuType.Pause;
    public bool ExitRequested { get; private set; }
    public bool ConsumedInputThisFrame { get; private set; }

    public UIManager(Texture2D pixel, InputBindings bindings = null)
    {
        this.pixel = pixel;
        this.bindings = bindings ?? new InputBindings();
    }

    public void Register(MenuType menu, IMenuPanel panel)
    {
        if (menu == MenuType.None)
            throw new ArgumentException("None represents a closed menu.", nameof(menu));
        panels.Add(menu, panel ?? throw new ArgumentNullException(nameof(panel)));
    }

    public void Open(MenuType menu)
    {
        if (!panels.ContainsKey(menu))
            throw new ArgumentException("Register the menu before opening it.", nameof(menu));
        if (ActiveMenu != menu)
            Close();
        ActiveMenu = menu;
    }

    public void Close()
    {
        if (panels.TryGetValue(ActiveMenu, out IMenuPanel panel))
            panel.OnClosed();
        ActiveMenu = MenuType.None;
    }

    public void Toggle(MenuType menu)
    {
        if (ActiveMenu == menu)
            Close();
        else
            Open(menu);
    }

    public void Update(GameTime gameTime, UIInput input, Viewport viewport)
    {
        ExitRequested = false;
        ConsumedInputThisFrame = BlocksWorldInput;

        if (input.Pressed(Keys.Escape))
        {
            if (BlocksWorldInput)
                Close();
            else
                ExitRequested = true; // Preserve Escape-to-exit in the world.
        }
        else if (input.Pressed(Keys.P))
            Toggle(MenuType.Pause);
        else if (!PausesWorld && input.Pressed(bindings.Inventory))
            Toggle(MenuType.Inventory);
        else if (!PausesWorld && input.Pressed(Keys.C))
            Toggle(MenuType.Crafting);

        ConsumedInputThisFrame |= BlocksWorldInput || ExitRequested;
        if (panels.TryGetValue(ActiveMenu, out IMenuPanel panel))
            panel.Update(gameTime, input, viewport);
    }

    // Game1 owns Begin/End; every menu draws in the same screen-space batch.
    public void Draw(SpriteBatch spriteBatch, Viewport viewport)
    {
        if (!panels.TryGetValue(ActiveMenu, out IMenuPanel panel))
            return;

        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height),
            Color.Black * 0.55f);
        panel.Draw(spriteBatch, viewport);
    }
}
````

### First_game/First_game.csproj

````xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <RollForward>Major</RollForward>
    <PublishReadyToRun>false</PublishReadyToRun>
    <TieredCompilation>false</TieredCompilation>
  </PropertyGroup>
  <PropertyGroup>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <ApplicationIcon>Icon.ico</ApplicationIcon>
  </PropertyGroup>
  <ItemGroup>
    <None Remove="Icon.ico" />
    <None Remove="Icon.bmp" />
  </ItemGroup>
  <ItemGroup>
    <EmbeddedResource Include="Icon.ico">
      <LogicalName>Icon.ico</LogicalName>
    </EmbeddedResource>
    <EmbeddedResource Include="Icon.bmp">
      <LogicalName>Icon.bmp</LogicalName>
    </EmbeddedResource>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.*" />
    <PackageReference Include="MonoGame.Content.Builder.Task" Version="3.8.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\MonoGameLibrary\MonoGameLibrary.csproj" />
  </ItemGroup>
  <ItemGroup>
    <None Update="Content/doors.json" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
````

### First_game/Game1.cs

````csharp
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary;
using First_game.Entities;
using MonoGameLibrary.Graphics;
using System;
using System.Collections.Generic;
using First_game.World;
using PlayerInventory = First_game.Inventory.Inventory;
using First_game.Inventory;
using First_game.Input;
using First_game.UI;
using MonoGameLibrary.Input;
using First_game.Actions;
using First_game.Fishing;
using First_game.Doors;
using System.IO;
using System.Diagnostics;

namespace First_game;


public class Game1 : Core
{
    private Sprite background;
    private Player cat;
    private PlayerInventory inventory;
    private WorldPickupSystem pickupSystem;
    private SpriteFont hudFont;
    private Camera2D camera;
    private readonly WorldRenderer worldRenderer = new WorldRenderer();
    private readonly List<Sprite> trees = new List<Sprite>();
    private readonly List<Sprite> pines = new List<Sprite>();
    private readonly Random random = new Random();
    private Sprite House;
    private Sprite Tent;
    private PlacedObject Lake;
    private MouseInteractionController mouseInteractions;
    private KeyboardState _previousKeyboard;
    private GridPlacer gridPlacer;
    private Texture2D gridPixel;
    private Func<string, Texture2D> itemTextureLoader;
    private UIManager uiManager;
    private Hotbar hotbar;
    private HotbarPanel hotbarPanel;
    private readonly MouseInput mouseInput = new MouseInput();
    private TimeSpan worldElapsed;
    private FishingController fishing;
    private FishingOverlay fishingOverlay;
    private DoorSystem doors;
    private DoorOverlay doorOverlay;
    private readonly GameTime worldTime = new GameTime();

    public Game1() : base("Game1" , 1280 , 720, false)
    {

    }
    private readonly WorldGrid worldGrid = new WorldGrid(
    cellSize: 100,
    origin: new Vector2(-3000, -3000),
    columnCount: 60,
    rowCount: 60);

    protected override void Initialize()
    {
        var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;

        Graphics.PreferredBackBufferWidth = display.Width;
        Graphics.PreferredBackBufferHeight = display.Height;
        Graphics.IsFullScreen = true;
        Graphics.ApplyChanges();

        base.Initialize();
    }
    private void DrawGrid()
{
    int left = (int)worldGrid.Origin.X;
    int top = (int)worldGrid.Origin.Y;
    int width = worldGrid.Columns * worldGrid.CellSize;
    int height = worldGrid.Rows * worldGrid.CellSize;

    Color lineColor = Color.White * 0.25f;

    for (int column = 0; column <= worldGrid.Columns; column++)
    {
        int x = left + column * worldGrid.CellSize;

        SpriteBatch.Draw(
            gridPixel,
            new Rectangle(x, top, 1, height),
            lineColor);
    }

    for (int row = 0; row <= worldGrid.Rows; row++)
    {
        int y = top + row * worldGrid.CellSize;

        SpriteBatch.Draw(
            gridPixel,
            new Rectangle(left, y, width, 1),
            lineColor);
    }
}



    protected override void LoadContent()
    {
        gridPixel = new Texture2D(GraphicsDevice, 1, 1);
        gridPixel.SetData(new[] { Color.White });
        gridPlacer = new GridPlacer(worldGrid);
        itemTextureLoader = assetName => Content.Load<Texture2D>(assetName);
        var catTexture = Content.Load<Texture2D>("Images/startercat");
        var mapTexture = Content.Load<Texture2D>("Images/grass");
        ItemDefinitionRegistry itemDefinitions = SampleItemCatalog.CreateDefinitions();
        ItemCategoryBehaviorRegistry itemBehaviors = SampleItemCatalog.CreateBehaviors();
        itemDefinitions.ResolveUseEffects(UseEffectRegistry.CreateBuiltIns(), itemBehaviors);
        inventory = new PlayerInventory(itemDefinitions, itemBehaviors, PlayerInventory.PlayerCapacity);
        hotbar = new Hotbar(inventory);
        var spawnRules = new WorldSpawnRuleRegistry(itemDefinitions);
        WorldSpawnCatalog.RegisterRules(spawnRules);
        pickupSystem = new WorldPickupSystem(
            worldGrid,
            itemDefinitions,
            spawnRules,
            itemTextureLoader,
            random);
        hudFont = Content.Load<SpriteFont>("Fonts/UIFont");
        var HouseTexture = Content.Load<Texture2D>("Images/House");
        var TreeTexture = Content.Load<Texture2D>("Images/Tree");
        var PineTexture = Content.Load<Texture2D>("Images/Pine");
        var TentTexture = Content.Load<Texture2D>("Images/Tent");
        var LakeTexture = Content.Load<Texture2D>("Images/Lake");
        var inventoryBackground = Content.Load<Texture2D>("Images/Inventory");
        var hotbarBackground = Content.Load<Texture2D>("Images/Hudbar");
        var selectionHighlight = Content.Load<Texture2D>("Images/Highlight");
        var itemSlots = new ItemSlotRenderer(itemDefinitions, itemTextureLoader, hudFont, selectionHighlight);
        var inventoryDrag = new InventoryDragController();
        hotbarPanel = new HotbarPanel(inventory, hotbar, hotbarBackground, itemSlots, inventoryDrag);


        var walkTextureRight = Content.Load<Texture2D>("Images/Right_walk");
        var walkTextureLeft = Content.Load<Texture2D>("Images/Left_walk");

        var animations = new Dictionary<string, Animation>
        {
            { "WalkRight", new Animation(walkTextureRight, 9)  },
            { "WalkLeft",  new Animation(walkTextureLeft, 9)  },
            { "WalkDown",  new Animation(walkTextureRight, 9)  },
            { "WalkUp",    new Animation(walkTextureLeft, 9)  },

            { "Right_Idle", new Animation(
                Content.Load<Texture2D>("Images/Right_Idle"), 2) { FrameDuration = 0.7f }},
            { "Left_Idle", new Animation(
                Content.Load<Texture2D>("Images/Left_Idle"), 2) { FrameDuration = 0.7f }},
            { "Front_Idle", new Animation(
                Content.Load<Texture2D>("Images/Front_Idle"), 2) { FrameDuration = 0.7f }},
            { "Back_Idle", new Animation(
                Content.Load<Texture2D>("Images/Back_Idle"), 2) { FrameDuration = 0.7f }},
        };

        cat = new Player(animations);
        uiManager = new UIManager(gridPixel, cat.Input);
        uiManager.Register(MenuType.Inventory, new InventoryPanel(
            inventory, hotbar, inventoryBackground, itemSlots, inventoryDrag));
        uiManager.Register(MenuType.Crafting, new CraftingPanel(hudFont));
        uiManager.Register(MenuType.Pause, new PauseMenu(hudFont));
        cat.Position = new Vector2(100, 1200);
        cat.Scale = 0.5f;
        cat.Speed = 300f;
        cat.CollisionSize = new Vector2(270, 64);
        cat.CollisionOffset = new Vector2(0, 331);

        //placing custom footprint for lake
        Point[] lakeFootprint = GridPlacer.CreateFootprint(
                "..XXXXXX....",
                ".XXXXXXXXX..",
                ".XXXXXXXXXX.",
                "XXXXXXXXXXXX",
                "XXXXXXXXXXXX",
                ".XXXXXXXXXXX",
                ".XXX....XXX."
        );

        if (!gridPlacer.TryPlaceFootprint(
        LakeTexture,
        new Vector2(-2000, 1500),
        lakeFootprint,
        anchorInCells: new Vector2(6f, 7f),
        scale: 0.4f,
        out Lake,
        groundOffsetY: LakeTexture.Height / 2f - 550f,
        groundOffsetX: -100f))
        {
        throw new InvalidOperationException(
            "The lake footprint is occupied or outside the grid.");
        }

        if (!gridPlacer.TryPlaceBuilding(
            HouseTexture,
            new Vector2(400, 150),
            widthInCells: 11,
            heightInCells: 4,
            scale: 0.3f,
            out House,
            groundOffsetY: HouseTexture.Height / 2f - 200f,
            groundOffsetX: 0f))
            {
            throw new InvalidOperationException(
                "The house footprint is occupied or outside the grid.");

        }
        if (!gridPlacer.TryPlaceBuilding(
            TentTexture,
            new Vector2(-500, 2000),
            widthInCells: 5,
            heightInCells: 3,
            scale: 0.15f,
            out Tent,
            groundOffsetY: TentTexture.Height / 2f - 667f,
            groundOffsetX: -200f))
            {
            throw new InvalidOperationException(
                "The house footprint is occupied or outside the grid.");

        }
        doors = DoorConfiguration.Preload(
            Path.Combine(AppContext.BaseDirectory, "Content", "doors.json"),
            cat, worldGrid, LogDoorError);
        doorOverlay = new DoorOverlay(doors, gridPixel, hudFont);
        doors.AreaChanged += OnAreaChanged;

        pickupSystem.RegisterNodes(WorldSpawnCatalog.CreateNodes());

        for (int treeIndex = 0; treeIndex < 10; treeIndex++)
        {
            Vector2 TreePosition = new Vector2(
                random.Next(-3000, 2000),
                random.Next(-3000, 2000));
            if (PlantCellOverlapsPlayer(TreePosition))
                continue;
            if (gridPlacer.TryPlaceSprite(
                TreeTexture,
                TreePosition,
                CellType.Plant,
                0.2f,
                out Sprite tree,
                groundOffsetY: TreeTexture.Height / 2f - 100f,
                groundOffsetX: -447.5f))
            {
                trees.Add(tree);
            }
        }

        for (int pineIndex = 0; pineIndex < 10; pineIndex++)
        {
            Vector2 PinePosition = new Vector2(
                random.Next(-3000, 2000),
                random.Next(-3000, 2000));
            if (PlantCellOverlapsPlayer(PinePosition))
                continue;
            if (gridPlacer.TryPlaceSprite(
                PineTexture,
                PinePosition,
                CellType.Plant,
                0.2f,
                out Sprite pine,
                groundOffsetY: PineTexture.Height / 2f -100f,
                groundOffsetX: -60f))
            {
                pines.Add(pine);
            }
        }
        camera = new Camera2D(cat.Position);
        mouseInteractions = new MouseInteractionController(camera, pickupSystem, inventory, mouseInput);
        fishing = new FishingController(cat, inventory);
        fishing.Register(new FishingSpot(worldGrid, Lake.GetOccupiedCells(), "(O)fish", new FishingSettings()));
        fishingOverlay = new FishingOverlay(gridPixel);
        mouseInteractions.TryInteract = fishing.TryInteract;

        background = new Sprite(mapTexture);
        background.Scale = 2.0f;
        background.Position = new Vector2(620, 360);
    }

    protected override void OnDeactivated(object sender, EventArgs args)
    {
        // A release outside the game window must not leave a pending drag.
        if (uiManager?.ActiveMenu == MenuType.Inventory)
            uiManager.Close();
        base.OnDeactivated(sender, args);
    }

    protected override void Update(GameTime gameTime)
    {
        mouseInput.Update();
        var currentKeyboard = Keyboard.GetState();
        var uiInput = new UIInput(currentKeyboard, _previousKeyboard, mouseInput);
        bool wasTransitioning = doors.IsTransitioning;

        // Continue sampling input during fades, but consume it without buffering.
        if (!wasTransitioning && IsActive)
            uiManager.Update(gameTime, uiInput, GraphicsDevice.Viewport);

        bool blocked = wasTransitioning || !IsActive || uiManager.ConsumedInputThisFrame;
        bool mouseOverHotbar = false;
        if (!blocked)
            mouseOverHotbar = hotbarPanel.Update(uiInput, GraphicsDevice.Viewport);

        if ((!wasTransitioning && uiManager.ExitRequested)
            || GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
            Exit();

        // The inactive exterior retains its objects and its exact respawn clock.
        bool advanceExterior = doors.ActiveArea.IsExterior
            && !wasTransitioning && !uiManager.PausesWorld;
        if (advanceExterior)
            worldElapsed += gameTime.ElapsedGameTime;
        worldTime.TotalGameTime = worldElapsed;
        worldTime.ElapsedGameTime = advanceExterior ? gameTime.ElapsedGameTime : TimeSpan.Zero;
        if (advanceExterior)
            pickupSystem.Update(worldTime);

        doors.SetInteractionEnabled(!blocked && !cat.Actions.IsBusy);
        if (!blocked)
        {
            // Resolve movement/trigger exits before E, including an exit on this frame.
            cat.Update(gameTime);
            cat.Actions.Update(gameTime, ActionInput.FromKeyboard(currentKeyboard, _previousKeyboard));
            if (doors.ActiveArea.IsExterior)
                fishing.Update(gameTime);
        }
        doors.SetInteractionEnabled(!blocked && !cat.Actions.IsBusy);
        doors.Update((float)gameTime.ElapsedGameTime.TotalSeconds, uiInput);

        if (doors.ActiveArea.IsExterior)
            mouseInteractions.Update(worldTime, GraphicsDevice.Viewport, cat.GroundPosition,
                blocked || doors.IsTransitioning || mouseOverHotbar || cat.Actions.IsBusy);

        // A right-click may have started an action after the door update.
        doors.SetInteractionEnabled(!blocked && !doors.IsTransitioning && !cat.Actions.IsBusy);
        if (!uiManager.PausesWorld && !wasTransitioning && !doors.IsTransitioning)
        {
            camera.UpdateTarget(cat.Position);
            camera.Update(gameTime);
        }

        _previousKeyboard = currentKeyboard;
        base.Update(gameTime);
    }

    private void OnAreaChanged()
    {
        // Teleports must not interpolate the camera across unrelated areas.
        camera.Position = cat.Position;
        camera.TargetPosition = cat.Position;
    }

    private static void LogDoorError(string message)
    {
        Trace.TraceError(message);
        Console.Error.WriteLine(message);
    }

    protected override void UnloadContent()
    {
        if (doors != null)
        {
            doors.AreaChanged -= OnAreaChanged;
            doors.Dispose();
        }
        gridPixel?.Dispose();
        base.UnloadContent();
    }

    private bool PlantCellOverlapsPlayer(Vector2 position)
    {
        Vector2 topLeft = worldGrid.CellToWorld(worldGrid.WorldToCell(position));
        return cat.Bounds.Intersects(new Rectangle(
            (int)topLeft.X, (int)topLeft.Y, worldGrid.CellSize, worldGrid.CellSize));
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(doors.ActiveArea.IsExterior ? Color.White : new Color(31, 27, 26));
        Matrix transformMatrix = camera.GetTransform(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        SpriteBatch.Begin(transformMatrix: transformMatrix);

        if (doors.ActiveArea.IsExterior)
        {
            background.Draw(SpriteBatch);
            Lake?.Sprite.Draw(SpriteBatch);
            worldRenderer.Submit(House.SortY, House.Draw);
            worldRenderer.Submit(Tent.SortY, Tent.Draw);
            foreach (Sprite tree in trees)
                worldRenderer.Submit(tree.SortY, tree.Draw);
            foreach (Sprite pine in pines)
                worldRenderer.Submit(pine.SortY, pine.Draw);
            pickupSystem.SubmitDraw(worldRenderer);
            worldRenderer.Submit(cat.Bounds.Bottom, cat.Draw);
            worldRenderer.Draw(SpriteBatch);
            fishingOverlay.DrawWorld(SpriteBatch, cat, fishing.Active);
            DrawGrid();
        }
        else
        {
            doors.ActiveArea.Draw(SpriteBatch, gridPixel);
            cat.Draw(SpriteBatch);
        }

        SpriteBatch.End();
        SpriteBatch.Begin();
        hotbarPanel.Draw(SpriteBatch, GraphicsDevice.Viewport);
        uiManager.Draw(SpriteBatch, GraphicsDevice.Viewport);
        doorOverlay.Draw(SpriteBatch, GraphicsDevice.Viewport);
        SpriteBatch.End();
        base.Draw(gameTime);
    }
}
````

### Tests/GameplayChecks/DoorChecks.cs

````csharp
using System;
using System.IO;
using First_game.Doors;
using First_game.Entities;
using First_game.UI;
using First_game.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

static class DoorChecks
{
    private static readonly UIInput Press = new(new KeyboardState(Keys.E), default, null);
    private static readonly UIInput Held = new(new KeyboardState(Keys.E), new KeyboardState(Keys.E), null);
    private static readonly UIInput Release = new(default, new KeyboardState(Keys.E), null);

    public static void Run(Action<bool, string> check)
    {
        Player player = new((Texture2D)null, Vector2.Zero)
        {
            Scale = 1, CollisionSize = new Vector2(20, 20)
        };
        var grid = new WorldGrid(100, new Vector2(-1000), 30, 30);
        var exterior = new ResidentArea("exterior", grid);
        var interior = new ResidentArea("house", new Rectangle(0, 0, 400, 400), 20);
        var enter = new Door("enter", new Rectangle(90, 90, 60, 60), interior,
            new AreaSpawn(new Vector2(200, 200), new Vector2(0, -1)), false);
        var exit = new Door("exit", new Rectangle(170, 300, 60, 60), exterior,
            new AreaSpawn(new Vector2(120, 210), new Vector2(0, 1)), true);
        exterior.SetDoors(new[] { enter });
        interior.SetDoors(new[] { exit });
        int errors = 0;
        using var system = new DoorSystem(player, new[] { exterior, interior }, exterior, .25f, _ => errors++);
        int promptChanges = 0;
        int swaps = 0;
        system.PromptChanged += _ => promptChanges++;
        system.AreaChanged += () => swaps++;

        player.Position = new Vector2(120, 120);
        check(system.Prompt == "Press E to enter", "door overlap shows enter prompt");
        int changes = promptChanges;
        for (int i = 0; i < 10; i++) system.Update(.016f, default);
        check(promptChanges == changes, "stationary updates do not rewrite prompt");
        player.MoveBy(new Vector2(100, 0));
        system.Update(.016f, Press);
        check(system.Prompt == null && !system.IsTransitioning,
            "leaving trigger on the E frame prevents entry");

        player.Position = new Vector2(120, 120);
        system.Update(.016f, Held);
        check(!system.IsTransitioning, "holding E when entering range requires a fresh press");
        system.Update(.016f, Press);
        check(system.IsTransitioning && player.InputLocked && system.Prompt == null,
            "entry locks input and hides prompt");
        Vector2 lockedPosition = player.Position;
        player.MoveBy(new Vector2(100, 0));
        check(player.Position == lockedPosition, "transition also blocks direct movement");
        system.Update(.125f, Press);
        check(system.ActiveArea == exterior && system.FadeOpacity == .5f,
            "fade advances without an early area swap");
        system.Update(.125f, Press);
        check(system.ActiveArea == interior && player.GroundPosition == new Vector2(200, 200)
            && player.Facing == "Back" && swaps == 1 && system.FadeOpacity == 1,
            "black frame commits one swap at the configured spawn");
        system.Update(.25f, Held);
        check(!system.IsTransitioning && !player.InputLocked && system.Prompt == null,
            "fade-in releases control without re-triggering");
        check(interior.BlocksMovement(new Rectangle(0, 0, 20, 20))
            && !interior.BlocksMovement(player.Bounds), "room walls contain the player");

        object exteriorState = new object();
        grid.Occupy(new Point(0, 0), CellType.Pickup, exteriorState, false);
        var actions = player.Actions;
        player.Position = new Vector2(200, 330);
        check(system.Prompt == "Press E to exit", "same component shows exit prompt");
        system.Update(.016f, Release);
        system.Update(.016f, Press);
        system.Update(1f, Press);
        check(system.ActiveArea == exterior && system.IsTransitioning && system.FadeOpacity == 1,
            "long frames retain a black frame and do not skip fade-in");
        system.Update(1f, Press);
        check(player.GroundPosition == new Vector2(120, 210) && player.Facing == "Front"
            && system.Prompt == null && !player.InputLocked, "return is outside trigger and facing away");
        check(ReferenceEquals(grid.GetCell(new Point(0, 0)).Occupant, exteriorState)
            && ReferenceEquals(actions, player.Actions), "round trip preserves world and player instances");

        player.Position = new Vector2(120, 120);
        interior.IsReady = false;
        system.Update(.016f, Press);
        system.Update(.25f, default);
        system.Update(.25f, default);
        check(errors == 1 && system.LastError != null && system.ActiveArea == exterior
            && !player.InputLocked && !system.IsTransitioning,
            "unavailable target logs and fades back with controls restored");
        interior.IsReady = true;

        // A collision added after preload must also fail before committing the swap.
        player.Position = new Vector2(120, 120);
        system.Update(.016f, Press);
        system.Update(.25f, default);
        system.Update(.25f, default);
        player.Position = new Vector2(200, 330);
        grid.Occupy(grid.WorldToCell(new Vector2(120, 210)), CellType.Building);
        system.Update(.016f, Press);
        system.Update(.25f, default);
        system.Update(.25f, default);
        check(errors == 2 && system.ActiveArea == interior && !player.InputLocked,
            "blocked return spawn keeps the player safely inside");
        grid.ClearCell(grid.WorldToCell(new Vector2(120, 210)));
        system.Update(.016f, Press);
        system.Update(.25f, default);
        system.Update(.25f, default);

        system.SetInteractionEnabled(false);
        player.Position = new Vector2(120, 120);
        system.Update(.016f, Press);
        check(system.Prompt == null && !system.IsTransitioning, "menus and actions suppress interaction");
        system.SetInteractionEnabled(true);
        player.Input.Interact = Keys.F;
        check(system.Prompt == "Press F to enter", "binding event refreshes cached prompt");
        system.Update(.016f, Press);
        check(!system.IsTransitioning, "old interaction binding no longer fires");
        system.Update(.016f, new UIInput(new KeyboardState(Keys.F), default, null));
        check(system.IsTransitioning, "configured interaction key starts transition");
        system.Update(.25f, default);
        system.Update(.25f, default);
        player.Input.Interact = Keys.E;

        CheckSpawnOverlap(check);
        CheckConfiguration(check);

        // Warm up JIT and exercise moving overlaps plus complete transitions.
        player.Position = new Vector2(200, 330);
        for (int i = 0; i < 100; i++) Cycle(player, system);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) Cycle(player, system);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        check(allocated == 0, "door movement callbacks and transitions allocate zero bytes after initialization");
    }

    private static void Cycle(Player player, DoorSystem system)
    {
        player.Position = system.ActiveArea.IsExterior ? new Vector2(120, 120) : new Vector2(200, 330);
        system.Update(.016f, Press);
        system.Update(.25f, Held);
        system.Update(.25f, Held);
        system.Update(.016f, Release);
    }

    private static void CheckSpawnOverlap(Action<bool, string> check)
    {
        Player player = new((Texture2D)null, new Vector2(110, 110))
        {
            Scale = 1, CollisionSize = new Vector2(20)
        };
        var a = new ResidentArea("a", new Rectangle(0, 0, 400, 400), 20);
        var b = new ResidentArea("b", new Rectangle(0, 0, 400, 400), 20);
        a.SetDoors(new[] { new Door("a-b", new Rectangle(90, 90, 60, 60), b,
            new AreaSpawn(new Vector2(110, 120), Vector2.UnitY), false) });
        b.SetDoors(new[] { new Door("b-a", new Rectangle(90, 90, 60, 60), a,
            new AreaSpawn(new Vector2(110, 120), Vector2.UnitY), true) });
        using var system = new DoorSystem(player, new[] { a, b }, a, .1f, null);
        system.Update(.01f, Press);
        check(!system.IsTransitioning && system.Prompt == null, "initial spawn inside trigger is disarmed");
        player.Position = new Vector2(250, 250);
        player.Position = new Vector2(110, 110);
        system.Update(.01f, Press);
        system.Update(.1f, default);
        system.Update(.1f, default);
        system.Update(.01f, Press);
        check(system.ActiveArea == b && !system.IsTransitioning && system.Prompt == null,
            "destination spawn inside trigger cannot bounce back even with a fresh press");
        player.Position = new Vector2(250, 250);
        player.Position = new Vector2(110, 110);
        check(system.Prompt == "Press E to exit", "leaving and re-entering rearms a spawn-overlapped door");
    }

    private static void CheckConfiguration(Action<bool, string> check)
    {
        Player player = new((Texture2D)null, Vector2.Zero)
        {
            Scale = .5f, CollisionSize = new Vector2(270, 64),
            CollisionOffset = new Vector2(0, 331)
        };
        var grid = new WorldGrid(100, new Vector2(-3000), 60, 60);
        grid.OccupyArea(grid.WorldToCell(new Vector2(400, 150)), 11, 4, CellType.Building);
        int errors = 0;
        string path = Path.Combine(AppContext.BaseDirectory, "Content", "doors.json");
        using (var system = DoorConfiguration.Preload(path, player, grid, _ => errors++))
        {
            check(errors == 0 && system.ActiveArea.Triggers.Doors.Length == 1,
                "shipped configuration preloads beside the real house footprint");
            check(grid.IsOccupied(grid.WorldToCell(new Vector2(950, 675)))
                && !grid.IntersectsBlockedCell(player.BoundsAt(player.PositionForGround(new Vector2(950, 675)))),
                "return approach is reserved but walkable");
            player.Position = player.PositionForGround(new Vector2(950, 540));
            system.Update(.016f, Press);
            system.Update(.25f, default);
            system.Update(.25f, default);
            check(!system.ActiveArea.IsExterior && player.GroundPosition == new Vector2(500, 480),
                "real player bounds enter the configured room exactly");
            player.Position = player.PositionForGround(new Vector2(500, 630));
            system.Update(.016f, Release);
            system.Update(.016f, Press);
            system.Update(.25f, default);
            system.Update(.25f, default);
            check(system.ActiveArea.IsExterior && player.GroundPosition == new Vector2(950, 675)
                && system.Prompt == null, "shipped configuration completes the return trip");
        }
        using var failed = DoorConfiguration.Preload(path + ".missing", player, grid, _ => errors++);
        check(errors == 1 && failed.ActiveArea.IsExterior && !failed.IsTransitioning
            && !player.InputLocked, "missing interior configuration logs and preserves exterior control");
    }
}
````

### Tests/GameplayChecks/Program.cs

````csharp
using System;
using First_game.Actions;
using First_game.Entities;
using First_game.Fishing;
using First_game.Inventory;
using First_game.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

static class Checks
{
    static int passed;
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAILED: " + name);
        passed++;
        Console.WriteLine("PASS: " + name);
    }
    static GameTime Time(float seconds) => new(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));
    static Player NewPlayer() => new((Texture2D)null, Vector2.Zero) { Scale = 1 };
    static Inventory NewInventory() => new(SampleItemCatalog.CreateDefinitions(), SampleItemCatalog.CreateBehaviors());
    static FishingSpot Spot(FishingSettings settings = null) => new(
        new WorldGrid(100, Vector2.Zero, 10, 10), new[] { new Point(1, 0) }, "(O)fish", settings ?? new());
    static readonly Vector2 Water = new(150, 50);

    static void Main()
    {
        Player player = NewPlayer();
        int hits = 0;
        var swing = new TimedEffectAction("Chop", null, Vector2.Zero, 100, 1, .5f, () => true, () => hits++);
        Check(player.Actions.TryStart(swing, out _), "start action without artwork");
        Check(player.Actions.BlocksMovement, "action locks movement");
        Check(!player.Actions.TryStart(new ConversationAction(new[] { "Hello" }, Vector2.Zero), out _), "reject overlapping actions");
        player.Actions.Update(Time(.9f), default);
        player.Actions.Update(Time(.9f), default);
        Check(hits == 1 && !player.Actions.IsBusy, "long frame crosses impact once and releases player");
        Check(!player.Actions.TryStart(swing, out _), "reject reusing completed instance");

        var cancelled = new TimedEffectAction("Mine", null, Vector2.Zero, 100, 1, .5f, () => true, () => hits++);
        player.Actions.TryStart(cancelled, out _);
        player.Actions.Update(Time(.1f), new(false, false, true));
        Check(hits == 1 && !player.Actions.IsBusy, "cancel before impact produces no reward");
        bool valid = true;
        player.Actions.TryStart(new TimedEffectAction("Attack", null, Vector2.Zero, 100, 1, .5f, () => valid, () => hits++), out _);
        valid = false;
        player.Actions.Update(Time(1), default);
        Check(hits == 1, "target validity is checked again at impact");

        var talk = new ConversationAction(new[] { "One", "Two" }, Vector2.Zero);
        player.Actions.TryStart(talk, out _);
        player.Actions.Update(Time(.1f), new(false, true, false));
        Check(talk.CurrentLine == "One", "holding confirm does not skip dialogue");
        player.Actions.Update(Time(.1f), new(true, true, false));
        Check(talk.CurrentLine == "Two", "fresh confirm advances dialogue");
        player.Actions.Update(Time(.1f), new(true, true, false));
        Check(!player.Actions.IsBusy, "dialogue completion releases player");

        var inventory = NewInventory();
        var fishing = new FishingController(player, inventory);
        fishing.Register(Spot());
        Check(!fishing.TryInteract(Vector2.Zero), "land falls through to other interactions");
        player.Position = new Vector2(900, 900);
        Check(fishing.TryInteract(Water) && !player.Actions.IsBusy, "distant water consumed without starting");
        player.Position = Vector2.Zero;
        var scaffold = new FishingAction(Spot(), Water, inventory);
        player.Actions.TryStart(scaffold, out _);
        player.Actions.Update(Time(1), default);
        Check(scaffold.State == FishingState.Catching, "cast hands off to custom catch stage");
        player.Actions.Update(Time(100), new(true, true, false));
        Check(player.Actions.IsBusy && inventory.GetItemCount("(O)fish") == 0,
            "scaffold waits without built-in rules or automatic reward");
        player.Actions.Cancel();
        Check(scaffold.State == FishingState.Cancelled, "scaffold cancels");

        var custom = new CustomFishing(Spot(), Water, inventory);
        player.Actions.TryStart(custom, out _);
        player.Actions.Update(Time(1), default);
        custom.Resolve(true);
        custom.Resolve(true);
        player.Actions.Update(Time(.01f), default);
        Check(!player.Actions.IsBusy && inventory.GetItemCount("(O)fish") == 1,
            "custom success awards once and releases player");

        var failure = new CustomFishing(Spot(), Water, inventory);
        player.Actions.TryStart(failure, out _);
        player.Actions.Update(Time(1), default);
        failure.Resolve(false);
        player.Actions.Update(Time(.01f), default);
        Check(failure.State == FishingState.Failed && inventory.GetItemCount("(O)fish") == 1,
            "custom failure awards nothing");

        var full = NewInventory();
        full.AddItem("(O)wood", 12 * 99);
        Check(!player.Actions.TryStart(new FishingAction(Spot(), Water, full), out _), "full inventory rejects cast");
        full.RemoveItem("(O)wood", 99);
        full.AddItem("(O)fish", 98);
        Check(full.HasSpaceFor("(O)fish"), "fish fits existing stack without empty slot");

        var lateFull = NewInventory();
        var lateCatch = new CustomFishing(Spot(), Water, lateFull);
        player.Actions.TryStart(lateCatch, out _);
        player.Actions.Update(Time(1), default);
        lateFull.AddItem("(O)wood", 12 * 99);
        lateCatch.Resolve(true);
        player.Actions.Update(Time(.01f), default);
        Check(lateCatch.State == FishingState.Failed && lateFull.GetItemCount("(O)fish") == 0,
            "inventory filling during attempt handled honestly");

        for (int stage = 0; stage < 2; stage++)
        {
            var cancelFish = new CustomFishing(Spot(), Water, inventory);
            player.Actions.TryStart(cancelFish, out _);
            cancelFish.Resolve(true);
            Check(inventory.GetItemCount("(O)fish") == 1, "cannot reward before custom catch stage");
            if (stage == 1) player.Actions.Update(Time(1), default);
            player.Actions.Update(Time(.01f), new(false, false, true));
            cancelFish.Resolve(true);
            Check(cancelFish.State == FishingState.Cancelled && !player.Actions.IsBusy
                && inventory.GetItemCount("(O)fish") == 1, "cancel prevents late reward at stage " + stage);
        }
        fishing.CreateAction = (spot, target, items) => new CustomFishing(spot, target, items);
        fishing.TryInteract(Water);
        Check(fishing.Active is CustomFishing, "factory starts user-defined fishing action");
        player.Actions.Cancel();
        DoorChecks.Run(Check);
        InventoryChecks.Run(Check);
        InventoryDragChecks.Run(Check);
        Console.WriteLine($"{passed} gameplay checks passed.");
    }
}

// Test-only catch rules. Production scaffold intentionally has none.
sealed class CustomFishing : FishingAction
{
    public CustomFishing(FishingSpot spot, Vector2 target, Inventory inventory) : base(spot, target, inventory) { }
    public void Resolve(bool success) => FinishCatch(success);
}
````

### First_game/UI/README.md

````markdown
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
````

### First_game/Doors/README.md

````markdown
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
3. Open Content/doors.json. InteractionKey defaults to E and InventoryKey to I.
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
````

### Tests/GameplayChecks/InventoryDragChecks.cs

````csharp
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
````

## 4. Setup and 5. Manual test checklist

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
3. Open Content/doors.json. InteractionKey defaults to E and InventoryKey to I.
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

## Validation result

Build succeeded with zero warnings and errors. All 147 gameplay checks passed on the installed .NET 10 runtime using --roll-forward Major, including zero bytes allocated across 1,000 warmed-up door transitions. Visual gameplay has not been manually tested.
