using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameLibrary.Graphics;
using MonoGameLibrary.Input;
using First_game.World;
using PlayerInventory = First_game.Inventory.Inventory;

namespace First_game.Input;

public class MouseInteractionController
{
    private const float PickupDistance = 200f;

    private readonly Camera2D camera;
    private readonly WorldPickupSystem pickupSystem;
    private readonly PlayerInventory inventory;
    private readonly MouseInput mouse;

    public MouseInteractionController(
        Camera2D camera,
        WorldPickupSystem pickupSystem,
        PlayerInventory inventory,
        MouseInput mouse)
    {
        this.camera = camera;
        this.pickupSystem = pickupSystem;
        this.inventory = inventory;
        this.mouse = mouse;
    }

    public void Update(
        GameTime gameTime,
        Viewport viewport,
        Vector2 playerPosition,
        bool blocksWorldInput)
    {
        // Game1 updates the shared mouse once, before UI and world input.
        if (!mouse.RightClicked || blocksWorldInput)
            return;

        Vector2 worldPosition = mouse.GetWorldPosition(camera, viewport);

        pickupSystem.TryCollectAt(
            playerPosition,
            worldPosition,
            inventory,
            PickupDistance,
            gameTime.TotalGameTime.TotalSeconds);
    }
}
