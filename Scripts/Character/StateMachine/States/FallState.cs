using Godot;
using Godot.Collections;

namespace Supersmash;

/// <summary>
/// The descending arc. Entered from JumpState (past the apex), from walking off a
/// ledge, or after hitstun ends in the air.
///
/// Handles:
///   • Gravity (fast-fall-aware terminal velocity).
///   • Air drift via the unified ApplyAirMovement (over-speed decay included).
///   • Fast-fall on a deliberate downward flick — instant snap, locked until landing.
///   • Mid-air jumps (instantaneous, bypassing JumpSquat → hands to JumpState).
///   • Aerial attacks.
/// </summary>
public partial class FallState : State
{
    public override void Enter(Dictionary? msg = null)
    {
        Character.StateFrameCounter = 0;
    }

    public override void HandleInput(InputHandler input)
    {
        // ── Mid-air jump ──────────────────────────────────────────────────────
        // Instantaneous: no JumpSquat window in the air. Preserve horizontal
        // momentum, override vertical, then hand to JumpState for the rising arc.
        if (input.IsBuffered(GameAction.Jump) && Character.AirJumpsRemaining > 0)
        {
            input.Consume(GameAction.Jump);
            Character.AirJumpsRemaining--;
            Character.IsFastFalling = false; // a fresh jump cancels fast-fall lock
            Character.CharacterVelocity = new Vector2(
                Character.CharacterVelocity.X,
                Character.Data.AirJumpVelocity);
            FSM.TransitionTo("JumpState");
            return;
        }

        // ── Fast-fall ─────────────────────────────────────────────────────────
        // Only on a deliberate downward FLICK (see InputHandler.IsFastFallFlick),
        // only while already descending, and only once per fall. On trigger we
        // SNAP vertical velocity to the fast-fall speed instantly and latch the flag;
        // the lock persists until the character lands (cleared in PhysicsUpdate).
        if (!Character.IsFastFalling &&
            Character.CharacterVelocity.Y > 0f &&
            input.IsFastFallFlick())
        {
            Character.IsFastFalling = true;
            Character.CharacterVelocity = new Vector2(
                Character.CharacterVelocity.X,
                Character.Data.FastFallMaxSpeed); // instant snap to fast-fall speed
        }

        // ── Aerial attack ─────────────────────────────────────────────────────
        if (input.IsBuffered(GameAction.Attack))
        {
            input.Consume(GameAction.Attack);
            FSM.TransitionTo("AttackState", "attack_type", "NeutralAir");
            return;
        }
    }

    public override void PhysicsUpdate(double delta)
    {
        Character.StateFrameCounter++;

        if (Character.IsOnFloor())
        {
            Character.IsFastFalling     = false;
            Character.AirJumpsRemaining = Character.Data.MaxAirJumps;
            FSM.TransitionTo("IdleState");
            return;
        }

        Character.CharacterVelocity = MovementComponent.ApplyGravity(
            Character.CharacterVelocity, Character.Data, Character.IsFastFalling, delta);

        Character.CharacterVelocity = MovementComponent.ApplyAirMovement(
            Character.CharacterVelocity, Character.Input.MoveStick.X, Character.Data);
    }
}
