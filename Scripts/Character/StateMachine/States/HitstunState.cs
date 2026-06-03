using Godot;
using Godot.Collections;

namespace Supersmash;

/// <summary>
/// The defender's experience of being hit. Two sequential sub-phases:
///
///   1. Hitlag  — Both characters freeze for HitlagFrames ticks. This is the
///               "crunch" of a confirmed hit. During this window the defender's
///               stick position is read for SDI (future feature — shifts position
///               slightly each hitlag frame to escape combos early).
///               IMPORTANT: the launch velocity is STORED, not applied, until
///               hitlag ends — applying it at entry then zeroing it in the loop
///               is a bug that sends every character straight down.
///
///   2. Hitstun — Defender tumbles along the stored launch vector for HitstunFrames
///               ticks. Gravity still applies (creates a natural arc). No other
///               actions are available until the counter expires.
/// </summary>
public partial class HitstunState : State
{
    private int     _hitlagFrames  = 0;
    private int     _hitstunFrames = 0;
    private int     _frameCount    = 0;
    private bool    _inHitlag      = true;
    private Vector2 _launchVelocity = Vector2.Zero;

    public override void Enter(Dictionary? msg = null)
    {
        _frameCount     = 0;
        _inHitlag       = true;
        _launchVelocity = Vector2.Zero;

        if (msg is not null)
        {
            // Store the launch velocity — don't apply it to the character yet.
            // CharacterVelocity will be zeroed during hitlag; we restore it after.
            if (msg.TryGetValue("launch_velocity",  out Variant v))  _launchVelocity = v.AsVector2();
            if (msg.TryGetValue("hitlag_frames",    out Variant hl)) _hitlagFrames   = hl.AsInt32();
            if (msg.TryGetValue("hitstun_frames",   out Variant hs)) _hitstunFrames  = hs.AsInt32();
        }

        // If there is no hitlag, commit the velocity immediately.
        if (_hitlagFrames <= 0)
        {
            _inHitlag = false;
            Character.CharacterVelocity = _launchVelocity;
        }
    }

    public override void PhysicsUpdate(double delta)
    {
        _frameCount++;

        // ── Phase 1: Hitlag ───────────────────────────────────────────────────
        if (_inHitlag)
        {
            if (_frameCount <= _hitlagFrames)
            {
                // Freeze in place. Both attacker (managed by AttackState) and defender
                // are stationary. Position reads for SDI would go here.
                Character.CharacterVelocity = Vector2.Zero;
                return;
            }

            // Hitlag just finished — now commit the launch velocity and begin tumbling.
            _inHitlag   = false;
            _frameCount = 0;
            Character.CharacterVelocity = _launchVelocity;
        }

        // ── Phase 2: Hitstun ──────────────────────────────────────────────────
        if (_frameCount >= _hitstunFrames)
        {
            FSM.TransitionTo(Character.IsOnFloor() ? "IdleState" : "FallState");
            return;
        }

        // Gravity shapes the tumble arc — no friction during knockback.
        Character.CharacterVelocity = MovementComponent.ApplyGravity(
            Character.CharacterVelocity, Character.Data, isFastFalling: false, delta);
    }

    // No input during hitstun. SDI (stick-shifting during hitlag for position escape)
    // is the planned extension here.
}
