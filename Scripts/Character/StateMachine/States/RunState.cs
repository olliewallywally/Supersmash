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

        if (input.IsBuffered(GameAction.Shield))
        {
            FSM.TransitionTo("ShieldState");
            return;
        }

        if (input.IsBuffered(GameAction.Grab))
        {
            input.Consume(GameAction.Grab);
            FSM.TransitionTo("GrabState");
            return;
        }

        if (input.IsBuffered(GameAction.Dodge))
        {
            input.Consume(GameAction.Dodge);
            // Dodging while running is always a roll, in the direction of travel.
            int dir = Character.CharacterVelocity.X >= 0f ? 1 : -1;
            FSM.TransitionTo("DodgeState", new Dictionary { { "kind", "roll" }, { "dir", dir } });
            return;
        }

        if (input.IsBuffered(GameAction.Special))
        {
            input.Consume(GameAction.Special);
            if (Character.Attacks?.Get("NeutralSpecial") is not null)
            {
                FSM.TransitionTo("AttackState", "attack_type", "NeutralSpecial");
                return;
            }
        }

        if (input.IsBuffered(GameAction.Attack))
        {
            input.Consume(GameAction.Attack);
            FSM.TransitionTo("AttackState", "attack_type",
                AttackState.PickGrounded(Character, input.MoveStick));
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

        // Footstep every 12 frames (~5 steps/sec at run cadence).
        if (Character.StateFrameCounter % 12 == 0)
            AudioManager.PlayFootstep(Character.GetTree(), Character.StateFrameCounter / 12);
    }
}
