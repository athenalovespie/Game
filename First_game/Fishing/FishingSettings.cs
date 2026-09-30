namespace First_game.Fishing;

/// <summary>Tune the first fishing spot here. Times are seconds; distances are world units.</summary>
public sealed record FishingSettings
{
    public float CastRange { get; init; } = 350f;
    public float CastSeconds { get; init; } = 0.8f;
    public string RequiredToolId { get; init; } // Null means no tool required for the prototype.

    public void Validate()
    {
        float[] values = { CastRange, CastSeconds };
        foreach (float value in values)
            if (!float.IsFinite(value) || value <= 0)
                throw new System.ArgumentException("Fishing settings must be finite and positive.");
    }
}
