using Godot;
using Godot.Collections;
using System.Collections.Generic;

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

    // Setters are public so MatchManager can configure a Fighter.tscn instance
    // (archetype data, library, player slot) in code before adding it to the tree.
    [Export] public CharacterData Data    { get; set; } = null!;
    [Export] public AttackLibrary? Attacks { get; set; }
    [Export] public int PlayerIndex       { get; set; } = 0;

    // ── Component references (resolved in _Ready) ─────────────────────────────

    public CharacterStateMachine FSM              { get; private set; } = null!;
    public InputHandler          Input            { get; private set; } = null!;
    public MovementComponent     Movement         { get; private set; } = null!;
    public CombatComponent       Combat           { get; private set; } = null!;
    public Hurtbox               HurtboxContainer { get; private set; } = null!;
    /// Null until _Ready runs. Always check before calling SetPaused / Play.
    public AnimationController?  AnimController   { get; private set; }
    /// Optional shield node. Null if this character scene has no ShieldComponent;
    /// states must null-check before shielding.
    public ShieldComponent?      Shield           { get; private set; }

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

    /// Frames of FullInvincibility remaining after a respawn landing.
    /// Set by RespawnState on touchdown; counted down in _PhysicsProcess.
    public int TemporaryInvincibilityFrames { get; set; } = 0;

    // ── Platform drop-through ─────────────────────────────────────────────────

    /// Collision mask bits: layer 4 (value 8) = solid world, layer 5 (value 16) =
    /// one-way platforms. Must match the layers used in Stage.tscn / Fighter.tscn.
    private const uint WorldMaskBit    = 8;
    private const uint PlatformMaskBit = 16;

    /// While > 0, one-way platforms are removed from the collision mask so the
    /// character falls through them. Set by CrouchState on a hard-down input;
    /// counted down here. Solid ground (layer 4) is never affected, so pressing
    /// down on the main stage is simply a crouch.
    public int DropThroughFrames { get; set; } = 0;

    /// True while HitstopManager has frozen this character.
    /// CharacterController._PhysicsProcess skips all FSM logic while this is set,
    /// effectively pausing the attack frame counter on the hit frame.
    public bool IsHitstopFrozen { get; private set; } = false;

    // ── Signals ───────────────────────────────────────────────────────────────

    /// Emitted when the character crosses a blast zone. Consumed by RespawnManager.
    [Signal] public delegate void DiedEventHandler();

    /// Called by BlastZone. Emits the Died signal.
    public void Die() => EmitSignal(SignalName.Died);

    /// Called exclusively by HitstopManager. Freezes/thaws physics and animation.
    internal void SetHitstopFrozen(bool frozen)
    {
        IsHitstopFrozen = frozen;
        AnimController?.SetPaused(frozen);
    }

    // ── Godot lifecycle ───────────────────────────────────────────────────────

    public override void _Ready()
    {
        FSM              = GetNode<CharacterStateMachine>("StateMachine");
        Input            = GetNode<InputHandler>("InputHandler");
        Movement         = GetNode<MovementComponent>("MovementComponent");
        Combat           = GetNode<CombatComponent>("CombatComponent");
        HurtboxContainer = GetNode<Hurtbox>("HurtboxContainer");
        AnimController   = GetNodeOrNull<AnimationController>("AnimationController");
        Shield           = GetNodeOrNull<ShieldComponent>("ShieldComponent");

        Input.PlayerIndex    = PlayerIndex;
        AirJumpsRemaining    = Data.MaxAirJumps;

        _hitboxRoot = GetNodeOrNull<Node2D>("HitboxRoot");

        // Stamp every hitbox we own with a back-reference to this controller, so a
        // confirmed hit can be attributed to us (and self-hits filtered out).
        foreach (Hitbox hitbox in FindHitboxes(this))
            hitbox.AttackerRef = this;
    }

    // ── Combat node resolution ────────────────────────────────────────────────

    /// All hitboxes and the GrabBox live under this container, which is X-flipped
    /// with FacingDirection so attack positions always mirror correctly. It must
    /// NOT be under VisualRoot — AnimationController flips that independently.
    private Node2D? _hitboxRoot;

    /// Resolve a combat node (Hitbox, GrabBox, WeaponTrail) by the bare name used
    /// in AttackData, checking both direct children (legacy layout) and the
    /// HitboxRoot container (current layout).
    public T? FindCombatNode<T>(string name) where T : Node =>
        GetNodeOrNull<T>(name) ?? GetNodeOrNull<T>($"HitboxRoot/{name}");

    /// Recursively collect all Hitbox descendants of <paramref name="node"/>.
    private static IEnumerable<Hitbox> FindHitboxes(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is Hitbox hb) yield return hb;
            foreach (Hitbox nested in FindHitboxes(child)) yield return nested;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // ── Hitstop: attacker is frozen in place for hitlagFrames ticks ────
        if (IsHitstopFrozen)
        {
            // Sample input so the ring buffer doesn't stall (buffered presses
            // recorded during hitstop will still fire in the next state).
            Input.SampleFrame();

            Velocity = Vector2.Zero;
            MoveAndSlide();
            CharacterVelocity = Velocity;
            TickInvincibility();
            return;
        }

        // ── Tick order is critical ──────────────────────────────────────────
        // 1. Sample raw input first so states see fresh data this tick.
        Input.SampleFrame();

        // 2. Give the current state a chance to respond to input and
        //    queue a transition before physics calculations run.
        FSM.HandleInput(Input);

        // 3. State updates velocity (gravity, friction, drift, etc.).
        FSM.PhysicsUpdate(delta);

        // 3b. Drop-through: while the window is open, ignore one-way platforms.
        if (DropThroughFrames > 0)
        {
            DropThroughFrames--;
            CollisionMask = WorldMaskBit;
        }
        else
        {
            CollisionMask = WorldMaskBit | PlatformMaskBit;
        }

        // 4. Commit velocity and let Godot resolve collisions.
        Velocity = CharacterVelocity;
        MoveAndSlide();

        // 5. Sync back: MoveAndSlide may zero out velocity on wall/floor contact.
        CharacterVelocity = Velocity;

        // 6. Tick down post-respawn invincibility and lift it when the window expires.
        TickInvincibility();

        // 7. Shield health regenerates whenever the shield isn't raised.
        if (Shield is not null && FSM.CurrentStateName != "ShieldState")
            Shield.Regen();

        // 8. Mirror the hitbox container with facing so attacks hit the right side.
        if (_hitboxRoot is not null && (int)_hitboxRoot.Scale.X != FacingDirection)
            _hitboxRoot.Scale = new Vector2(FacingDirection, 1f);
    }

    private void TickInvincibility()
    {
        if (TemporaryInvincibilityFrames <= 0) return;
        TemporaryInvincibilityFrames--;
        if (TemporaryInvincibilityFrames == 0)
            HurtboxContainer.CurrentInvincibility = Hurtbox.InvincibilityType.None;
    }

    // ── Hit reception (called by attacker's Hitbox signal) ───────────────────

    /// Process a confirmed hit from an attacker.
    /// <param name="hitstunMultiplier">Attack-level hitstun scale from AttackData.</param>
    public void ReceiveHit(CharacterController attacker, HitboxData hitboxData, float hitstunMultiplier = 1f)
    {
        // ── Shield absorb ─────────────────────────────────────────────────────
        // A raised shield eats the hit: chip damage to shield health, brief
        // pushback, no percent gained. Grab-type hitboxes pass straight through
        // (grabs beat shields). A chip that empties the shield BREAKS it.
        if (FSM.CurrentStateName == "ShieldState" &&
            Shield is not null && !Shield.IsBroken &&
            hitboxData.Type != HitboxType.Grab)
        {
            bool broke = Shield.Chip(hitboxData.Damage);
            if (broke)
            {
                Shield.Hide();
                FSM.TransitionTo("HitstunState", new Dictionary
                {
                    { "launch_velocity", new Vector2(0f, -300f) },
                    { "hitstun_frames",  Data.ShieldBreakStunFrames },
                    { "hitlag_frames",   hitboxData.HitlagFrames },
                });
                // Restore health now; the long stun itself is the punishment.
                Shield.Reset();
            }
            else
            {
                // Shield pushback: slide away from the attacker, scaled by damage.
                CharacterVelocity = new Vector2(
                    attacker.FacingDirection * hitboxData.Damage * 28f, 0f);
            }

            HitstopManager.RequestFreeze(attacker, hitboxData.HitlagFrames);
            return;
        }

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

        int hitstunFrames = Mathf.RoundToInt(
            CombatComponent.CalculateHitstun(hitboxData, knockbackMagnitude) * hitstunMultiplier);

        FSM.TransitionTo("HitstunState", new Dictionary
        {
            { "launch_velocity",  launchVelocity  },
            { "hitstun_frames",   hitstunFrames   },
            { "hitlag_frames",    hitboxData.HitlagFrames },
        });

        // Freeze the ATTACKER for the same hitlag duration.
        // The defender's freeze is managed internally by HitstunState (animation)
        // and the hitlag phase of HitstunState.PhysicsUpdate() (velocity).
        HitstopManager.RequestFreeze(attacker, hitboxData.HitlagFrames);
    }
}
