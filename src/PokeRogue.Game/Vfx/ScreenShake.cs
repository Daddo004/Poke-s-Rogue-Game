using Microsoft.Xna.Framework;

namespace PokeRogue.Game.Vfx;

/// <summary>Trauma-based camera shake: impacts add trauma, which decays; the offset grows with trauma squared.</summary>
public sealed class ScreenShake
{
    private const float MaxOffset = 5f;
    private const float DecayPerTick = 1f / 36f;

    private readonly Random _random = new(9);
    private float _trauma;

    /// <summary>Off by player choice (Options, phase 8.2).</summary>
    public bool Enabled { get; set; } = true;

    public void Add(float trauma) => _trauma = MathHelper.Clamp(_trauma + trauma, 0f, 1f);

    /// <summary>The player's own moves (phase 3.6): half as strong, and they never push the shake past this ceiling.</summary>
    public const float MoveCeiling = 0.22f;

    /// <summary>Shake from the player's moves: halved, and capped at <see cref="MoveCeiling"/> however many land at once (Tuono).</summary>
    public void AddFromMove(float trauma)
    {
        if (_trauma < MoveCeiling)
            _trauma = MathF.Min(MoveCeiling, _trauma + trauma * 0.5f);
    }

    public void Update() => _trauma = MathF.Max(0f, _trauma - DecayPerTick);

    public void Reset() => _trauma = 0f;

    /// <summary>Whole-pixel offset for this frame.</summary>
    public Vector2 Offset
    {
        get
        {
            if (!Enabled || _trauma <= 0f)
                return Vector2.Zero;

            var strength = _trauma * _trauma * MaxOffset;
            return new Vector2(MathF.Round((_random.NextSingle() * 2f - 1f) * strength), MathF.Round((_random.NextSingle() * 2f - 1f) * strength));
        }
    }
}
