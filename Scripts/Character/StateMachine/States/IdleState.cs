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
            string kind = Mathf.Abs(input.MoveStick.X) > 0.5f ? "roll" : "spotdodge";
            int dir = input.MoveStick.X >= 0f ? 1 : -1;
            FSM.TransitionTo("DodgeState", new Dictionary { { "kind", kind }, { "dir", dir } });
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

        float moveX = input.MoveStick.X;
        float moveY = input.MoveStick.Y;

        // Hard horizontal flick → dash (dash-dance entry); soft tilt → run.
        if (Mathf.Abs(moveX) > DashState.DashThreshold)
        {
            FSM.TransitionTo("DashState", "dir", Mathf.Sign(moveX));
            return;
        }
        if (Mathf.Abs(moveX) > 0.3f)
        {
            FSM.TransitionTo("RunState");
            return;
        }

        // Held down (without a horizontal component) → crouch.
        if (moveY > 0.6f)
        {
            FSM.TransitionTo("CrouchState");
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
