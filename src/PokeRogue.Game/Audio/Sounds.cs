using Microsoft.Xna.Framework.Audio;
using PokeRogue.Core.Audio;

namespace PokeRogue.Game.Audio;

/// <summary>
/// The sound effects (phase 8.3), made by the game's sound chip from content/data/audio.json the first time each plays.
/// A sound never plays twice within its <see cref="SoundSpec.MinGapTicks"/>, so a hundred hits in a tick make one.
/// Silent when muted (captures, --muto) or when no audio device is available.
/// </summary>
public sealed class Sounds(AudioFile file)
{
    private readonly Dictionary<string, SoundEffect?> _cache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _lastPlayed = new(StringComparer.Ordinal);
    private readonly Random _random = new(5);
    private long _tick;

    public bool Muted { get; set; }

    /// <summary>The player's setting, 0 to 1.</summary>
    public float Volume { get; set; } = 0.8f;

    /// <summary>Once per update: the clock of the gaps between plays.</summary>
    public void Update() => _tick++;

    public void Play(string id, float volume = 1f, float pitch = 0f)
    {
        if (Muted || Volume <= 0f || !file.Sfx.TryGetValue(id, out var spec))
            return;
        if (_lastPlayed.TryGetValue(id, out var last) && _tick - last < spec.MinGapTicks)
            return;
        if (Get(id, spec) is not { } sound)
            return;

        _lastPlayed[id] = _tick;
        if (spec.PitchJitter > 0f)
            pitch += (_random.NextSingle() * 2f - 1f) * spec.PitchJitter;

        try
        {
            sound.Play(Math.Clamp(spec.Volume * volume * Volume, 0f, 1f), Math.Clamp(pitch, -1f, 1f), 0f);
        }
        catch (Exception exception) when (exception is NoAudioHardwareException or InstancePlayLimitException)
        {
            // No sound card, or too many sounds at once: the game goes on.
        }
    }

    private SoundEffect? Get(string id, SoundSpec spec)
    {
        if (_cache.TryGetValue(id, out var sound))
            return sound;

        try
        {
            sound = new SoundEffect(Synth.ToPcm16(Synth.Render(spec, file.SampleRate)), file.SampleRate, AudioChannels.Mono);
        }
        catch (Exception exception) when (exception is NoAudioHardwareException or ArgumentException or InvalidOperationException)
        {
            sound = null;
        }

        _cache[id] = sound;
        return sound;
    }
}
