using Godot;

namespace Supersmash;

public enum HitboxType
{
    Normal,
    Grab,
    Wind,       // pushes without damage
    Reflectable,
    Absorbable,
}

/// <summary>
/// Data asset describing a single hitbox on a single active frame window.
/// One attack can reference multiple HitboxData resources (e.g. inner/outer sweet/sour spots).
/// </summary>
[GlobalClass]
public partial class HitboxData : Resource
{
    [ExportGroup("Damage")]
    [Export] public float Damage { get; set; } = 5f;

    [ExportGroup("Knockback")]
    /// Fixed knockback added regardless of target's percent. Sets the "floor" of the hit.
    [Export] public float BaseKnockback    { get; set; } = 30f;
    /// Scales how much the target's percent amplifies knockback. Higher = kill move.
    [Export] public float KnockbackGrowth  { get; set; } = 50f;
    /// Launch angle in degrees. 0 = direct right, 90 = straight up, 270/–90 = spike down.
    [Export] public float LaunchAngle      { get; set; } = 45f;
    /// When true, launch angle is always relative to the vector between attacker and defender
    /// (used for meteor smashes and grabs).
    [Export] public bool  AngleFlipsWithFacing { get; set; } = true;

    [ExportGroup("Frame Data")]
    /// Frames both attacker and defender freeze on hit. Creates the satisfying "crunch" feel.
    [Export] public int HitlagFrames  { get; set; } = 5;
    /// Frames the defender spends in hitstun (unable to act) after the hitlag ends.
    [Export] public int HitstunFrames { get; set; } = 20;

    [ExportGroup("Properties")]
    [Export] public HitboxType Type { get; set; } = HitboxType.Normal;
    /// If true, DI has no effect on this hit (e.g., command grabs, spikes).
    [Export] public bool IgnoresDI { get; set; } = false;
    /// Unique ID within an attack to prevent the same box hitting the same target twice.
    [Export] public int HitboxId { get; set; } = 0;
}
