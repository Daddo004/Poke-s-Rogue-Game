using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokeRogue.Game.Graphics.Floor;

/// <summary>Procedural pixel-art decorations that stand on the floor.</summary>
public static class FloorProps
{
    public static Texture2D Rock(GraphicsDevice device) => PixelArt.FromPattern(device,
    [
        "...oooooo...",
        "..oLLLMMMo..",
        ".oLLMMMMMDo.",
        "oLMMMMMMMDDo",
        "oMMMMMMMDDDo",
        "oMMMMMMDDDDo",
        ".oDDDDDDDDo.",
        "..oooooooo..",
    ], new Dictionary<char, Color>
    {
        ['o'] = new(40, 40, 52), ['L'] = new(200, 200, 208), ['M'] = new(152, 152, 168), ['D'] = new(104, 104, 124),
    });

    public static Texture2D Bush(GraphicsDevice device) => PixelArt.FromPattern(device,
    [
        "....oooooo....",
        "..ooLLMMMMoo..",
        ".oLLMMMMMMMDo.",
        "oLMMMMLMMMMDDo",
        "oMMMMMMMMMDDDo",
        "oMMLMMMMMDDDDo",
        "oMMMMMMMDDDDDo",
        ".oDMMMDDDDDDo.",
        "..ooDDDDDDoo..",
        "....oooooo....",
    ], new Dictionary<char, Color>
    {
        ['o'] = new(24, 64, 32), ['L'] = new(128, 200, 96), ['M'] = new(72, 148, 64), ['D'] = new(48, 108, 48),
    });

    public static Texture2D Stump(GraphicsDevice device) => PixelArt.FromPattern(device,
    [
        "..oooooooo..",
        ".oTTTTTTTTo.",
        "oTRRRRRRRRTo",
        "oTRrrrrrRRTo",
        ".oTTTTTTTTo.",
        ".oBBbBBBbBo.",
        ".oBbBBBBBbo.",
        ".oBBBBbBBBo.",
        "oBBBBBBBBBBo",
        ".oooooooooo.",
    ], new Dictionary<char, Color>
    {
        ['o'] = new(56, 36, 24), ['T'] = new(176, 128, 72), ['R'] = new(208, 160, 96), ['r'] = new(168, 120, 64),
        ['B'] = new(120, 80, 48), ['b'] = new(96, 64, 40),
    });
}
