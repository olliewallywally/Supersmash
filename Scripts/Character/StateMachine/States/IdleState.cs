using Godot;
using Godot.Collections;

namespace Supersmash;

public partial class IdleState : State
{
    public override void Enter(Dictionary? msg = null)
    {
        Character.IsFastFalling     = false;
        Character.StateFrameCounter = 0;
        Character.AirJumpsRemaining = Character.Data.MaxAirJumps;
    }

    public override void HandleInput(InputHandler input)
    {
        if (input.IsBuffered(GameAction.Jump))
        {
            input.Consume(GameAction.Jump);
            FSM.TransitionTo("JumpSquatState");
            return;
        }

        float moveX = input.MoveStick.X;
        if (Mathf.Abs(moveX) > 0.3f)
        {
            FSM.TransitionTo("RunState");
            return;
        }

        if (input.IsBuffered(GameAction.Attack))
        {
            input.Consume(GameAction.Attack);
            FSM.TransitionTo("AttackState", "attack_type", "Jab");
            return;
        }
    }

    public override void PhysicsUpdate(double delta)
    {
        if (!Character.IsOnFloor())
        {
            FSM.TransitionTo("FallState");
            return;
        }

        // Friction decelerates any residual momentum (e.g. after landing from a dash).
        Character.CharacterVelocity = MovementComponent.ApplyGroundFriction(
            Character.CharacterVelocity, Character.Data);

        // Keep the character snapped to the floor.
        Character.CharacterVelocity = new Vector2(
            Character.CharacterVelocity.X,
            0f);
    }
}
