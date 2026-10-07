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
    // Named placeholder artwork; replace these draws when interior assets are available.
    private static readonly Color PlaceholderWall = new(77, 57, 49);
    private static readonly Color PlaceholderFloor = new(186, 151, 105);
    private static readonly Color PlaceholderExit = new(131, 83, 56);
    public bool DrawRug { get; set; } = true;
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
        batch.Draw(pixel, Floor, PlaceholderWall);
        batch.Draw(pixel, WalkableBounds, PlaceholderFloor);
        for (int y = WalkableBounds.Top + 48; y < WalkableBounds.Bottom; y += 48)
            batch.Draw(pixel, new Rectangle(WalkableBounds.Left, y, WalkableBounds.Width, 2),
                new Color(155, 122, 83));
        if (DrawRug)
        {
        Rectangle rug = new Rectangle(Floor.Center.X - 160, Floor.Center.Y - 100, 320, 200);
        batch.Draw(pixel, rug, new Color(90, 116, 107));
        batch.Draw(pixel, new Rectangle(rug.X + 8, rug.Y + 8, rug.Width - 16, rug.Height - 16),
            new Color(125, 149, 123));
        }
        for (int i = 0; i < Triggers.Doors.Length; i++)
        {
            Rectangle trigger = Triggers.Doors[i].TriggerBounds;
            batch.Draw(pixel, trigger, PlaceholderExit);
            batch.Draw(pixel, new Rectangle(trigger.Center.X - 18, trigger.Center.Y - 3, 36, 6),
                new Color(232, 196, 109));
        }
    }
}
