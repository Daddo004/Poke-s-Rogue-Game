using Microsoft.Xna.Framework;
using PokeRogue.Core.Data;

namespace PokeRogue.Game.Vfx;

/// <summary>Four tones per type for move effects: hottest centre, main body, dark edge, sparkles.</summary>
public readonly record struct TypeFxPalette(Color Hot, Color Main, Color Dark, Color Spark);

public static class TypeFx
{
    public static TypeFxPalette For(PokemonType type) => type switch
    {
        PokemonType.Fire => new(new(255, 250, 200), new(255, 150, 40), new(200, 40, 20), new(255, 220, 120)),
        PokemonType.Water => new(new(230, 250, 255), new(80, 160, 255), new(30, 70, 200), new(180, 230, 255)),
        PokemonType.Grass => new(new(220, 255, 180), new(100, 200, 70), new(40, 120, 40), new(180, 240, 120)),
        PokemonType.Electric => new(new(255, 255, 230), new(255, 230, 80), new(230, 170, 20), new(255, 250, 180)),
        PokemonType.Ice => new(new(255, 255, 255), new(160, 230, 255), new(90, 170, 230), new(220, 250, 255)),
        PokemonType.Fighting => new(new(255, 230, 200), new(230, 90, 60), new(150, 40, 30), new(255, 180, 140)),
        PokemonType.Poison => new(new(240, 200, 255), new(170, 90, 200), new(90, 40, 120), new(220, 160, 240)),
        PokemonType.Ground => new(new(250, 230, 180), new(200, 160, 100), new(120, 90, 50), new(230, 200, 150)),
        PokemonType.Flying => new(new(255, 255, 255), new(200, 220, 255), new(130, 150, 220), new(230, 240, 255)),
        PokemonType.Psychic => new(new(255, 220, 240), new(250, 100, 170), new(170, 40, 110), new(255, 170, 210)),
        PokemonType.Bug => new(new(240, 255, 180), new(170, 200, 40), new(100, 120, 20), new(210, 230, 120)),
        PokemonType.Rock => new(new(240, 230, 200), new(180, 160, 110), new(110, 95, 70), new(220, 205, 170)),
        PokemonType.Ghost => new(new(230, 210, 255), new(130, 100, 200), new(60, 40, 110), new(190, 170, 240)),
        PokemonType.Dragon => new(new(230, 210, 255), new(120, 80, 240), new(60, 30, 150), new(190, 160, 255)),
        PokemonType.Dark => new(new(220, 200, 190), new(110, 90, 80), new(50, 40, 40), new(170, 150, 140)),
        PokemonType.Steel => new(new(255, 255, 255), new(200, 200, 220), new(120, 120, 140), new(230, 230, 240)),
        _ => new(new(255, 255, 240), new(220, 220, 200), new(150, 150, 130), new(240, 240, 225)),
    };
}
