using Godot;
using Godot.Collections;

namespace Supersmash;

/// <summary>
/// Drives an attack through its three phases: Startup → Active → Recovery.
/// Frame counts for each phase are supplied at runtime via the msg dictionary
/// (or read from a lookup table — implement AttackLibrary for that later).
///
/// Phase boundaries:
///   [0,          StartupFrames)   — no hitbox, character is committing to the attack
///   [StartupFrames, ActiveEnd)    — hitbox ACTIVE, can confirm a hit
///   [ActiveEnd, TotalFrames)      — hitbox off, recovery lag, punishable
///
/// On completion the state transitions back to Idle or Fall depending on grounded status.
/// </summary>
public partial class AttackState : State
{
    // These are set from the msg dict in Enter(); defaults are safe fallbacks.
    private int _startupFrames = 5;
    private int _activeFrames  = 3;
    private int _recoveryFrames = 10;
    private int _totalFrames   => _startupFrames + _activeFrames + _recoveryFrames;

    private int _frameCount = 0;
    private bool _hitboxActive = false;

    // The hitbox node driven by this state. Resolved by name in Enter().
    private Hitbox? _hitbox;

    public override void Enter(Dictionary? msg = null)
    {
        _frameCount   = 0;
        _hitboxActive = false;

        // Read per-attack tuning from the transition message.
        if (msg is not null)
        {
            if (msg.TryGetValue("startup_frames",  out Variant s)) _startupFrames  = s.AsInt32();
            if (msg.TryGetValue("active_frames",   out Variant a)) _activeFrames   = a.AsInt32();
            if (msg.TryGetValue("recovery_frames", out Variant r)) _recoveryFrames = r.AsInt32();

            // Resolve the hitbox node by the name specified in the message,
            // e.g. msg["hitbox_node"] = "JabHitbox". Allows per-attack hitbox shapes.
            if (msg.TryGetValue("hitbox_node", out Variant h))
                _hitbox = Character.GetNodeOrNull<Hitbox>(h.AsString());
        }
    }

    public override void Exit()
    {
        _hitbox?.Deactivate();
        _hitboxActive = false;
    }

    public override void PhysicsUpdate(double delta)
    {
        _frameCount++;

        // ── Startup ───────────────────────────────────────────────────────────
        if (_frameCount < _startupFrames)
            return;

        // ── First active frame ────────────────────────────────────────────────
        if (_frameCount == _startupFrames && !_hitboxActive)
        {
            _hitbox?.Activate();
            _hitboxActive = true;
        }

        // ── Last active frame → deactivate ────────────────────────────────────
        int activeEnd = _startupFrames + _activeFrames;
        if (_frameCount == activeEnd && _hitboxActive)
        {
            _hitbox?.Deactivate();
            _hitboxActive = false;
        }

        // ── Recovery complete → exit attack ──────────────────────────────────
        if (_frameCount >= _totalFrames)
        {
            FSM.TransitionTo(Character.IsOnFloor() ? "IdleState" : "FallState");
        }
    }

    // Input is intentionally NOT handled during an attack.
    // Buffered inputs (jump, etc.) will naturally fire in the next state.
}
