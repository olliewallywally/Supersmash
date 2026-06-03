using Godot;
using Godot.Collections;

namespace Supersmash;

public partial class RunState : State
{
    public override void Enter(Dictionary? msg = null)
    {
        Character.StateFrameCounter = 0;
    }

    public override void HandleInput(InputHandler input)
    {
        if (input.IsBuffered(GameAction.Jump))
        {
            input.Consume(GameAction.Jump);
            FSM.TransitionTo("JumpSquatState");
            return;
        }

        if (input.IsBuffered(GameAction.Attack))
        {
            input.Consume(GameAction.Attack);
            // Smash attack if strong input, tilt otherwise — placeholder logic.
            string type = Mathf.Abs(input.MoveStick.X) > 0.85f ? "ForwardSmash" : "ForwardTilt";
            FSM.TransitionTo("AttackState", "attack_type", type);
            return;
        }

        if (Mathf.Abs(input.MoveStick.X) < 0.15f)
        {
            FSM.TransitionTo("IdleState");
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

        float inputX = Character.Input.MoveStick.X;

        // Update facing direction.
        if (Mathf.Abs(inputX) > 0.1f)
            Character.FacingDirection = Mathf.Sign(inputX);

        Character.CharacterVelocity = MovementComponent.ApplyGroundAcceleration(
            Character.CharacterVelocity, inputX, Character.Data);

        Character.CharacterVelocity = new Vector2(
            Character.CharacterVelocity.X,
            0f);

        Character.StateFrameCounter++;
    }
}
