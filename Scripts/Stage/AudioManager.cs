using Godot;
using System.Collections.Generic;

namespace Supersmash;

/// <summary>
/// Thin static helper that plays one-shot SFX from the Kenney Impact Sounds pack.
/// No autoload required — callers pass the SceneTree and AudioManager creates a
/// self-freeing AudioStreamPlayer in the scene root.
///
/// ── Hit sounds (Kenney CC0 — kenney.nl/assets/impact-sounds) ─────────────
///   damage >= 15  →  impactWood_heavy  (loud, thumpy — smash attacks)
///   damage >= 8   →  impactPunch_medium (solid — tilts / aerials)
///   damage <  8   →  impactGeneric_light (light — jabs)
///
/// ── Footstep / landing sounds ─────────────────────────────────────────────
///   PlayLandSound()      →  impactSoft_heavy
///   PlayFootstep(index)  →  footstep_concrete cycled by step index
///
/// Each variant has 5 files (_000 – _004); a random one is chosen on each call
/// so repeated sounds never stack identically.
/// </summary>
public static class AudioManager
{
    private const string BasePath = "res://Resources/Audio/SFX/Impact/";

    // ── Sound category file prefixes ─────────────────────────────────────────

    private static readonly string[] HeavyHit  = { "impactWood_heavy_",    "impactPunch_heavy_"   };
    private static readonly string[] MediumHit = { "impactPunch_medium_",  "impactSoft_medium_"   };
    private static readonly string[] LightHit  = { "impactGeneric_light_", "impactPunch_medium_"  };
    private static readonly string[] Land      = { "impactSoft_heavy_"                            };
    private static readonly string[] Footstep  = { "footstep_concrete_"                           };

    // Shared stream cache so each .ogg is loaded from disk only once.
    private static readonly Dictionary<string, AudioStream> _cache = new();

    // ── Public API ────────────────────────────────────────────────────────────

    /// Play the appropriate hit sound for a move that dealt <paramref name="damage"/> %.
    public static void PlayHitSound(SceneTree tree, float damage)
    {
        string[] category = damage >= 15f ? HeavyHit
                          : damage >=  8f ? MediumHit
                          :                 LightHit;

        // Pick a random prefix from the category then a random variant 000–004.
        string prefix  = category[GD.RandRange(0, category.Length - 1)];
        string variant = GD.RandRange(0, 4).ToString("D3");
        PlayOnce(tree, BasePath + prefix + variant + ".ogg", volumeDb: 0f);
    }

    /// Play a landing thud (call from IdleState.Enter when landing from the air).
    public static void PlayLandSound(SceneTree tree)
    {
        string variant = GD.RandRange(0, 4).ToString("D3");
        PlayOnce(tree, BasePath + Land[0] + variant + ".ogg", volumeDb: -4f);
    }

    /// Play a footstep sound cycled by <paramref name="stepIndex"/> % 5.
    public static void PlayFootstep(SceneTree tree, int stepIndex)
    {
        string variant = (stepIndex % 5).ToString("D3");
        PlayOnce(tree, BasePath + Footstep[0] + variant + ".ogg", volumeDb: -8f);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private static void PlayOnce(SceneTree tree, string path, float volumeDb)
    {
        if (!_cache.TryGetValue(path, out AudioStream? stream))
        {
            stream = ResourceLoader.Load<AudioStream>(path);
            if (stream is null)
            {
                GD.PushWarning($"[AudioManager] Could not load '{path}'.");
                return;
            }
            _cache[path] = stream;
        }

        var player     = new AudioStreamPlayer();
        player.Stream  = stream;
        player.VolumeDb = volumeDb;
        player.Bus     = "SFX";   // Route through a 'SFX' bus if it exists; falls back to Master.

        tree.CurrentScene.AddChild(player);
        player.Play();
        // Self-destruct when playback ends — no manual cleanup needed.
        player.Finished += player.QueueFree;
    }
}
