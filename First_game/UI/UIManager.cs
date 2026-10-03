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
