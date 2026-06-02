using Godot;

namespace Supersmash;

/// <summary>
/// All numeric tuning parameters for a single character.
/// Create one .tres file per fighter; assign it in the inspector.
/// Every value is in pixels/sec or plain units — no magic constants in code.
/// </summary>
[GlobalClass]
public partial class CharacterData : Resource
{
    [ExportGroup("Identity")]
    /// Smash Bros weight scale. Heavier = more knockback resistance. Typical range 75–135.
    [Export] public float Weight { get; set; } = 100f;

    [ExportGroup("Ground Movement")]
    [Export] public float WalkSpeed       { get; set; } = 300f;
    [Export] public float RunSpeed        { get; set; } = 600f;
    /// Speed burst applied on the first frame of a dash.
    [Export] public float InitialDashSpeed { get; set; } = 800f;
    /// Lerp factor applied each tick when decelerating on the ground (0–1, lower = more slide).
    [Export] public float GroundFriction  { get; set; } = 0.18f;
    /// Additional deceleration factor when turning around mid-dash.
    [Export] public float TurnaroundFriction { get; set; } = 0.30f;

    [ExportGroup("Jumping")]
    [Export] public float FullHopVelocity  { get; set; } = -1050f;
    [Export] public float ShortHopVelocity { get; set; } = -650f;
    [Export] public float AirJumpVelocity  { get; set; } = -980f;
    /// How many extra jumps after the initial ground jump.
    [Export] public int   MaxAirJumps      { get; set; } = 1;
    /// Frames spent in JumpSquat before actually leaving the ground.
    [Export] public int   JumpSquatFrames  { get; set; } = 3;

    [ExportGroup("Air Movement")]
    [Export] public float AirSpeed        { get; set; } = 420f;
    /// Lerp factor applied each tick toward the target air speed (0–1).
    [Export] public float AirAcceleration { get; set; } = 0.07f;
    /// Passive deceleration when no horizontal input is held in the air.
    [Export] public float AirFriction     { get; set; } = 0.02f;

    [ExportGroup("Gravity & Fall")]
    [Export] public float Gravity            { get; set; } = 2600f;
    [Export] public float MaxFallSpeed       { get; set; } = 1600f;
    /// Multiplier applied to gravity when fast-falling.
    [Export] public float FastFallMultiplier { get; set; } = 1.65f;
    [Export] public float FastFallMaxSpeed   { get; set; } = 2800f;

    [ExportGroup("Combat")]
    /// Frames the character is frozen in hitlag when their own attack connects.
    [Export] public int AttackerHitlagFrames { get; set; } = 3;
}
