using Godot;
using Godot.Collections;

namespace Supersmash;

/// <summary>
/// A single attack (jab, f-tilt, nair, …) as an inspector-editable Resource.
///
/// Design note — where do Damage / BKB / KBG / Angle live?
///   Those per-hit values live on <see cref="HitboxData"/>, NOT here, because one
///   attack commonly has several hitboxes with DIFFERENT values: a "sweetspot"
///   (high knockback) and a "sourspot" (weak), or a multi-hit drill. So AttackData
///   owns the TIMING (startup/active/recovery) and references one-or-more HitboxData
///   payloads. For a simple move, just add a single element to Hitboxes.
///
/// Frame-data convention (1-indexed, matches how fighters list frame data):
///   Startup  occupies frames [1 .. StartupFrames]            — windup, no hitbox.
///   Active   occupies frames [StartupFrames+1 .. +ActiveFrames] — hitbox LIVE.
///   Recovery occupies the remainder up to TotalFrames        — cooldown, punishable.
/// </summary>
[GlobalClass]
public partial class AttackData : Resource
{
    [ExportGroup("Identity")]
    /// Lookup key used by AttackLibrary, e.g. "Jab", "ForwardTilt", "NeutralAir".
    [Export] public string AttackId { get; set; } = "";
    /// If true this move may be performed airborne (aerials); else grounded only.
    [Export] public bool IsAerial { get; set; } = false;

    [ExportGroup("Frame Data")]
    [Export] public int StartupFrames  { get; set; } = 4;
    [Export] public int ActiveFrames   { get; set; } = 3;
    [Export] public int RecoveryFrames { get; set; } = 12;

    [ExportGroup("Hitbox")]
    /// Node name of the Area2D Hitbox in the character scene to arm during active frames.
    [Export] public string HitboxNodeName { get; set; } = "";
    /// Combat payload(s). Element 0 is armed onto the hitbox node above.
    /// Multiple entries are reserved for sweetspot/sourspot & multi-hit support.
    [Export] public Array<HitboxData> Hitboxes { get; set; } = new();

    [ExportGroup("Tuning")]
    /// Scales the computed hitstun. >1 grants more frame advantage (combo tool);
    /// <1 makes a "safe" poke. Applied on top of the hitbox's base hitstun.
    [Export] public float HitstunMultiplier { get; set; } = 1.0f;

    // ── Derived ────────────────────────────────────────────────────────────────

    public int TotalFrames => StartupFrames + ActiveFrames + RecoveryFrames;

    /// First frame (1-indexed) the hitbox is live.
    public int FirstActiveFrame => StartupFrames + 1;

    /// First frame (1-indexed) of recovery — the frame the hitbox switches off.
    public int FirstRecoveryFrame => StartupFrames + ActiveFrames + 1;

    /// The primary payload armed onto the hitbox node, or null if none configured.
    public HitboxData? PrimaryHitbox => Hitboxes.Count > 0 ? Hitboxes[0] : null;
}
