using Microsoft.Xna.Framework.Audio;
using NVorbis;
using PokeRogue.Core.Audio;

namespace PokeRogue.Game.Audio;

/// <summary>
/// The music (phase 8.3). A track is the player's own file when there is one (content/audio/&lt;id&gt;.ogg or .wav),
/// otherwise the game's chiptune of the same name (content/data/music/&lt;id&gt;.json). Tracks are prepared on a
/// background thread the first time they are asked for; changing track fades the old one out and the new one in.
/// </summary>
public sealed class Music(string contentRoot, AudioFile file)
{
    private const float FadePerTick = 1f / 40f;

    private sealed record Track(byte[] Pcm, int SampleRate, AudioChannels Channels, bool Loop);

    private readonly Dictionary<string, Task<Track?>> _tracks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SoundEffect?> _effects = new(StringComparer.Ordinal);

    private SoundEffectInstance? _current;
    private SoundEffectInstance? _fading;
    private string? _currentId;
    private float _currentLevel;
    private float _fadingLevel;

    public MusicMap Map => file.Music;

    public bool Muted { get; set; }

    /// <summary>The player's setting, 0 to 1.</summary>
    public float Volume { get; set; } = 0.7f;

    /// <summary>Music is quieter under the pause (0 to 1).</summary>
    public float Duck { get; set; } = 1f;

    /// <summary>The track asked for last; null for silence.</summary>
    public string? Wanted { get; private set; }

    /// <summary>Asks for a track; the same one keeps playing unless <paramref name="restart"/>.</summary>
    public void Play(string? id, bool restart = false)
    {
        if (id == Wanted && !restart)
            return;

        Wanted = id;
        if (restart)
            _currentId = null;
        if (id is not null && !Muted)
            Prepare(id);
    }

    public void Stop() => Play(null);

    /// <summary>Starts preparing a track before it is needed (the boss' theme when a level starts).</summary>
    public void Prepare(string id)
    {
        if (!Muted && !_tracks.ContainsKey(id))
            _tracks[id] = Task.Run(() => Load(id));
    }

    /// <summary>Once per update: switches to the wanted track when it is ready, and moves the fades along.</summary>
    public void Update()
    {
        if (Muted)
        {
            StopAll();
            return;
        }

        if (Wanted != _currentId && (Wanted is null || Ready(Wanted)))
        {
            if (_current is not null)
            {
                Release(_fading);
                _fading = _current;
                _fadingLevel = _currentLevel;
            }

            _currentId = Wanted;
            _current = Wanted is null ? null : Start(Wanted);
            _currentLevel = 0f;
        }

        var target = Volume * file.Music.Volume * Duck;
        _currentLevel = MathF.Min(1f, _currentLevel + FadePerTick);
        SetVolume(_current, _currentLevel * target);
        if (_fading is not null)
        {
            _fadingLevel -= FadePerTick;
            if (_fadingLevel <= 0f)
            {
                Release(_fading);
                _fading = null;
            }
            else
            {
                SetVolume(_fading, _fadingLevel * target);
            }
        }
    }

    private bool Ready(string id) => _tracks.TryGetValue(id, out var task) && task.IsCompleted || _effects.ContainsKey(id);

    private SoundEffectInstance? Start(string id)
    {
        try
        {
            if (!_effects.TryGetValue(id, out var effect))
            {
                var track = _tracks[id].Result;
                effect = track is null ? null : new SoundEffect(track.Pcm, track.SampleRate, track.Channels);
                _effects[id] = effect;
            }

            if (effect is null)
                return null;

            var instance = effect.CreateInstance();
            instance.IsLooped = _tracks[id].Result?.Loop ?? true;
            instance.Volume = 0f;
            instance.Play();
            return instance;
        }
        catch (Exception exception) when (exception is NoAudioHardwareException or InstancePlayLimitException or ArgumentException or InvalidOperationException)
        {
            _effects[id] = null;
            return null;
        }
    }

    private static void SetVolume(SoundEffectInstance? instance, float volume)
    {
        if (instance is not null)
            instance.Volume = Math.Clamp(volume, 0f, 1f);
    }

    private static void Release(SoundEffectInstance? instance)
    {
        instance?.Stop();
        instance?.Dispose();
    }

    private void StopAll()
    {
        Release(_current);
        Release(_fading);
        _current = _fading = null;
        _currentId = null;
    }

    /// <summary>The player's own file first, then the built-in chiptune; null when there is neither (silence).</summary>
    private Track? Load(string id)
    {
        try
        {
            var folder = Path.Combine(contentRoot, "audio");
            var ogg = Path.Combine(folder, id + ".ogg");
            if (File.Exists(ogg))
                return LoadOgg(ogg, LoopOf(id));

            var wav = Path.Combine(folder, id + ".wav");
            if (File.Exists(wav))
                return LoadWav(wav, LoopOf(id));

            var json = Path.Combine(contentRoot, "data", "music", id + ".json");
            if (!File.Exists(json))
                return null;

            var music = MusicFile.Load(json);
            return new Track(Synth.ToPcm16(MusicTrack.Render(music, file.SampleRate)), file.SampleRate, AudioChannels.Mono, music.Loop);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or System.Text.Json.JsonException or ArgumentException or NoAudioHardwareException)
        {
            return null;
        }
    }

    /// <summary>Jingles play once, whatever file they come from.</summary>
    private bool LoopOf(string id) => id != file.Music.Victory && id != file.Music.Defeat;

    private static Track LoadOgg(string path, bool loop)
    {
        using var reader = new VorbisReader(path);
        var samples = new float[reader.TotalSamples * reader.Channels];
        var read = reader.ReadSamples(samples, 0, samples.Length);
        Array.Resize(ref samples, read);
        return new Track(Synth.ToPcm16(samples), reader.SampleRate, reader.Channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo, loop);
    }

    /// <summary>A 16-bit PCM WAV: its data chunk as it is.</summary>
    private static Track LoadWav(string path, bool loop)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        reader.ReadBytes(12);
        int channels = 1, rate = 44100;
        while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
        {
            var chunk = new string(reader.ReadChars(4));
            var size = reader.ReadInt32();
            if (chunk == "fmt ")
            {
                reader.ReadInt16();
                channels = reader.ReadInt16();
                rate = reader.ReadInt32();
                reader.ReadBytes(size - 8);
            }
            else if (chunk == "data")
            {
                return new Track(reader.ReadBytes(size), rate, channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo, loop);
            }
            else
            {
                reader.ReadBytes(size);
            }
        }

        throw new InvalidDataException($"{path} has no sound data.");
    }
}
