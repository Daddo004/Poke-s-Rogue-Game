using Microsoft.Xna.Framework;
using PokeRogue.Core.Data;

namespace PokeRogue.Game.Graphics;

/// <summary>Classic type colours, used for move icons, type pills and move VFX tints.</summary>
public static class TypePalette
{
    public static Color Of(PokemonType type) => type switch
    {
        PokemonType.Normal => new Color(168, 168, 120),
        PokemonType.Fighting => new Color(192, 48, 40),
        PokemonType.Flying => new Color(168, 144, 240),
        PokemonType.Poison => new Color(160, 64, 160),
        PokemonType.Ground => new Color(224, 192, 104),
        PokemonType.Rock => new Color(184, 160, 56),
        PokemonType.Bug => new Color(168, 184, 32),
        PokemonType.Ghost => new Color(112, 88, 152),
        PokemonType.Steel => new Color(184, 184, 208),
        PokemonType.Fire => new Color(240, 128, 48),
        PokemonType.Water => new Color(104, 144, 240),
        PokemonType.Grass => new Color(120, 200, 80),
        PokemonType.Electric => new Color(248, 208, 48),
        PokemonType.Psychic => new Color(248, 88, 136),
        PokemonType.Ice => new Color(152, 216, 216),
        PokemonType.Dragon => new Color(112, 56, 248),
        PokemonType.Dark => new Color(112, 88, 72),
        _ => Color.Gray,
    };

    public static string NameIt(PokemonType type) => type switch
    {
        PokemonType.Normal => "Normale",
        PokemonType.Fighting => "Lotta",
        PokemonType.Flying => "Volante",
        PokemonType.Poison => "Veleno",
        PokemonType.Ground => "Terra",
        PokemonType.Rock => "Roccia",
        PokemonType.Bug => "Coleottero",
        PokemonType.Ghost => "Spettro",
        PokemonType.Steel => "Acciaio",
        PokemonType.Fire => "Fuoco",
        PokemonType.Water => "Acqua",
        PokemonType.Grass => "Erba",
        PokemonType.Electric => "Elettro",
        PokemonType.Psychic => "Psico",
        PokemonType.Ice => "Ghiaccio",
        PokemonType.Dragon => "Drago",
        PokemonType.Dark => "Buio",
        _ => type.ToString(),
    };
}
