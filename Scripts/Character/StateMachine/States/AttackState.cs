using Godot;
using Godot.Collections;

namespace Supersmash;

/// <summary>
/// Drives one attack through Startup → Active → Recovery, reading all timing and
/// payload data from the character's AttackLibrary (no hard-coded numbers).
///
/// Entry contract:
///   FSM.TransitionTo("AttackState", "attack_type", "Jab");
/// The "attack_type" value is the AttackId looked up in Character.Attacks.
///
/// ── How frames are tracked precisely ─────────────────────────────────────────
/// _frame is a plain integer incremented exactly once per physics tick (the first
/// PhysicsUpdate sets it to 1). Because _PhysicsProcess runs on the fixed 60 Hz
/// tick — never delta-time — frame N here is the same wall-clock moment on every
/// machine. The hitbox is armed on the tick _frame == FirstActiveFrame and disarmed
/// on the tick _frame == FirstRecoveryFrame: exact, deterministic, rollback-safe.
///
/// ── How double-hits are prevented ────────────────────────────────────────────
/// The Hitbox node owns a HashSet of already-hit target instance IDs, cleared each
/// time Activate() is called. While live it records every target it confirms and
/// ignores repeats, so a target overlapping the box for all ActiveFrames is hit once.
/// </summary>
public partial class AttackState : State
{
    /// The AttackId that was requested when entering this state.
    /// Read by AnimationController to resolve the correct animation name.
    /// Remains set even if the attack is not found in the library (for debug logging).
    public string CurrentAttackId { get; private set; } = string.Empty;

    private AttackData?  _attack;
    private Hitbox?      _hitbox;   // tipper / primary
    private Hitbox?      _hitbox2;  // sourspot / secondary (null when unused)
    private WeaponTrail? _trail;
    private int          _frame;
    private bool         _hitboxLive;
    private bool         _startedAirborne;

    public override void Enter(Dictionary? msg = null)
    {
        _frame           = 0;
        _hitboxLive      = false;
        _hitbox          = null;
        _hitbox2         = null;
        _trail           = null;
        _attack          = null;
        _startedAirborne = !Character.IsOnFloor();
        CurrentAttackId  = string.Empty;

        string attackId = string.Empty;
        if (msg is not null && msg.TryGetValue("attack_type", out Variant v))
            attackId = v.AsString();

        CurrentAttackId = attackId; // set before library lookup so it's always readable

        _attack = Character.Attacks?.Get(attackId);
        if (_attack is null)
        {
            // Unknown move — bail safely on the next tick (never transition from Enter).
            GD.PushError($"[AttackState] No AttackData for '{attackId}'. Aborting to neutral.");
            return;
        }

        // Resolve and pre-arm the hitbox node with this attack's payload + tuning.
        if (!string.IsNullOrEmpty(_attack.HitboxNodeName))
        {
            _hitbox = Character.FindCombatNode<Hitbox>(_attack.HitboxNodeName);
            if (_hitbox is not null)
            {
                _hitbox.Data              = _attack.PrimaryHitbox;
                _hitbox.HitstunMultiplier = _attack.HitstunMultiplier;
            }
            else
            {
                GD.PushWarning($"[AttackState] Hitbox node '{_attack.HitboxNodeName}' not found on {Character.Name}.");
            }
        }

        // Resolve optional sourspot / secondary hitbox.
        if (!string.IsNullOrEmpty(_attack.HitboxNodeName2))
        {
            _hitbox2 = Character.FindCombatNode<Hitbox>(_attack.HitboxNodeName2);
            if (_hitbox2 is not null && _attack.Hitboxes.Count > 1)
            {
                _hitbox2.Data              = _attack.Hitboxes[1];
                _hitbox2.HitstunMultiplier = _attack.HitstunMultiplier;
            }
        }

        // Tipper priority: when the sweetspot confirms a hit, suppress that target
        // on the sourspot so it cannot land a second hit in the same activation window.
        if (_hitbox is not null && _hitbox2 is not null)
            _hitbox.HitConfirmed += OnTipperConfirmed;

        // Resolve optional weapon trail node.
        if (!string.IsNullOrEmpty(_attack.TrailNodeName))
            _trail = Character.FindCombatNode<WeaponTrail>(_attack.TrailNodeName);
    }

    public override void Exit()
    {
        // Safety net: never leave a hitbox or trail live across a state change.
        if (_hitboxLive)
        {
            _hitbox?.Deactivate();
            _hitbox2?.Deactivate();
        }
        _hitboxLive = false;
        _trail?.Deactivate();

        // Always disconnect the tipper signal even if _hitboxLive was false.
        if (_hitbox is not null && _hitbox2 is not null)
            _hitbox.HitConfirmed -= OnTipperConfirmed;
    }

    public override void PhysicsUpdate(double delta)
    {
        // Abort path for an unknown attack (we deferred this out of Enter).
        if (_attack is null)
        {
            FSM.TransitionTo(Character.IsOnFloor() ? "IdleState" : "FallState");
            return;
        }

        _frame++;

        // First active frame → arm both hitboxes, activate trail, spawn projectile.
        if (_frame == _attack.FirstActiveFrame)
        {
            if (!_hitboxLive && _hitbox is not null)
            {
                _hitbox.Activate();
                _hitbox2?.Activate();
                _hitboxLive = true;
            }

            _trail?.Activate();

            if (_attack.ProjectileScene is not null)
                SpawnProjectile();
        }

        // First recovery frame → disarm both hitboxes and trail.
        if (_frame == _attack.FirstRecoveryFrame)
        {
            if (_hitboxLive && _hitbox is not null)
            {
                _hitbox.Deactivate();
                _hitbox2?.Deactivate();
                _hitboxLive = false;
            }

            _trail?.Deactivate();
        }

        // ── Physics during the attack ─────────────────────────────────────────
        // Airborne attacks keep falling and drifting; grounded attacks skid to a
        // stop. Branch on where the attack STARTED — not IsAerial — so a special
        // usable in both air and ground (e.g. Falcon Punch) doesn't get gravity
        // and an instant landing-cancel when thrown from the ground.
        if (_startedAirborne)
        {
            Character.CharacterVelocity = MovementComponent.ApplyGravity(
                Character.CharacterVelocity, Character.Data, Character.IsFastFalling, delta);
            Character.CharacterVelocity = MovementComponent.ApplyAirMovement(
                Character.CharacterVelocity, Character.Input.MoveStick.X, Character.Data);

            // Landing-cancel: touching down mid-aerial cuts to landing lag.
            // Half the move's recovery, never less than soft landing. L-cancel
            // (halving again on a timed input) hooks in here later.
            if (Character.IsOnFloor())
            {
                int lag = Mathf.Max(Character.Data.SoftLandingFrames, _attack.RecoveryFrames / 2);
                FSM.TransitionTo("LandingState", "lag_frames", lag);
                return;
            }
        }
        else
        {
            Character.CharacterVelocity = MovementComponent.ApplyGroundFriction(
                Character.CharacterVelocity, Character.Data);
        }

        // Past the final recovery frame → return to neutral.
        if (_frame > _attack.TotalFrames)
        {
            FSM.TransitionTo(Character.IsOnFloor() ? "IdleState" : "FallState");
        }
    }

    // ── Move selection helpers (used by Idle/Run/Fall/Jump states) ──────────────

    /// Pick the aerial matching the stick direction, falling back to NeutralAir
    /// when the directional move doesn't exist in this character's library.
    public static string PickAerial(CharacterController character, Vector2 stick)
    {
        string id = "NeutralAir";
        if (Mathf.Abs(stick.X) > 0.4f)
            id = Mathf.Sign(stick.X) == character.FacingDirection ? "ForwardAir" : "BackAir";
        else if (stick.Y < -0.4f)
            id = "UpAir";

        return character.Attacks?.Get(id) is not null ? id : "NeutralAir";
    }

    /// Pick the grounded normal for the stick direction: up-tilt, forward tilt
    /// or smash (by stick magnitude), or jab at neutral.
    public static string PickGrounded(CharacterController character, Vector2 stick)
    {
        string id;
        if (stick.Y < -0.4f && Mathf.Abs(stick.Y) >= Mathf.Abs(stick.X))
            id = "UpTilt";
        else if (Mathf.Abs(stick.X) > 0.85f)
            id = "ForwardSmash";
        else if (Mathf.Abs(stick.X) > 0.3f)
            id = "ForwardTilt";
        else
            id = "Jab";

        return character.Attacks?.Get(id) is not null ? id : "Jab";
    }

    // Other inputs are intentionally not handled mid-attack; buffered presses
    // (jump, etc.) fire naturally in the next state once recovery ends.

    // ── Tipper priority ───────────────────────────────────────────────────────

    private void OnTipperConfirmed(CharacterController target, HitboxData data)
    {
        // Tipper (sweetspot) just confirmed a hit. Tell the sourspot to ignore
        // this target for the rest of the activation window so we never get
        // a double-hit from the two overlapping hitbox regions.
        _hitbox2?.SuppressTarget(target.GetInstanceId());
    }

    // ── Projectile spawning ───────────────────────────────────────────────────

    private void SpawnProjectile()
    {
        var proj = _attack!.ProjectileScene!.Instantiate<Projectile>();

        // Set runtime properties BEFORE AddChild so _Ready() sees correct values.
        proj.DirectionX     = Character.FacingDirection;
        proj.OwnerCharacter = Character;

        // Add to the scene root so the projectile is independent of both fighters.
        var scene = Character.GetTree().CurrentScene;
        scene.AddChild(proj);

        // Position AFTER AddChild (GlobalPosition requires the node to be in-tree).
        // Flip X offset by FacingDirection so it always spawns in front of the attacker.
        Vector2 offset = _attack.ProjectileSpawnOffset with
        {
            X = _attack.ProjectileSpawnOffset.X * Character.FacingDirection
        };
        proj.GlobalPosition = Character.GlobalPosition + offset;
    }
}
