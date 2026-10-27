using PokeRogue.Core.Audio;

namespace PokeRogue.Core.Tests.Audio;

public sealed class AudioTests
{
    private static readonly Lazy<AudioFile> File = new(() => AudioFile.Load(TestContent.Root));

    private static string MusicFolder => Path.Combine(TestContent.Root, "data", "music");

    [Theory]
    [InlineData("A4", 440f)]
    [InlineData("C4", 261.63f)]
    [InlineData("F#5", 739.99f)]
    [InlineData("Bb3", 233.08f)]
    [InlineData("B1", 61.74f)]
    public void Notes_have_their_pitch(string note, float hz) =>
        Assert.Equal(hz, Synth.NoteFrequency(note)!.Value, 0.05f);

    [Theory]
    [InlineData("H4")]
    [InlineData(".")]
    [InlineData("C")]
    public void Anything_else_is_not_a_note(string token) => Assert.Null(Synth.NoteFrequency(token));

    [Fact]
    public void Every_sound_effect_is_audible_and_never_clips()
    {
        var file = File.Value;
        Assert.True(file.Sfx.Count >= 30);
        foreach (var (id, spec) in file.Sfx)
        {
            var samples = Synth.Render(spec, file.SampleRate);
            var peak = samples.Max(MathF.Abs);
            Assert.True(peak > 0.05f, $"{id} is silent");
            Assert.True(peak <= 1f, $"{id} clips");
            Assert.True(samples.Length < file.SampleRate * 2, $"{id} lasts more than 2 s");
        }
    }

    [Fact]
    public void The_game_plays_the_sounds_it_names()
    {
        string[] used =
        [
            "menu-sposta", "menu-conferma", "menu-indietro", "menu-errore", "acquisto", "titolo-via", "pausa",
            "colpo", "colpo-forte", "ko", "gemma", "moneta", "cura", "oggetto", "strumento", "livello", "danno", "rinascita",
            "forziere-scuote", "forziere-apre", "dominante", "boss-allarme", "esplosione", "carica", "teletrasporto", "sparo-nemico",
            "roccia", "pokeball", "rompi-erba", "rompi-roccia", "cromatico", "missione", "evoluzione", "evoluzione-fine",
            "mossa-evoluta", "speciale", "speciale-impatto", "abilita", "allenatore",
            "mossa-elettro", "mossa-fuoco", "mossa-acqua", "mossa-vento", "mossa-colpo", "mossa-raggio", "mossa-ombra",
        ];
        var missing = used.Where(id => !File.Value.Sfx.ContainsKey(id)).ToList();
        Assert.True(missing.Count == 0, $"missing: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Every_track_of_the_music_map_exists()
    {
        var map = File.Value.Music;
        foreach (var track in new[] { map.Menu, map.Level, map.LevelDark, map.Boss, map.Victory, map.Defeat }.Concat(map.Levels.Values))
            Assert.True(System.IO.File.Exists(Path.Combine(MusicFolder, track + ".json")), $"{track}.json is missing");
    }

    [Fact]
    public void Every_channel_fits_its_track()
    {
        foreach (var path in Directory.EnumerateFiles(MusicFolder, "*.json"))
        {
            var music = MusicFile.Load(path);
            var steps = MusicTrack.Steps(music);
            Assert.True(steps > 0, path);
            foreach (var channel in music.Channels)
            {
                var count = MusicTrack.Tokens(channel).Count;
                Assert.True(steps % count == 0, $"{Path.GetFileName(path)}: a channel of {count} steps does not divide {steps}");
                if (!channel.Drums && music.Loop)
                    Assert.True(count % (music.StepsPerBeat * 4) == 0, $"{Path.GetFileName(path)}: a channel ends mid-bar ({count} steps)");
            }

            // Every note is a note: rendering throws on anything else.
            Assert.NotEmpty(MusicTrack.Voices(music));
        }
    }

    [Fact]
    public void A_looping_track_is_exactly_its_bars_long_and_a_jingle_keeps_its_tail()
    {
        var level = MusicFile.Load(Path.Combine(MusicFolder, "avventura.json"));
        var samples = MusicTrack.Render(level, 8000);
        var seconds = MusicTrack.Steps(level) * MusicTrack.StepSeconds(level);
        Assert.Equal(seconds * 8000, samples.Length, 2f);
        Assert.InRange(samples.Max(MathF.Abs), 0.2f, 1f);

        var jingle = MusicFile.Load(Path.Combine(MusicFolder, "vittoria.json"));
        var tail = MusicTrack.Render(jingle, 8000);
        Assert.True(tail.Length > MusicTrack.Voices(jingle).Max(v => v.At + v.Length) * 8000);
    }

    [Fact]
    public void Held_notes_last_and_chords_play_together()
    {
        var music = new MusicFile
        {
            Bpm = 120,
            StepsPerBeat = 2,
            Channels = [new MusicChannel { Patterns = new() { ["A"] = "C4+E4 . . - G4 . - -" } }],
        };

        var voices = MusicTrack.Voices(music).ToList();
        Assert.Equal(3, voices.Count);
        Assert.Equal(0.75f, voices[0].Length, 0.001f);
        Assert.Equal(voices[0].At, voices[1].At);
        Assert.Equal(1f, voices[2].At, 0.001f);
        Assert.Equal(0.5f, voices[2].Length, 0.001f);
    }

    [Fact]
    public void Notes_in_a_sound_effect_become_one_voice_each()
    {
        var parts = Synth.Expand([new SoundPart { Notes = "C5 E5 G5", Step = 0.05f, Length = 0.1f, From = 1f, To = 2f }]).ToList();
        Assert.Equal(3, parts.Count);
        Assert.Equal(0.1f, parts[2].At, 0.001f);
        Assert.Equal(parts[0].From * 2f, parts[0].To!.Value, 0.01f);
    }
}
