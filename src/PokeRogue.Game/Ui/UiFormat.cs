using System.Globalization;
using PokeRogue.Core.Run;

namespace PokeRogue.Game.Ui;

/// <summary>Italian number and time formatting (dot for thousands, comma for decimals).</summary>
public static class UiFormat
{
    public static string Number(long value) => value.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');

    public static string Decimal(float value, int decimals = 1) =>
        value.ToString("0." + new string('0', decimals), CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>Run time as mm:ss (hours roll into minutes).</summary>
    public static string Clock(int ticks)
    {
        var seconds = ticks / RunClock.TicksPerSecond;
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }
}
