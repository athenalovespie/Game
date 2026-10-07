using System;
using System.Collections.Generic;
using First_game.Interiors;
using First_game.Entities;
using First_game.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace First_game.Doors;

public sealed class DoorSystem : IDisposable
{
    private enum TransitionPhase { Idle, FadeOut, FadeIn }
    private readonly Player player;
    private readonly List<ResidentArea> areas;
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
        this.areas = new List<ResidentArea>(areas);
        this.fadeSeconds = fadeSeconds;
        this.logError = logError;
        ActiveArea = initialArea;
        player.MapId = initialArea.Id;
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

    public event Action TransitionStarted;
    public event Action<ResidentArea> OnEnterInterior;
    public event Action<ResidentArea> OnExitInterior;
    public IReadOnlyList<ResidentArea> Areas => areas;
    public void RegisterArea(ResidentArea area)
    {
        if (areas.Contains(area)) return;
        areas.Add(area); area.Triggers.Entered += OnOverlapChanged; area.Triggers.Exited += OnOverlapChanged;
        RefreshBindings();
    }
    public void UnregisterArea(ResidentArea area)
    {
        area.Triggers.Entered -= OnOverlapChanged; area.Triggers.Exited -= OnOverlapChanged;
        areas.Remove(area);
    }
    public void RefreshTriggers()
    {
        RefreshBindings(); ActiveArea.Triggers.Observe(player.Bounds); RefreshPrompt();
    }
    public void RestoreLocation(ResidentArea area, Vector2 position)
    {
        if (IsTransitioning || !area.IsReady || area.BlocksMovement(player.BoundsAt(position)))
            throw new InvalidOperationException("Saved player location is blocked or unavailable.");
        CommitLocation(area, position, null);
        RefreshPrompt();
    }
    private void CommitLocation(ResidentArea target, Vector2 position, AreaSpawn? spawn)
    {
        ResidentArea previous = ActiveArea;
        ActiveArea.Triggers.Reset(); ActiveArea = target; player.MapId = target.Id;
        ActiveArea.Triggers.Reset(); placingSpawn = true;
        player.Position = position;
        if (spawn.HasValue) player.FaceTowards(player.GroundPosition + spawn.Value.FacingDirection);
        player.RestoreIdleAnimation(); placingSpawn = false;
        ActiveArea.Triggers.Observe(player.Bounds, spawning: true);
        AreaChanged?.Invoke();
        if (previous != target)
        {
            if (!previous.IsExterior) OnExitInterior?.Invoke(previous);
            if (!target.IsExterior) OnEnterInterior?.Invoke(target);
        }
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
        for (int i = 0; i < areas.Count; i++)
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
            TransitionStarted?.Invoke();
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
        PortalDestination destination;
        Vector2 position;
        try
        {
            destination = door.Enterable.Resolve();
            position = player.PositionForGround(destination.Spawn.GroundPosition);
            if (destination.Area == null || !destination.Area.IsReady || destination.Area.BlocksMovement(player.BoundsAt(position)))
                throw new InvalidOperationException(door.DestinationError);
        }
        catch (Exception error)
        {
            LastError = door.DestinationError + " " + error.Message;
            logError?.Invoke(LastError);
            return;
        }
        // Commit owner identity before map events so observers see a consistent location.
        door.Enterable.OnArrived?.Invoke();
        CommitLocation(destination.Area, position, destination.Spawn);
    }

    public void Dispose()
    {
        player.PositionChanged -= OnPlayerMoved;
        player.Input.InteractChanged -= RefreshBindings;
        for (int i = 0; i < areas.Count; i++)
        {
            areas[i].Triggers.Entered -= OnOverlapChanged;
            areas[i].Triggers.Exited -= OnOverlapChanged;
        }
        player.InputLocked = false;
    }
}
