using Godot;
using Godot.Collections;

namespace Supersmash;

/// <summary>
/// Root of the character scene. Acts as a lightweight coordinator:
/// it owns the shared physics state, holds component references, and
/// drives the tick loop in the correct order.
///
/// Expected scene tree (node names are used for GetNode — must match):
///   CharacterController (CharacterBody2D)
///   ├── StateMachine          (CharacterStateMachine)
///   ├── InputHandler          (InputHandler)
///   ├── MovementComponent     (MovementComponent)
///   ├── CombatComponent       (CombatComponent)
///   ├── CollisionShape2D
///   ├── Sprite2D
///   └── HurtboxContainer      (Hurtbox / Area2D)
///       └── CollisionShape2D
/// </summary>
public partial class CharacterController : CharacterBody2D
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Export] public CharacterData Data { get; private set; } = null!;
    [Export] public int PlayerIndex    { get; set; }         = 0;

    // ── Component references (resolved in _Ready) ─────────────────────────────

    public CharacterStateMachine FSM       { get; private set; } = null!;
    public InputHandler          Input     { get; private set; } = null!;
    public MovementComponent     Movement  { get; private set; } = null!;
    public CombatComponent       Combat    { get; private set; } = null!;
    public Hurtbox               HurtboxContainer { get; private set; } = null!;

    // ── Shared physics state (read/written by states & components) ────────────

    /// Working velocity. States modify this; we commit it to Godot at end of tick.
    public Vector2 CharacterVelocity { get; set; } = Vector2.Zero;

    /// 1 = facing right, –1 = facing left.
    public int FacingDirection { get; set; } = 1;

    /// Whether the character is actively fast-falling.
    public bool IsFastFalling { get; set; } = false;

    /// Remaining double-jumps (or triple-jumps for floaties, etc.).
    public int AirJumpsRemaining { get; set; }

    /// Frame counter for the current state (incremented by states that need it).
    public int StateFrameCounter { get; set; } = 0;

    // ── Godot lifecycle ───────────────────────────────────────────────────────

    public override void _Ready()
    {
        FSM      = GetNode<CharacterStateMachine>("StateMachine");
        Input    = GetNode<InputHandler>("InputHandler");
        Movement = GetNode<MovementComponent>("MovementComponent");
        Combat   = GetNode<CombatComponent>("CombatComponent");
        HurtboxContainer = GetNode<Hurtbox>("HurtboxContainer");

        Input.PlayerIndex    = PlayerIndex;
        AirJumpsRemaining    = Data.MaxAirJumps;

        // Wire hit-reception directly on the controller.
        // The Hitbox that confirmed the hit routes here via signal.
        // (Connected from Hitbox.HitConfirmed by the AttackState of the attacker.)
    }

    public override void _PhysicsProcess(double delta)
    {
        // ── Tick order is critical ──────────────────────────────────────────
        // 1. Sample raw input first so states see fresh data this tick.
        Input.SampleFrame();

        // 2. Give the current state a chance to respond to input and
        //    queue a transition before physics calculations run.
        FSM.HandleInput(Input);

        // 3. State updates velocity (gravity, friction, drift, etc.).
        FSM.PhysicsUpdate(delta);

        // 4. Commit velocity and let Godot resolve collisions.
        Velocity = CharacterVelocity;
        MoveAndSlide();

        // 5. Sync back: MoveAndSlide may zero out velocity on wall/floor contact.
        CharacterVelocity = Velocity;
    }

    // ── Hit reception (called by attacker's Hitbox signal) ───────────────────

    /// Process a confirmed hit from an attacker.
    public void ReceiveHit(CharacterController attacker, HitboxData hitboxData)
    {
        if (HurtboxContainer.HasSuperArmour)
        {
            // Super armour: absorb the hit without launching. Still take damage.
            Combat.AddDamage(hitboxData.Damage);
            return;
        }

        Combat.AddDamage(hitboxData.Damage);

        float knockbackMagnitude = Combat.CalculateKnockback(hitboxData, Data);
        Vector2 launchVelocity   = CombatComponent.KnockbackToVelocity(
            knockbackMagnitude, hitboxData.LaunchAngle, attacker.FacingDirection);

        if (!hitboxData.IgnoresDI)
            launchVelocity = MovementComponent.ApplyDI(launchVelocity, Input.MoveStick);

        int hitstunFrames = CombatComponent.CalculateHitstun(hitboxData, knockbackMagnitude);

        FSM.TransitionTo("HitstunState", new Dictionary
        {
            { "launch_velocity",  launchVelocity  },
            { "hitstun_frames",   hitstunFrames   },
            { "hitlag_frames",    hitboxData.HitlagFrames },
        });
    }
}
