using Microsoft.Xna.Framework.Audio;
using NVorbis;

namespace PokeRogue.Game.Audio;

/// <summary>
/// Pokémon cries from content/cries/NNNN.ogg (PokeAPI), decoded once with NVorbis and kept as sound effects.
/// The first sound of the game (phase 3.4: the boss' entrance); music and effects arrive in Block 8.
/// Silent when muted (captures, --muto) or when no audio device is available.
/// </summary>
public sealed class Cries(string contentRoot)
{
    private readonly Dictionary<int, SoundEffect?> _cache = [];

    public bool Muted { get; set; }

    public float Volume { get; set; } = 0.6f;

    public void Play(int speciesId, float pitch = 0f)
    {
        if (Muted || Get(speciesId) is not { } sound)
            return;

        try
        {
            sound.Play(Volume, pitch, 0f);
        }
        catch (Exception exception) when (exception is NoAudioHardwareException or InstancePlayLimitException)
        {
            // No sound card, or too many sounds at once: the game goes on silently.
        }
    }

    private SoundEffect? Get(int speciesId)
    {
        if (_cache.TryGetValue(speciesId, out var sound))
            return sound;

        _cache[speciesId] = sound = Load(Path.Combine(contentRoot, "cries", $"{speciesId:D4}.ogg"));
        return sound;
    }

    private static SoundEffect? Load(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            using var reader = new VorbisReader(path);
            var samples = new float[reader.TotalSamples * reader.Channels];
            var read = reader.ReadSamples(samples, 0, samples.Length);

            // 16-bit little-endian PCM, as SoundEffect wants it.
            var pcm = new byte[read * 2];
            for (var i = 0; i < read; i++)
            {
                var value = (short)Math.Clamp((int)(samples[i] * short.MaxValue), short.MinValue, short.MaxValue);
                pcm[i * 2] = (byte)value;
                pcm[i * 2 + 1] = (byte)(value >> 8);
            }

            return new SoundEffect(pcm, reader.SampleRate, reader.Channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo);
        }
        catch (Exception exception) when (exception is InvalidDataException or NoAudioHardwareException or ArgumentException)
        {
            return null;
        }
    }
}
