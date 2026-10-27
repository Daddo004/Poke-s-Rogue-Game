using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeRogue.Game;

/// <summary>
/// Player settings (phase 3.6, all of them since 8.2), kept in %APPDATA%\PokeRogue\settings.json between sessions and
/// changed in Opzioni, from the main menu or the pause.
/// </summary>
public sealed class Settings
{
    /// <summary>Volumes go from 0 to this, in steps of one.</summary>
    public const int MaxVolume = 10;

    /// <summary>Damage numbers over the enemies during a run.</summary>
    public bool ShowDamageNumbers { get; set; } = true;

    /// <summary>Borderless full screen at the monitor's resolution.</summary>
    public bool FullScreen { get; set; }

    /// <summary>
    /// False: the game is scaled by a whole number, every pixel square (bars around it). True: it fills the screen,
    /// scaled up sharp first and then smoothly down to fit, so pixels stay crisp without gaps (phase 8.2).
    /// </summary>
    public bool FillScreen { get; set; } = OperatingSystem.IsAndroid();

    /// <summary>
    /// How big the HUD is drawn, in percent of its design size (the playtest of 2026-10-27 found it too big on a 1440p
    /// screen). It is rounded to whole monitor pixels per HUD pixel, so it stays sharp: at ×4 the sizes are 50, 75 and 100.
    /// </summary>
    public int HudPercent { get; set; } = OperatingSystem.IsAndroid() ? 100 : 75;

    /// <summary>The window's size; 0 keeps the default (twice the game's 640×360).</summary>
    public int WindowWidth { get; set; }

    public int WindowHeight { get; set; }

    /// <summary>Full screen's resolution; 0 is the monitor's own (borderless), anything else switches the monitor's mode.</summary>
    public int FullScreenWidth { get; set; }

    public int FullScreenHeight { get; set; }

    public int MusicVolume { get; set; } = 7;

    public int EffectsVolume { get; set; } = 8;

    public int CriesVolume { get; set; } = 7;

    /// <summary>How much the screen shakes, in percent (0, 25, 50, 75, 100).</summary>
    public int ShakePercent { get; set; } = 100;

    [JsonIgnore]
    public string? Path { get; private set; }

    public static Settings Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize(File.ReadAllText(path), SettingsJson.Default.Settings);
                if (loaded is not null)
                {
                    loaded.Path = path;
                    return loaded;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // A broken or locked file: start from the defaults.
        }

        return new Settings { Path = path };
    }

    /// <summary>Writes the settings; a failure (read-only folder, …) is silent, the game goes on.</summary>
    public void Save()
    {
        if (Path is null)
            return;

        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(this, SettingsJson.Default.Settings));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string DefaultPath =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PokeRogue", "settings.json");
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(Settings))]
internal sealed partial class SettingsJson : JsonSerializerContext;
