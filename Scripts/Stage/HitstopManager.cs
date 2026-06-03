using Godot;
using System.Collections.Generic;

namespace Supersmash;

/// <summary>
/// Singleton node that drives frame-freeze ("hitstop") for the ATTACKER the moment
/// a hit confirms, while HitstunState separately manages the DEFENDER's hitlag.
///
/// Responsibilities
///   • Freeze the attacker's physics (skips FSM updates, zeroes velocity) and
///     animation (SpeedScale → 0) for hitlagFrames ticks.
///   • Leave the defender alone — HitstunState already handles its hitlag internally,
///     except for animation pause, which HitstunState now calls directly via
///     Character.AnimController.SetPaused().
///
/// Wiring
///   1. Add a Node to the scene root. Attach this script.
///   2. Place it BELOW the character nodes in the scene tree so _PhysicsProcess
///      runs after characters have sampled input and before MoveAndSlide.
///      (A 1-frame offset is invisible at 60 Hz if ordering isn't perfect.)
///
/// CharacterController.ReceiveHit() calls:
///   HitstopManager.RequestFreeze(attacker, hitboxData.HitlagFrames);
/// </summary>
public partial class HitstopManager : Node
{
    public static HitstopManager? Instance { get; private set; }

    // Frame countdown per frozen character.
    private readonly Dictionary<CharacterController, int> _freezeCounters = new();

    public override void _Ready()    => Instance = this;
    public override void _ExitTree() => Instance = null;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Request that <paramref name="character"/> be frozen for
    /// <paramref name="frames"/> physics ticks. Safe to call multiple times —
    /// the longer of the new and existing durations wins.
    /// </summary>
    public static void RequestFreeze(CharacterController character, int frames)
    {
        if (Instance is null || !GodotObject.IsInstanceValid(character)) return;

        if (Instance._freezeCounters.TryGetValue(character, out int existing) && existing >= frames)
            return;

        Instance._freezeCounters[character] = frames;
        character.SetHitstopFrozen(true);
    }

    /// Convenience formula so callers don't need to embed the tuning constant.
    /// Scales hitstop with damage: weak hits are brief crunches; heavy hits are dramatic.
    /// Approximate match to Ultimate's per-hit freeze durations.
    public static int CalculateHitstopFrames(float damage) =>
        Mathf.Max(2, (int)(damage * 0.65f) + 2);

    // ── Tick ──────────────────────────────────────────────────────────────────

    public override void _PhysicsProcess(double delta)
    {
        if (_freezeCounters.Count == 0) return;

        List<CharacterController>? toThaw = null;

        foreach (var kvp in _freezeCounters)
        {
            if (!GodotObject.IsInstanceValid(kvp.Key))
            {
                (toThaw ??= new()).Add(kvp.Key);
                continue;
            }

            if (kvp.Value <= 1)
                (toThaw ??= new()).Add(kvp.Key);
            else
                _freezeCounters[kvp.Key] = kvp.Value - 1;
        }

        if (toThaw is null) return;

        foreach (var c in toThaw)
        {
            _freezeCounters.Remove(c);
            if (GodotObject.IsInstanceValid(c))
                c.SetHitstopFrozen(false);
        }
    }
}
