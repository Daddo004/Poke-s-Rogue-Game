using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>Small pixel-art objects thrown by moves, drawn rotated in 16 steps so they stay crisp.</summary>
public sealed class MoveShapes(GraphicsDevice device)
{
    public Texture2D Bone { get; } = PixelArt.FromPattern(device,
    [
        ".oo......oo.",
        "owwo....owwo",
        "owwwoooowwwo",
        ".owwwwwwwwo.",
        "osswoooowsso",
        "osso....osso",
        ".oo......oo.",
    ], new Dictionary<char, Color> { ['o'] = new(96, 72, 48), ['w'] = new(244, 236, 210), ['s'] = new(204, 188, 150) });

    public Texture2D Leaf { get; } = PixelArt.FromPattern(device,
    [
        "...gggg..",
        ".gGGGGGg.",
        "gLLLLLLLs",
        ".gGGGGGg.",
        "...gggg..",
    ], new Dictionary<char, Color> { ['g'] = new(40, 110, 40), ['G'] = new(96, 186, 72), ['L'] = new(186, 236, 128), ['s'] = new(70, 140, 50) });

    public Texture2D Rock { get; } = PixelArt.FromPattern(device,
    [
        "...oooo....",
        "..oLLMMo...",
        ".oLMMMMMoo.",
        "oLMMMMMMMMo",
        "oMMMMMDMMMo",
        "oMMMMDDMMDo",
        ".oMMDDDDDo.",
        "..oDDDDDo..",
        "...ooooo...",
    ], new Dictionary<char, Color> { ['o'] = new(52, 46, 40), ['L'] = new(186, 176, 156), ['M'] = new(142, 132, 116), ['D'] = new(100, 92, 82) });

    /// <summary>Comete: a five-pointed gold star.</summary>
    public Texture2D Star { get; } = PixelArt.FromPattern(device,
    [
        "....o....",
        "...oyo...",
        "..oylyo..",
        "ooyylyyoo",
        "oyyywyyyo",
        ".oyyyyyo.",
        "..oyyyo..",
        ".oyyoyyo.",
        ".ooo.ooo.",
    ], new Dictionary<char, Color> { ['o'] = new(120, 80, 0), ['y'] = new(255, 214, 64), ['l'] = new(255, 244, 170), ['w'] = Color.White });

    /// <summary>Geloscheggia: a long ice crystal pointing along its flight, lit from the top-left.</summary>
    public Texture2D Shard { get; } = PixelArt.FromPattern(device,
    [
        ".....oo.....",
        "...oowwbo...",
        ".oowwwbbbdo.",
        "owwwbbbbbddo",
        ".oobbbbddoo.",
        "...oobddo...",
        ".....oo.....",
    ], new Dictionary<char, Color> { ['o'] = new(40, 80, 140), ['w'] = Color.White, ['b'] = new(170, 225, 255), ['d'] = new(110, 180, 240) });

    /// <summary>Forbice X: one long lens-shaped blade; two crossed make the X.</summary>
    public Texture2D Blade { get; } = PixelArt.FromPattern(device,
    [
        "......ooooo......",
        "...oooHHHHHooo...",
        "ooHHHHMMMMMMMMMoo",
        "...oooMMMMMooo...",
        "......ooooo......",
    ], new Dictionary<char, Color> { ['o'] = new(60, 70, 10), ['H'] = new(250, 255, 200), ['M'] = new(205, 235, 70) });

    /// <summary>Rocciotomba: a pointed stone rising out of the ground (drawn tip first while it rises).</summary>
    public Texture2D Spike { get; } = PixelArt.FromPattern(device,
    [
        "...o...",
        "..oLo..",
        "..oLMo.",
        ".oLMMo.",
        ".oLMMDo",
        ".oLMMDo",
        "oLMMMDo",
        "oLMMDDo",
        "oLMMDDo",
        "oMMMDDo",
        "oMMDDDo",
        ".ooooo.",
    ], new Dictionary<char, Color> { ['o'] = new(52, 46, 40), ['L'] = new(186, 176, 156), ['M'] = new(142, 132, 116), ['D'] = new(100, 92, 82) });

    /// <summary>Draws <paramref name="texture"/> centred at <paramref name="center"/>, rotation snapped to 16 steps.</summary>
    public static void DrawRotated(SpriteBatch batch, Texture2D texture, Vector2 center, float radians, Color tint, float scale = 1f)
    {
        const float step = MathF.Tau / 16f;
        var snapped = MathF.Round(radians / step) * step;
        batch.Draw(texture, new Vector2(MathF.Round(center.X), MathF.Round(center.Y)), null, tint, snapped,
            new Vector2(texture.Width / 2f, texture.Height / 2f), scale, SpriteEffects.None, 0f);
    }
}
