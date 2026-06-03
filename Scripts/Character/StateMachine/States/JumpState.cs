using Godot;
using Godot.Collections;

namespace Supersmash;

/// <summary>
/// The rising arc after JumpSquatState has applied the launch velocity.
///
/// This state's only jobs are:
///   • Apply gravity on the way up.
///   • Allow aerial drift input.
///   • Allow air jumps (double-jump, triple-jump, etc.).
///   • Hand off to FallState once the arc peaks (velocity.Y flips positive).
///
/// Short-hop vs. full-hop detection and momentum carry have already happened
/// in JumpSquatState — by the time we get here the velocity is set.
/// </summary>
public partial class JumpState : State
{
    public override void Enter(Dictionary? msg = null)
    {
        // Velocity already applied by JumpSquatState. Nothing to set up.
        Character.StateFrameCounter = 0;
    }

    public override void HandleInput(InputHandler input)
    {
        // Air jump — instantaneous, no JumpSquat window.
        if (input.IsBuffered(GameAction.Jump) && Character.AirJumpsRemaining > 0)
        {
            input.Consume(GameAction.Jump);
            Character.AirJumpsRemaining--;
            Character.IsFastFalling = false;

            // Preserve horizontal momentum; only override the vertical component.
            Character.CharacterVelocity = new Vector2(
                Character.CharacterVelocity.X,
                Character.Data.AirJumpVelocity);

            // No state change — we stay in JumpState on the new rising arc.
        }

        if (input.IsBuffered(GameAction.Attack))
        {
            input.Consume(GameAction.Attack);
            FSM.TransitionTo("AttackState", "attack_type", "nair");
        }
    }

    public override void PhysicsUpdate(double delta)
    {
        Character.StateFrameCounter++;

        // Extremely rare edge case: land on the same tick as launch (e.g., jumping into ceiling).
        if (Character.IsOnFloor())
        {
            Character.AirJumpsRemaining = Character.Data.MaxAirJumps;
            FSM.TransitionTo("IdleState");
            return;
        }

        // Arc peak — velocity.Y crosses zero, gravity wins from here.
        if (Character.CharacterVelocity.Y >= 0f)
        {
            FSM.TransitionTo("FallState");
            return;
        }

        Character.CharacterVelocity = MovementComponent.ApplyGravity(
            Character.CharacterVelocity, Character.Data, isFastFalling: false, delta);

        Character.CharacterVelocity = MovementComponent.ApplyAirMovement(
            Character.CharacterVelocity, Character.Input.MoveStick.X, Character.Data);
    }
}
