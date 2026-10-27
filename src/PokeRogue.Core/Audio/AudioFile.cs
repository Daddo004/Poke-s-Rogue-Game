using System.Text.Json;
using PokeRogue.Core.Data;

namespace PokeRogue.Core.Audio;

/// <summary>
/// One voice of a sound effect: a wave whose pitch slides from <see cref="From"/> to <see cref="To"/> over its length,
/// with a short attack, a fade at the end and an optional decay. <see cref="Notes"/> makes one such voice per note, one
/// <see cref="Step"/> apart (jingles, arpeggios).
/// </summary>
public sealed record SoundPart
{
    /// <summary>square, triangle, saw, sine or noise.</summary>
    public string Wave { get; set; } = "square";

    /// <summary>The square wave's share spent high: 0.5 is hollow, 0.125 thin and nasal.</summary>
    public float Duty { get; set; } = 0.5f;

    /// <summary>Pitch at the start, in Hz. For noise, how often it changes: low rumbles, high hisses.</summary>
    public float From { get; set; } = 440f;

    /// <summary>Pitch at the end; the same as <see cref="From"/> when left out.</summary>
    public float? To { get; set; }

    /// <summary>"C5 E5 G5": one voice per note (pitch from the note, sliding by the same ratio as From → To).</summary>
    public string? Notes { get; set; }

    /// <summary>Seconds between two notes; the length of a note when left at 0.</summary>
    public float Step { get; set; }

    /// <summary>Start, in seconds.</summary>
    public float At { get; set; }

    public float Length { get; set; } = 0.1f;

    public float Attack { get; set; } = 0.003f;

    /// <summary>Fade at the end of the voice, in seconds.</summary>
    public float Release { get; set; } = 0.03f;

    /// <summary>Exponential fall of the volume, per second (0: steady).</summary>
    public float Decay { get; set; }

    public float Volume { get; set; } = 1f;

    /// <summary>Vibrato depth in semitones.</summary>
    public float Vibrato { get; set; }

    public float VibratoRate { get; set; } = 6f;

    /// <summary>A gentle low-pass in Hz that takes the edge off squares and noise (0: none).</summary>
    public float Cutoff { get; set; }
}

/// <summary>A sound effect (phase 8.3): its voices, how loud, and how often it may play.</summary>
public sealed record SoundSpec
{
    public float Volume { get; set; } = 0.5f;

    public List<SoundPart> Parts { get; set; } = [];

    /// <summary>Ticks before the same sound may play again (hits and gems come by the dozen).</summary>
    public int MinGapTicks { get; set; } = 2;

    /// <summary>Random pitch change on each play, ± this much (in MonoGame's -1..1 octave units).</summary>
    public float PitchJitter { get; set; }
}

/// <summary>Which track plays where (phase 8.3); a track is content/data/music/&lt;id&gt;.json, or content/audio/&lt;id&gt;.ogg when present.</summary>
public sealed record MusicMap
{
    public string Menu { get; set; } = "titolo";

    /// <summary>Levels in daylight.</summary>
    public string Level { get; set; } = "avventura";

    /// <summary>Caves, towers, levels at night or in the dark.</summary>
    public string LevelDark { get; set; } = "grotta";

    public string Boss { get; set; } = "boss";

    public string Victory { get; set; } = "vittoria";

    public string Defeat { get; set; } = "sconfitta";

    /// <summary>Levels with a track of their own: level id → track.</summary>
    public Dictionary<string, string> Levels { get; set; } = [];

    /// <summary>Music's loudness under the player's own setting.</summary>
    public float Volume { get; set; } = 0.55f;
}

/// <summary>content/data/audio.json: the sound effects and the music map.</summary>
public sealed record AudioFile
{
    public const string FileName = "audio.json";

    public int SampleRate { get; set; } = 22050;

    public Dictionary<string, SoundSpec> Sfx { get; set; } = [];

    public MusicMap Music { get; set; } = new();

    public static AudioFile Load(string contentRoot)
    {
        var path = Path.Combine(contentRoot, "data", FileName);
        if (!File.Exists(path))
            return new AudioFile();

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize(stream, DataJsonContext.Default.AudioFile) ?? new AudioFile();
    }
}
