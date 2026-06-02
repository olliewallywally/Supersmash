using Godot;
using Godot.Collections;

namespace Supersmash;

/// <summary>
/// Handles two sub-phases in one state:
///   1. JumpSquat — the ground anticipation frames before leaving the floor.
///      Determined by CharacterData.JumpSquatFrames (typically 3).
///   2. Rising — once airborne with jump velocity applied.
///
/// Short hop vs. full hop is determined by whether the jump button is still
/// held at the end of JumpSquat (held = full hop, released = short hop).
/// This matches how Smash Bros implements it for a natural, skill-expressive feel.
/// </summary>
public partial class JumpState : State
{
    private int _jumpSquatTimer = 0;
    private bool _jumpButtonHeld = false;

    public override void Enter(Dictionary? msg = null)
    {
        _jumpSquatTimer  = 0;
        _jumpButtonHeld  = true;
        Character.StateFrameCounter = 0;
    }

    public override void HandleInput(InputHandler input)
    {
        // Track whether the player releases jump during JumpSquat for short-hop detection.
        if (!input.IsHeld(GameAction.Jump))
            _jumpButtonHeld = false;

        // Air jump when airborne (after JumpSquat resolves).
        if (_jumpSquatTimer >= Character.Data.JumpSquatFrames &&
            input.IsBuffered(GameAction.Jump) &&
            Character.AirJumpsRemaining > 0)
        {
            input.Consume(GameAction.Jump);
            ApplyAirJump();
        }
    }

    public override void PhysicsUpdate(double delta)
    {
        Character.StateFrameCounter++;

        // ── JumpSquat phase ───────────────────────────────────────────────────
        if (_jumpSquatTimer < Character.Data.JumpSquatFrames)
        {
            _jumpSquatTimer++;

            // Stay grounded during JumpSquat — keep Y velocity flat.
            Character.CharacterVelocity = new Vector2(Character.CharacterVelocity.X, 0f);

            if (_jumpSquatTimer == Character.Data.JumpSquatFrames)
                LaunchFromGround();

            return;
        }

        // ── Airborne phase ────────────────────────────────────────────────────
        if (Character.IsOnFloor())
        {
            FSM.TransitionTo("IdleState");
            return;
        }

        // Transition to Fall once vertical velocity turns positive (arc peak passed).
        if (Character.CharacterVelocity.Y > 0)
        {
            FSM.TransitionTo("FallState");
            return;
        }

        Character.CharacterVelocity = MovementComponent.ApplyGravity(
            Character.CharacterVelocity, Character.Data, isFastFalling: false, delta);

        Character.CharacterVelocity = MovementComponent.ApplyAirDrift(
            Character.CharacterVelocity, Character.Input.MoveStick.X, Character.Data);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void LaunchFromGround()
    {
        float jumpVelocity = _jumpButtonHeld
            ? Character.Data.FullHopVelocity
            : Character.Data.ShortHopVelocity;

        Character.CharacterVelocity = new Vector2(
            Character.CharacterVelocity.X,
            jumpVelocity);
    }

    private void ApplyAirJump()
    {
        Character.AirJumpsRemaining--;
        Character.IsFastFalling = false;
        Character.CharacterVelocity = new Vector2(
            Character.CharacterVelocity.X,
            Character.Data.AirJumpVelocity);
    }
}
