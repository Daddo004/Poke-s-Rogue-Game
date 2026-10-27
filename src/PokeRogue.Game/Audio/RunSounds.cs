using PokeRogue.Core.Data;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Abilities;
using PokeRogue.Core.Run.Pickups;

namespace PokeRogue.Game.Audio;

/// <summary>What a run sounds like (phase 8.3): each event of the tick to its effect in content/data/audio.json.</summary>
public static class RunSounds
{
    public static void OnEvent(Sounds sounds, RunEvent e, RunState run)
    {
        switch (e.Type)
        {
            case RunEventType.EnemyHit when e.SpeciesId != 0:
                sounds.Play(e.Critical || e.Effectiveness > 1f ? "colpo-forte" : "colpo");
                break;
            case RunEventType.EnemyDefeated:
                sounds.Play("ko");
                break;
            case RunEventType.GemCollected:
                sounds.Play("gemma");
                break;
            case RunEventType.LootCollected:
                sounds.Play((LootKind)(int)e.Value switch
                {
                    LootKind.Coin or LootKind.Nugget or LootKind.BigNugget => "moneta",
                    LootKind.Potion or LootKind.OranBerry => "cura",
                    LootKind.HeldItem => "strumento",
                    _ => "oggetto",
                });
                break;
            case RunEventType.FindCollected:
                sounds.Play("strumento");
                break;
            case RunEventType.LevelUp:
                sounds.Play("livello");
                break;
            case RunEventType.PlayerHurt:
                sounds.Play("danno");
                break;
            case RunEventType.PlayerRevived:
                sounds.Play("rinascita");
                break;
            case RunEventType.MoveUsed when (int)e.Value < run.Player.Moves.Count:
                sounds.Play(MoveSound(run.Player.Moves[(int)e.Value].Definition.Type));
                break;
            case RunEventType.MoveEvolved:
                sounds.Play("mossa-evoluta");
                break;
            case RunEventType.EnemyShotFired:
                sounds.Play("sparo-nemico");
                break;
            case RunEventType.EnemyCharged:
                sounds.Play("carica");
                break;
            case RunEventType.EnemyExploded:
                sounds.Play("esplosione");
                break;
            case RunEventType.EnemyTeleported:
                sounds.Play("teletrasporto", 0.6f);
                break;
            case RunEventType.DominantAppeared:
                sounds.Play("dominante");
                break;
            case RunEventType.ChestDropped:
                sounds.Play("forziere-scuote");
                break;
            case RunEventType.BossAppeared or RunEventType.TrainerChallenged:
                sounds.Play("boss-allarme");
                break;
            case RunEventType.TrainerSentOut:
                sounds.Play("pokeball");
                break;
            case RunEventType.HazardLanded:
                sounds.Play("roccia");
                break;
            case RunEventType.PropBroken:
                sounds.Play((BreakableKind)(int)e.Value == BreakableKind.Rock ? "rompi-roccia" : "rompi-erba");
                break;
            case RunEventType.ShinyAppeared:
                sounds.Play("cromatico");
                break;
            case RunEventType.AbilityTriggered:
                switch ((AbilityEffect)(int)e.Value)
                {
                    case AbilityEffect.Teleported:
                        sounds.Play("teletrasporto");
                        break;
                    case AbilityEffect.Transformed or AbilityEffect.TransformEnded or AbilityEffect.Switched or AbilityEffect.Awakened
                        or AbilityEffect.Blocked or AbilityEffect.Dodged or AbilityEffect.Endured:
                        sounds.Play("abilita");
                        break;
                }

                break;
            case RunEventType.TrainerAbility when (Core.Run.Trainers.TrainerEffect)(int)e.Value == Core.Run.Trainers.TrainerEffect.Used:
                sounds.Play("allenatore");
                break;

            // Arceus' Plates (phase 9.1): found or chosen, then the effects that show; Kecleon's visits (phase 9.2).
            case RunEventType.PlateEffect when e.Value < 0f:
                sounds.Play("mossa-evoluta");
                break;
            case RunEventType.PlateEffect:
                switch ((Core.Run.Plates.PlateEffectKind)(int)e.Value)
                {
                    case Core.Run.Plates.PlateEffectKind.Tide:
                        sounds.Play("mossa-acqua");
                        break;
                    case Core.Run.Plates.PlateEffectKind.Bolt:
                        sounds.Play("mossa-elettro", 0.7f);
                        break;
                    case Core.Run.Plates.PlateEffectKind.BurnBlast:
                        sounds.Play("esplosione", 0.5f);
                        break;
                    case Core.Run.Plates.PlateEffectKind.Phantom:
                        sounds.Play("abilita");
                        break;
                }

                break;
            case RunEventType.MerchantArrived:
                sounds.Play("missione");
                break;
        }
    }

    /// <summary>The sound of the player's moves, by family of types: zaps, flames, bubbles, wind, blows, beams and shadows.</summary>
    public static string MoveSound(PokemonType type) => type switch
    {
        PokemonType.Electric => "mossa-elettro",
        PokemonType.Fire => "mossa-fuoco",
        PokemonType.Water => "mossa-acqua",
        PokemonType.Grass or PokemonType.Bug or PokemonType.Flying => "mossa-vento",
        PokemonType.Psychic or PokemonType.Ice or PokemonType.Dragon => "mossa-raggio",
        PokemonType.Poison or PokemonType.Dark or PokemonType.Ghost => "mossa-ombra",
        _ => "mossa-colpo",
    };
}
