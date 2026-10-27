using System.Text.Json;
using PokeRogue.Core.Data;

namespace PokeRogue.Core.Audio;

/// <summary>
/// One instrument of a track: its sound, and its notes as a tracker would write them, one token per step:
/// "C5" starts a note (several at once with "+": "C4+E4+G4"), "." holds the previous one, "-" is silence.
/// A drum channel (<see cref="Drums"/>) uses "k" kick, "s" snare, "h" hat and "o" open hat instead. Bars may be
/// separated by "|". Patterns are played in <see cref="Order"/>; a channel shorter than the track starts again.
/// </summary>
public sealed record MusicChannel
{
    public string Wave { get; set; } = "square";

    public float Duty { get; set; } = 0.5f;

    public float Volume { get; set; } = 0.3f;

    public float Attack { get; set; } = 0.005f;

    public float Release { get; set; } = 0.04f;

    public float Decay { get; set; }

    public float Vibrato { get; set; }

    public float VibratoRate { get; set; } = 5f;

    public float Cutoff { get; set; }

    /// <summary>Semitones up (or down) for every note.</summary>
    public int Transpose { get; set; }

    public bool Drums { get; set; }

    public Dictionary<string, string> Patterns { get; set; } = [];

    public List<string> Order { get; set; } = [];
}

/// <summary>A built-in track (phase 8.3): content/data/music/&lt;id&gt;.json.</summary>
public sealed record MusicFile
{
    public string NameIt { get; set; } = "";

    public float Bpm { get; set; } = 120f;

    /// <summary>Tokens per beat: 2 for eighth notes, 4 for sixteenths.</summary>
    public int StepsPerBeat { get; set; } = 2;

    /// <summary>False for jingles that play once (victory, defeat).</summary>
    public bool Loop { get; set; } = true;

    public float Volume { get; set; } = 1f;

    public List<MusicChannel> Channels { get; set; } = [];

    public static MusicFile Load(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize(stream, DataJsonContext.Default.MusicFile) ?? throw new InvalidDataException($"{path} is empty.");
    }
}

/// <summary>Turns a <see cref="MusicFile"/> into samples: every note becomes a <see cref="SoundPart"/> for the <see cref="Synth"/>.</summary>
public static class MusicTrack
{
    /// <summary>The tokens a channel plays, its patterns one after another; an unknown pattern name is an error.</summary>
    public static List<string> Tokens(MusicChannel channel)
    {
        var tokens = new List<string>();
        var order = channel.Order.Count > 0 ? channel.Order : channel.Patterns.Keys.Take(1).ToList();
        foreach (var name in order)
        {
            if (!channel.Patterns.TryGetValue(name, out var pattern))
                throw new InvalidDataException($"Music: pattern '{name}' is not defined.");

            tokens.AddRange(pattern.Split([' ', '|', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries));
        }

        return tokens;
    }

    /// <summary>Steps in the whole track: its longest channel.</summary>
    public static int Steps(MusicFile file) => file.Channels.Count == 0 ? 0 : file.Channels.Max(c => Tokens(c).Count);

    public static float StepSeconds(MusicFile file) => 60f / file.Bpm / Math.Max(1, file.StepsPerBeat);

    /// <summary>The notes of the track as synth voices (with their start and length), shorter channels repeated to the end.</summary>
    public static IEnumerable<SoundPart> Voices(MusicFile file)
    {
        var steps = Steps(file);
        var step = StepSeconds(file);
        foreach (var channel in file.Channels)
        {
            var tokens = Tokens(channel);
            if (tokens.Count == 0)
                continue;

            for (var i = 0; i < steps; i++)
            {
                var token = tokens[i % tokens.Count];
                if (token is "." or "-" or "_")
                    continue;

                if (channel.Drums)
                {
                    foreach (var hit in Drum(token, i * step, channel.Volume))
                        yield return hit;
                    continue;
                }

                // The note lasts while the next tokens hold it ("."), up to the end of the track.
                var length = 1;
                while (i + length < steps && tokens[(i + length) % tokens.Count] == ".")
                    length++;

                foreach (var note in token.Split('+'))
                {
                    if (Synth.NoteFrequency(note) is not { } frequency)
                        throw new InvalidDataException($"Music: '{token}' is not a note.");

                    frequency *= MathF.Pow(2f, channel.Transpose / 12f);
                    yield return new SoundPart
                    {
                        Wave = channel.Wave,
                        Duty = channel.Duty,
                        From = frequency,
                        At = i * step,
                        Length = length * step,
                        Attack = channel.Attack,
                        Release = channel.Release,
                        Decay = channel.Decay,
                        Volume = channel.Volume,
                        Vibrato = channel.Vibrato,
                        VibratoRate = channel.VibratoRate,
                        Cutoff = channel.Cutoff,
                    };
                }
            }
        }
    }

    /// <summary>The drum kit, all from the chip: a falling sine for the kick, noise for snare and hats.</summary>
    private static IEnumerable<SoundPart> Drum(string token, float at, float volume)
    {
        foreach (var hit in token)
        {
            switch (hit)
            {
                case 'k':
                    yield return new SoundPart { Wave = "sine", From = 150f, To = 42f, At = at, Length = 0.16f, Release = 0.05f, Volume = volume * 1.4f, Decay = 8f };
                    break;
                case 's':
                    yield return new SoundPart { Wave = "noise", From = 9000f, At = at, Length = 0.14f, Release = 0.08f, Volume = volume * 0.7f, Decay = 18f, Cutoff = 6000f };
                    yield return new SoundPart { Wave = "triangle", From = 200f, To = 150f, At = at, Length = 0.07f, Volume = volume * 0.6f, Decay = 20f };
                    break;
                case 'h':
                    yield return new SoundPart { Wave = "noise", From = 16000f, At = at, Length = 0.035f, Release = 0.02f, Volume = volume * 0.3f };
                    break;
                case 'o':
                    yield return new SoundPart { Wave = "noise", From = 16000f, At = at, Length = 0.16f, Release = 0.1f, Volume = volume * 0.25f, Decay = 10f };
                    break;
            }
        }
    }

    /// <summary>
    /// The whole track as mono samples. A looping track is exactly its steps long and the tails of its last notes wrap
    /// to its start, so it loops without a seam; a jingle keeps its tail.
    /// </summary>
    public static float[] Render(MusicFile file, int sampleRate)
    {
        var length = Steps(file) * StepSeconds(file);
        var voices = Voices(file).ToList();
        var end = file.Loop ? length : voices.Count == 0 ? 0f : voices.Max(v => v.At + v.Length) + 0.2f;
        var buffer = new float[Math.Max(1, (int)MathF.Round(end * sampleRate))];
        var seed = 1;
        foreach (var voice in voices)
            Synth.Add(buffer, voice with { Volume = voice.Volume * file.Volume }, sampleRate, seed++, wrap: file.Loop);

        Synth.Clip(buffer);
        return buffer;
    }
}
