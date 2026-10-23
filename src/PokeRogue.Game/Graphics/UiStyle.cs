using System.Text.Json;
using System.Text.Json.Serialization;
using PokeRogue.Game.Ui;

namespace PokeRogue.Game.Graphics;

/// <summary>Approved art direction (content/data/ui-style.json). Every property has the approved value as default.</summary>
public sealed record UiStyle
{
    public static readonly UiStyle Default = new();

    public string Approved { get; set; } = "";

    public int VirtualWidth { get; set; } = 640;

    public int VirtualHeight { get; set; } = 360;

    public string HudLayout { get; set; } = "classic";

    public string BodyFont { get; set; } = "PixelifySans";

    public float BodyFontSize { get; set; } = 16;

    public string SmallFont { get; set; } = "Silkscreen-Regular";

    public float SmallFontSize { get; set; } = 8;

    /// <summary>Damage numbers, levels, timer, counters (approved in 1.5: Pixelify's 5 reads as S).</summary>
    public string NumberFont { get; set; } = "Jersey10-Regular";

    public float NumberFontSize { get; set; } = 20;

    public string PanelTheme { get; set; } = "dark";

    public string MoveFx { get; set; } = "procedural";

    /// <summary>"cards" (approved in 1.5) or "list".</summary>
    public string LevelUpLayout { get; set; } = "cards";

    [JsonIgnore]
    public UiTheme Theme => PanelTheme == "light" ? UiTheme.Light : UiTheme.Dark;

    [JsonIgnore]
    public LevelUpLayout LevelUpWindowLayout => LevelUpLayout == "list" ? Ui.LevelUpLayout.List : Ui.LevelUpLayout.Cards;

    public static UiStyle Load(string contentRoot)
    {
        var path = Path.Combine(contentRoot, "data", "ui-style.json");
        if (!File.Exists(path))
            return Default;

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize(stream, UiStyleJsonContext.Default.UiStyle) ?? Default;
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(UiStyle))]
internal sealed partial class UiStyleJsonContext : JsonSerializerContext;
