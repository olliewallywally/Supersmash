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
    private AttackData? _attack;
    private Hitbox?     _hitbox;
    private int         _frame;
    private bool        _hitboxLive;

    public override void Enter(Dictionary? msg = null)
    {
        _frame      = 0;
        _hitboxLive = false;
        _hitbox     = null;
        _attack     = null;

        string attackId = string.Empty;
        if (msg is not null && msg.TryGetValue("attack_type", out Variant v))
            attackId = v.AsString();

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
            _hitbox = Character.GetNodeOrNull<Hitbox>(_attack.HitboxNodeName);
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
    }

    public override void Exit()
    {
        // Safety net: never leave a hitbox live across a state change.
        if (_hitboxLive) _hitbox?.Deactivate();
        _hitboxLive = false;
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

        // First active frame → arm the hitbox.
        if (_frame == _attack.FirstActiveFrame && !_hitboxLive && _hitbox is not null)
        {
            _hitbox.Activate();
            _hitboxLive = true;
        }

        // First recovery frame → disarm the hitbox.
        if (_frame == _attack.FirstRecoveryFrame && _hitboxLive && _hitbox is not null)
        {
            _hitbox.Deactivate();
            _hitboxLive = false;
        }

        // Past the final recovery frame → return to neutral.
        if (_frame > _attack.TotalFrames)
        {
            FSM.TransitionTo(Character.IsOnFloor() ? "IdleState" : "FallState");
        }
    }

    // Inputs are intentionally not handled mid-attack; buffered presses (jump, etc.)
    // fire naturally in the next state once recovery ends.
}
