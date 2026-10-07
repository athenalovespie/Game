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
    public Keys InventoryKey { get; set; } = Keys.Tab;
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
            // Fixed house rooms use the already-loaded pixel texture. Build them once at startup:
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
                if (!grid.IsValidCell(cell)) continue;
                grid.GetCell(cell).PlacementForbidden = true;
                if (grid.CanPlace(cell))
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
