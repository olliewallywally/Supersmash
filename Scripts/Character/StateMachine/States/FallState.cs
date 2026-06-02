using Godot;
using Godot.Collections;

namespace Supersmash;

public partial class FallState : State
{
    public override void Enter(Dictionary? msg = null)
    {
        Character.StateFrameCounter = 0;
    }

    public override void HandleInput(InputHandler input)
    {
        // Fast fall: tap down while falling (not just holding).
        if (!Character.IsFastFalling &&
            Character.CharacterVelocity.Y > 0 &&
            input.IsJustPressed(GameAction.Dodge) == false && // not a dodge
            input.MoveStick.Y > 0.65f)
        {
            // Use IsJustPressed with a 1-frame buffer for tap detection.
            // We check the stick magnitude crossed the threshold this frame.
            Character.IsFastFalling = true;
        }

        // Air jump.
        if (input.IsBuffered(GameAction.Jump) && Character.AirJumpsRemaining > 0)
        {
            input.Consume(GameAction.Jump);
            Character.AirJumpsRemaining--;
            Character.IsFastFalling = false;
            Character.CharacterVelocity = new Vector2(
                Character.CharacterVelocity.X,
                Character.Data.AirJumpVelocity);
            // Stay in FallState; jump arc will be governed by gravity here.
            return;
        }

        if (input.IsBuffered(GameAction.Attack))
        {
            input.Consume(GameAction.Attack);
            FSM.TransitionTo("AttackState", "attack_type", "nair");
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

        float inputX = Character.Input.MoveStick.X;
        if (Mathf.Abs(inputX) > 0.1f)
        {
            Character.CharacterVelocity = MovementComponent.ApplyAirDrift(
                Character.CharacterVelocity, inputX, Character.Data);
        }
        else
        {
            Character.CharacterVelocity = MovementComponent.ApplyAirFriction(
                Character.CharacterVelocity, Character.Data);
        }
    }
}
