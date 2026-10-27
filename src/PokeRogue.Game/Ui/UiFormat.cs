using System.Globalization;
using PokeRogue.Core.Run;

namespace PokeRogue.Game.Ui;

/// <summary>Italian number and time formatting (dot for thousands, comma for decimals).</summary>
public static class UiFormat
{
    public static string Number(long value) => value.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');

    public static string Decimal(float value, int decimals = 1) =>
        value.ToString("0." + new string('0', decimals), CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>"A", "A e B", "A, B e C".</summary>
    public static string List(IReadOnlyList<string> items) => items.Count switch
    {
        0 => "",
        1 => items[0],
        _ => string.Join(", ", items.Take(items.Count - 1)) + " e " + items[^1],
    };

    /// <summary>Run time as mm:ss (hours roll into minutes).</summary>
    public static string Clock(int ticks)
    {
        var seconds = ticks / RunClock.TicksPerSecond;
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }
}
