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
    private readonly MouseInput mouse = new MouseInput();

    public MouseInteractionController(
        Camera2D camera,
        WorldPickupSystem pickupSystem,
        PlayerInventory inventory)
    {
        this.camera = camera;
        this.pickupSystem = pickupSystem;
        this.inventory = inventory;
    }

    public void Update(
        GameTime gameTime,
        Viewport viewport,
        Vector2 playerPosition,
        bool inventoryOpen)
    {
        // Track presses even while the inventory is open, so closing it
        // while holding the button does not cause a new world interaction.
        mouse.Update();

        if (!mouse.RightClicked || inventoryOpen)
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
