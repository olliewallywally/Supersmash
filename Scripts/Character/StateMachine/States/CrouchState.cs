using Godot;
using Godot.Collections;

namespace Supersmash;

/// <summary>
/// Grounded crouch: hold down on the stick. The hurtbox shrinks to
/// Data.CrouchHurtboxSize, ducking under high attacks. Releasing down returns to
/// Idle; the usual ground options (jump, attack, shield, grab) remain available
/// and exit crouch directly.
///
/// Down-tilts/down-smashes would branch from here in a fuller moveset; for now a
/// crouching Attack press fires the standard Jab so the state is never a dead end.
/// </summary>
public partial class CrouchState : State
{
    public override void Enter(Dictionary? msg = null)
    {
        Character.StateFrameCounter = 0;
        Character.CharacterVelocity = new Vector2(Character.CharacterVelocity.X, 0f);
        ResizeHurtbox(Character.Data.CrouchHurtboxSize);
    }

    public override void Exit()
    {
        // Restore the standing hurtbox on the way out of every exit path.
        ResizeHurtbox(Character.Data.StandingHurtboxSize);
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

        if (input.IsBuffered(GameAction.Attack))
        {
            input.Consume(GameAction.Attack);
            FSM.TransitionTo("AttackState", "attack_type", "ForwardTilt");
            return;
        }

        // Released the down input → stand back up.
        if (input.MoveStick.Y < 0.5f)
            FSM.TransitionTo("IdleState");
    }

    public override void PhysicsUpdate(double delta)
    {
        if (!Character.IsOnFloor())
        {
            FSM.TransitionTo("FallState");
            return;
        }

        Character.CharacterVelocity = MovementComponent.ApplyGroundFriction(
            Character.CharacterVelocity, Character.Data);
        Character.CharacterVelocity = new Vector2(Character.CharacterVelocity.X, 0f);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private void ResizeHurtbox(Vector2 size)
    {
        var shape = Character.HurtboxContainer.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (shape is null) return;

        // Assign a FRESH shape rather than mutating the existing one: the scene's
        // sub-resource is shared between the hurtbox, the body collider, and every
        // other fighter instance — mutating it would crouch everyone at once.
        shape.Shape = new RectangleShape2D { Size = size };
    }
}
