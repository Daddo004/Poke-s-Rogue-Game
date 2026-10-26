using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokeRogue.Game.Graphics;

/// <summary>
/// Breakables (phase 3.5): a tuft of the games' tall grass, pointed blades rather than the floor's round bushes, and a
/// sandy boulder, warmer than the floor's grey stones, so they read as things to break.
/// </summary>
public sealed class BreakableArt(GraphicsDevice device)
{
    public Texture2D Grass { get; } = PixelArt.FromPattern(device,
    [
        "..o...o....o..",
        ".oLo.oLo..oLo.",
        ".oLo.oLo.oLLo.",
        "oLMo.oLMooLMo.",
        "oLMoooLMooLMMo",
        "oMMoLoMMoLoMMo",
        "oMMoLooMMLoMDo",
        "oMDoMMoMDMMoDo",
        "oDDoMMoDDMMDDo",
        ".oDDMDDDDMDDo.",
        "..oooooooooo..",
    ], new Dictionary<char, Color>
    {
        ['o'] = new(24, 72, 32), ['L'] = new(156, 224, 104), ['M'] = new(84, 172, 64), ['D'] = new(48, 116, 48),
    });

    public Texture2D Rock { get; } = PixelArt.FromPattern(device,
    [
        "....oooooo......",
        "..ooLLLMMMoo....",
        ".oLLMMMMMMMDoo..",
        ".oLMMMMMMMMMDDo.",
        "oLMMMMMMMMMMDDDo",
        "oMMMMMMMMMMDDDDo",
        "oMMMMMMMMMDDDDDo",
        ".oDMMMMMDDDDDDo.",
        "..ooDDDDDDDDoo..",
        "....oooooooo....",
    ], new Dictionary<char, Color>
    {
        ['o'] = new(52, 40, 30), ['L'] = new(200, 176, 140), ['M'] = new(152, 128, 100), ['D'] = new(108, 88, 68),
    });

    public static readonly Color GrassLight = new(156, 224, 104);
    public static readonly Color GrassDark = new(48, 116, 48);
    public static readonly Color RockLight = new(200, 176, 140);
    public static readonly Color RockDark = new(108, 88, 68);
    public static readonly Color Crack = new(52, 40, 30);
}
