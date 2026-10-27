using System.Text.Json.Serialization;

namespace PokeRogue.Core.Data;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true,
    WriteIndented = true,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(PokedexFile))]
[JsonSerializable(typeof(TypeChartFile))]
[JsonSerializable(typeof(MoveCatalogFile))]
[JsonSerializable(typeof(PokeRogue.Core.Run.Moves.MoveFile))]
[JsonSerializable(typeof(PokeRogue.Core.Run.Statuses.StatusFile))]
[JsonSerializable(typeof(PokeRogue.Core.Run.Items.ItemFile))]
[JsonSerializable(typeof(PokeRogue.Core.Run.Progression.LevelUpFile))]
[JsonSerializable(typeof(PokeRogue.Core.Run.Waves.WaveFile))]
[JsonSerializable(typeof(PokeRogue.Core.Run.Enemies.EnemyFile))]
[JsonSerializable(typeof(PokeRogue.Core.Run.Pickups.LootFile))]
[JsonSerializable(typeof(PokeRogue.Core.Run.Characters.CharacterFile))]
[JsonSerializable(typeof(PokeRogue.Core.Run.Specials.SpecialFile))]
[JsonSerializable(typeof(PokeRogue.Core.Run.Trainers.TrainerFile))]
[JsonSerializable(typeof(PokeRogue.Core.Meta.ShopFile))]
[JsonSerializable(typeof(PokeRogue.Core.Meta.MissionFile))]
[JsonSerializable(typeof(PokeRogue.Core.Run.Worlds.WorldsFile))]
[JsonSerializable(typeof(PokeRogue.Core.Run.Plates.PlateFile))]
[JsonSerializable(typeof(PokeRogue.Core.Run.Merchant.MerchantFile))]
[JsonSerializable(typeof(PokeRogue.Core.Audio.AudioFile))]
[JsonSerializable(typeof(PokeRogue.Core.Audio.MusicFile))]
public sealed partial class DataJsonContext : JsonSerializerContext;
