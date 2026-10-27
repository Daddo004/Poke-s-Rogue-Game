namespace PokeRogue.Core.Audio;

/// <summary>
/// The game's own little sound chip (phase 8.3): square, triangle, saw, sine and noise voices with pitch slides, mixed
/// into mono samples between -1 and 1. Sound effects and the built-in music are made with it, so the game needs no
/// sound files; the same input always gives the same samples.
/// </summary>
public static class Synth
{
    /// <summary>"C4", "F#5", "Bb3" to Hz (A4 = 440); null for anything else.</summary>
    public static float? NoteFrequency(string note)
    {
        if (note.Length < 2)
            return null;

        int semitone = char.ToUpperInvariant(note[0]) switch
        {
            'C' => 0,
            'D' => 2,
            'E' => 4,
            'F' => 5,
            'G' => 7,
            'A' => 9,
            'B' => 11,
            _ => -1,
        };
        if (semitone < 0)
            return null;

        var i = 1;
        if (note[i] == '#')
        {
            semitone++;
            i++;
        }
        else if (note[i] == 'b' && i + 1 < note.Length)
        {
            semitone--;
            i++;
        }

        if (!int.TryParse(note.AsSpan(i), out var octave))
            return null;

        var midi = (octave + 1) * 12 + semitone;
        return 440f * MathF.Pow(2f, (midi - 69) / 12f);
    }

    /// <summary>Renders a sound effect at <paramref name="sampleRate"/>; volume is left to the player.</summary>
    public static float[] Render(SoundSpec spec, int sampleRate)
    {
        var parts = Expand(spec.Parts).ToList();
        var seconds = parts.Count == 0 ? 0f : parts.Max(p => p.At + p.Length);
        var buffer = new float[(int)MathF.Ceiling(seconds * sampleRate) + 1];
        var seed = 1;
        foreach (var part in parts)
            Add(buffer, part, sampleRate, seed++);

        Clip(buffer);
        return buffer;
    }

    /// <summary>A part with notes becomes one part per note.</summary>
    public static IEnumerable<SoundPart> Expand(IEnumerable<SoundPart> parts)
    {
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part.Notes))
            {
                yield return part;
                continue;
            }

            var slide = (part.To ?? part.From) / part.From;
            var step = part.Step > 0f ? part.Step : part.Length;
            var notes = part.Notes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < notes.Length; i++)
            {
                if (NoteFrequency(notes[i]) is not { } frequency)
                    continue;

                yield return part with { Notes = null, From = frequency, To = frequency * slide, At = part.At + i * step };
            }
        }
    }

    /// <summary>Mixes one voice into <paramref name="buffer"/>; past the end it wraps to the start when <paramref name="wrap"/> (looping music).</summary>
    public static void Add(float[] buffer, SoundPart part, int sampleRate, int seed, bool wrap = false)
    {
        var start = (int)(part.At * sampleRate);
        var count = (int)(part.Length * sampleRate);
        if (count <= 0)
            return;

        var to = part.To ?? part.From;
        var ratio = part.From > 0f && to > 0f ? to / part.From : 1f;
        var attack = MathF.Max(1f, part.Attack * sampleRate);
        var release = MathF.Max(1f, MathF.Min(part.Release * sampleRate, count));
        var noise = new NoiseSource(seed);
        var lowPass = part.Cutoff > 0f ? 1f - MathF.Exp(-2f * MathF.PI * part.Cutoff / sampleRate) : 1f;
        var filtered = 0f;
        var phase = 0.0;
        var level = part.Volume;

        for (var i = 0; i < count; i++)
        {
            var t = i / (float)sampleRate;
            var frequency = part.From * MathF.Pow(ratio, i / (float)count);
            if (part.Vibrato > 0f)
                frequency *= MathF.Pow(2f, part.Vibrato * MathF.Sin(2f * MathF.PI * part.VibratoRate * t) / 12f);

            phase += frequency / sampleRate;
            var wrapped = phase >= 1.0;
            if (wrapped)
                phase -= Math.Floor(phase);

            var p = (float)phase;
            var sample = part.Wave switch
            {
                "triangle" => 1f - 4f * MathF.Abs(p - 0.5f),
                "saw" => 2f * p - 1f,
                "sine" => MathF.Sin(2f * MathF.PI * p),
                "noise" => noise.Next(wrapped),
                _ => p < part.Duty ? 0.6f : -0.6f,
            };

            filtered += (sample - filtered) * lowPass;
            var envelope = MathF.Min(1f, i / attack) * MathF.Min(1f, (count - i) / release);
            if (part.Decay > 0f)
                envelope *= MathF.Exp(-part.Decay * t);

            var at = start + i;
            if (at >= buffer.Length)
            {
                if (!wrap)
                    break;
                at %= buffer.Length;
            }

            buffer[at] += filtered * envelope * level;
        }
    }

    /// <summary>Keeps every sample between -1 and 1, rounding off peaks instead of cutting them.</summary>
    public static void Clip(float[] buffer)
    {
        for (var i = 0; i < buffer.Length; i++)
        {
            var s = buffer[i];
            if (s > 0.9f || s < -0.9f)
                buffer[i] = MathF.Tanh(s);
        }
    }

    /// <summary>16-bit little-endian PCM.</summary>
    public static byte[] ToPcm16(float[] samples, float gain = 1f)
    {
        var pcm = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            var value = (short)Math.Clamp((int)(samples[i] * gain * short.MaxValue), short.MinValue, short.MaxValue);
            pcm[i * 2] = (byte)value;
            pcm[i * 2 + 1] = (byte)(value >> 8);
        }

        return pcm;
    }

    /// <summary>A mono 16-bit WAV file (previews: the sounds can be listened to outside the game).</summary>
    public static void WriteWav(string path, float[] samples, int sampleRate)
    {
        var pcm = ToPcm16(samples);
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write("RIFF"u8);
        writer.Write(36 + pcm.Length);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(pcm.Length);
        writer.Write(pcm);
    }

    /// <summary>White noise held between the wave's cycles, so its "pitch" sets how coarse it sounds.</summary>
    private struct NoiseSource(int seed)
    {
        private uint _state = (uint)(seed * 2654435761u) | 1u;
        private float _value;

        public float Next(bool change)
        {
            if (change || _value == 0f)
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                _value = (_state & 0xFFFF) / 32767.5f - 1f;
                if (_value == 0f)
                    _value = 0.001f;
            }

            return _value;
        }
    }
}
