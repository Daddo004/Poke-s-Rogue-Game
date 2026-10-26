using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeRogue.Game;

/// <summary>
/// Player settings (phase 3.6), kept in %APPDATA%\PokeRogue\settings.json between sessions. The full options screen
/// comes in Block 8; for now they are changed from the pause menu.
/// </summary>
public sealed class Settings
{
    /// <summary>Damage numbers over the enemies during a run.</summary>
    public bool ShowDamageNumbers { get; set; } = true;

    /// <summary>Borderless full screen at the monitor's resolution.</summary>
    public bool FullScreen { get; set; }

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
