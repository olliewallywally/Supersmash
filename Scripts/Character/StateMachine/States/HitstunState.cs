using Godot;
using Godot.Collections;

namespace Supersmash;

/// <summary>
/// The defender's experience of being hit.
///
/// Two sub-phases:
///   1. Hitlag   — freeze frame. Both attacker and defender pause for HitlagFrames.
///                 Creates the satisfying "crunch" of a confirmed hit.
///   2. Hitstun  — defender tumbles in the knockback direction, unable to act,
///                 for HitstunFrames ticks. Player can influence trajectory via DI
///                 (already applied by CharacterController.ReceiveHit before entering).
///
/// During hitstun the player can hold a direction to set up DI for the NEXT hit
/// (SDI / smash DI is a future extension — it shifts position during hitlag).
/// </summary>
public partial class HitstunState : State
{
    private int _hitlagFrames  = 0;
    private int _hitstunFrames = 0;
    private int _frameCount    = 0;
    private bool _inHitlag     = true;

    public override void Enter(Dictionary? msg = null)
    {
        _frameCount = 0;
        _inHitlag   = true;

        if (msg is not null)
        {
            if (msg.TryGetValue("launch_velocity", out Variant v))
                Character.CharacterVelocity = v.AsVector2();

            if (msg.TryGetValue("hitlag_frames", out Variant hl))
                _hitlagFrames = hl.AsInt32();

            if (msg.TryGetValue("hitstun_frames", out Variant hs))
                _hitstunFrames = hs.AsInt32();
        }
    }

    public override void PhysicsUpdate(double delta)
    {
        _frameCount++;

        // ── Hitlag phase: freeze in place ─────────────────────────────────────
        if (_inHitlag)
        {
            if (_frameCount <= _hitlagFrames)
            {
                // Zero velocity so neither character moves during the freeze.
                Character.CharacterVelocity = Vector2.Zero;
                return;
            }
            _inHitlag   = false;
            _frameCount = 0; // reset counter for hitstun phase
        }

        // ── Hitstun phase: fly with knockback, gravity still applies ──────────
        if (_frameCount >= _hitstunFrames)
        {
            // Hitstun expired — allow the player to act again.
            FSM.TransitionTo(Character.IsOnFloor() ? "IdleState" : "FallState");
            return;
        }

        // Apply gravity during tumble so long-distance knockback has a natural arc.
        Character.CharacterVelocity = MovementComponent.ApplyGravity(
            Character.CharacterVelocity, Character.Data, isFastFalling: false, delta);
    }

    // No input allowed during hitstun (SDI will be added here later as a special case).
}
