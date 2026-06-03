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

    [ExportGroup("Dash & Dash-Dance")]
    /// Frames the InitialDash lasts before settling into a full run.
    [Export] public int   InitialDashFrames   { get; set; } = 12;
    /// Window (frames) from the start of a dash during which flicking the stick the
    /// other way produces a fresh dash instead of a turnaround — enables dash-dancing.
    [Export] public int   DashDanceWindowFrames { get; set; } = 12;
    /// Frames you must wait between foxtrots (re-dashing in the same direction).
    [Export] public int   FoxtrotCooldownFrames { get; set; } = 2;

    [ExportGroup("Jumping")]
    [Export] public float FullHopVelocity  { get; set; } = -1050f;
    [Export] public float ShortHopVelocity { get; set; } = -650f;
    [Export] public float AirJumpVelocity  { get; set; } = -980f;
    /// How many extra jumps after the initial ground jump.
    [Export] public int   MaxAirJumps      { get; set; } = 1;
    /// Frames spent in JumpSquat before actually leaving the ground.
    [Export] public int   JumpSquatFrames  { get; set; } = 3;
    /// Fraction of grounded horizontal velocity carried into the jump (0–1).
    /// 1.0 = full dash-jump momentum (Melee-like); lower values feel heavier.
    [Export] public float JumpMomentumTransfer { get; set; } = 1f;
    /// Horizontal speed cap applied to the launch velocity of a dash-jump.
    [Export] public float MaxHorizontalJumpSpeed { get; set; } = 700f;

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

    [ExportGroup("Landing Lag")]
    /// Frames of recovery when touching down from a neutral fall (no attack).
    [Export] public int SoftLandingFrames    { get; set; } = 2;
    /// Frames of recovery when landing out of Helpless/special-fall. Deliberately punishing.
    [Export] public int HelplessLandingFrames { get; set; } = 20;
    /// Multiplier applied to an aerial's landing lag when L-cancelled (0–1). 0.5 = halved.
    [Export] public float LCancelMultiplier   { get; set; } = 0.5f;

    [ExportGroup("Ledge")]
    /// Offset (relative to the character origin) of the box that detects grabbable ledges.
    [Export] public Vector2 LedgeGrabBoxOffset { get; set; } = new Vector2(18f, -8f);
    [Export] public Vector2 LedgeGrabBoxSize   { get; set; } = new Vector2(24f, 40f);
    /// Intangibility frames granted on first grabbing a ledge.
    [Export] public int LedgeHangInvincibilityFrames { get; set; } = 30;
    /// Frames before the same character may regrab the same ledge (anti-stall).
    [Export] public int LedgeRegrabCooldownFrames     { get; set; } = 30;
    /// Frames the standard ledge get-up animation takes.
    [Export] public int LedgeGetupFrames { get; set; } = 26;

    [ExportGroup("Hurtbox Sizing")]
    /// Default standing hurtbox dimensions (pixels). Drives the CollisionShape2D at runtime.
    [Export] public Vector2 StandingHurtboxSize { get; set; } = new Vector2(40f, 90f);
    /// Hurtbox dimensions while crouching — typically shorter to duck under attacks.
    [Export] public Vector2 CrouchHurtboxSize   { get; set; } = new Vector2(44f, 52f);

    [ExportGroup("Shield")]
    /// Maximum shield health. Shield shatters (stun) at 0.
    [Export] public float ShieldMaxHealth   { get; set; } = 50f;
    /// Shield health drained per frame while actively shielding.
    [Export] public float ShieldDecayRate   { get; set; } = 0.28f;
    /// Shield health regained per frame while not shielding.
    [Export] public float ShieldRegenRate   { get; set; } = 0.20f;
    /// Frames of stun inflicted on the character when their shield breaks.
    [Export] public int   ShieldBreakStunFrames { get; set; } = 180;

    [ExportGroup("Combat")]
    /// Frames the character is frozen in hitlag when their own attack connects.
    [Export] public int AttackerHitlagFrames { get; set; } = 3;

    [ExportGroup("Respawn")]
    /// Frames of FullInvincibility granted the moment the character lands after a respawn.
    /// 120 frames = 2 seconds at 60 Hz — enough to get a footing without being ambushed.
    [Export] public int SpawnInvincibilityFrames { get; set; } = 120;
}
