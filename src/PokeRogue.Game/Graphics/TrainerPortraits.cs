using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run.Trainers;

namespace PokeRogue.Game.Graphics;

/// <summary>
/// The trainers' sprites (content/trainers, 80×80 full figures) and the square around each head that the HUD's card shows
/// (phase 5.2). The head is found from the picture itself: the top of the figure, centred on the pixels of its first rows.
/// </summary>
public sealed class TrainerPortraits(SpriteLibrary sprites)
{
    /// <summary>Side of the head square, in sprite pixels.</summary>
    public const int HeadSize = 36;

    private readonly Dictionary<string, Rectangle> _heads = new(StringComparer.Ordinal);

    public Texture2D? Sprite(TrainerDefinition trainer) => sprites.Texture(trainer.SpritePath);

    /// <summary>The square around the trainer's head in their sprite.</summary>
    public Rectangle Head(TrainerDefinition trainer)
    {
        if (_heads.TryGetValue(trainer.Id, out var head))
            return head;

        head = new Rectangle(22, 4, HeadSize, HeadSize);
        if (Sprite(trainer) is { } texture)
            head = FindHead(texture);
        _heads[trainer.Id] = head;
        return head;
    }

    private static Rectangle FindHead(Texture2D texture)
    {
        var pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);

        // The first row with a few solid pixels is the top of the hair or the hat.
        var top = 0;
        for (var y = 0; y < texture.Height; y++)
        {
            var solid = 0;
            for (var x = 0; x < texture.Width; x++)
                solid += pixels[y * texture.Width + x].A > 128 ? 1 : 0;
            if (solid >= 3)
            {
                top = y;
                break;
            }
        }

        // Centre on the figure's pixels in the head's rows (hair, hat, face), not on an arm or a cape lower down.
        long sum = 0;
        var count = 0;
        for (var y = top; y < Math.Min(texture.Height, top + 22); y++)
        {
            for (var x = 0; x < texture.Width; x++)
            {
                if (pixels[y * texture.Width + x].A <= 128)
                    continue;
                sum += x;
                count++;
            }
        }

        var centre = count > 0 ? (int)(sum / count) : texture.Width / 2;
        var left = Math.Clamp(centre - HeadSize / 2, 0, texture.Width - HeadSize);
        var y0 = Math.Clamp(top - 2, 0, texture.Height - HeadSize);
        return new Rectangle(left, y0, HeadSize, HeadSize);
    }
}
