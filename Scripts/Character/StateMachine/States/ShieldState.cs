using Godot;
using Godot.Collections;

namespace Supersmash;

/// <summary>
/// Grounded shield: hold the shield button to raise a bubble that absorbs hits.
///
/// ── While held ───────────────────────────────────────────────────────────────
///   • Shield health drains each tick (ShieldComponent.Drain). At 0 it BREAKS:
///     the character drops into a long HitstunState stun (shield-break punish).
///   • Out-of-shield options:
///       – Jump            → JumpSquatState
///       – Grab            → GrabState (the classic "grab out of shield")
///       – Dodge + down    → DodgeState as a spotdodge
///       – Dodge + left/rt → DodgeState as a roll
///
/// ── On release ───────────────────────────────────────────────────────────────
///   Returns to Idle. Regeneration happens in the non-shielding states via
///   ShieldComponent.Regen (driven here only while held).
///
/// If the character scene has no ShieldComponent, this state bounces straight
/// back to Idle so a shield press is simply a no-op for that fighter.
/// </summary>
public partial class ShieldState : State
{
    public override void Enter(Dictionary? msg = null)
    {
        Character.StateFrameCounter = 0;
        Character.CharacterVelocity = new Vector2(Character.CharacterVelocity.X, 0f);

        if (Character.Shield is null)
        {
            // No shield on this fighter — treated as a no-op next tick.
            return;
        }

        if (Character.Shield.IsBroken)
        {
            // Can't raise a broken shield.
            return;
        }

        Character.Shield.Show();
    }

    public override void Exit()
    {
        Character.Shield?.Hide();
    }

    public override void HandleInput(InputHandler input)
    {
        if (Character.Shield is null || Character.Shield.IsBroken)
        {
            FSM.TransitionTo("IdleState");
            return;
        }

        // Jump out of shield.
        if (input.IsBuffered(GameAction.Jump))
        {
            input.Consume(GameAction.Jump);
            FSM.TransitionTo("JumpSquatState");
            return;
        }

        // Grab out of shield.
        if (input.IsBuffered(GameAction.Grab))
        {
            input.Consume(GameAction.Grab);
            FSM.TransitionTo("GrabState");
            return;
        }

        // Roll / spotdodge out of shield.
        if (input.IsBuffered(GameAction.Dodge))
        {
            input.Consume(GameAction.Dodge);
            string kind = Mathf.Abs(input.MoveStick.X) > 0.5f ? "roll" : "spotdodge";
            int dir = input.MoveStick.X >= 0f ? 1 : -1;
            FSM.TransitionTo("DodgeState", new Dictionary
            {
                { "kind", kind },
                { "dir",  dir  },
            });
            return;
        }

        // Released shield → drop it.
        if (!input.IsHeld(GameAction.Shield))
            FSM.TransitionTo("IdleState");
    }

    public override void PhysicsUpdate(double delta)
    {
        if (!Character.IsOnFloor())
        {
            FSM.TransitionTo("FallState");
            return;
        }

        // Pinned in place while shielding.
        Character.CharacterVelocity = MovementComponent.ApplyGroundFriction(
            Character.CharacterVelocity, Character.Data);
        Character.CharacterVelocity = new Vector2(Character.CharacterVelocity.X, 0f);

        if (Character.Shield is null) return;

        bool broke = Character.Shield.Drain();
        if (broke)
            BreakShield();
    }

    private void BreakShield()
    {
        // A broken shield launches the character straight up into a long stun.
        int stun = Character.Data.ShieldBreakStunFrames;
        Character.Shield?.Hide();
        FSM.TransitionTo("HitstunState", new Dictionary
        {
            { "launch_velocity", new Vector2(0f, -300f) },
            { "hitstun_frames",  stun },
            { "hitlag_frames",   0 },
        });
    }
}
