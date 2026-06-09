using Godot;
using Godot.Collections;

namespace Supersmash;

/// <summary>
/// The victim's side of a grab. Position is pinned in front of the holder every
/// tick; no movement or actions are possible except MASHING OUT: every fresh
/// button press shaves EscapeFramesPerPress off the remaining hold time, so
/// furious mashing escapes a low-percent grab before any throw comes out.
///
/// Exit paths:
///   • Thrown   — the holder's GrabState calls ReceiveHit, which transitions us
///                to HitstunState directly (this state simply gets Exit()ed).
///   • Mash-out — remaining frames hit zero → FallState (brief tumble out).
///   • Holder lost the grab (hit by third party, died) → FallState.
/// </summary>
public partial class GrabbedState : State
{
    private const int EscapeFramesPerPress = 6;

    private CharacterController? _holder;
    private int _remainingFrames;

    public override void Enter(Dictionary? msg = null)
    {
        _holder          = null;
        _remainingFrames = 90;

        if (msg is not null)
        {
            if (msg.TryGetValue("holder", out Variant h))
                _holder = h.As<CharacterController>();
            if (msg.TryGetValue("max_hold_frames", out Variant m))
                _remainingFrames = m.AsInt32();
        }

        Character.CharacterVelocity = Vector2.Zero;
        Character.IsFastFalling     = false;
        Character.StateFrameCounter = 0;

        // Face the holder — you're held by the collar, after all.
        if (_holder is not null)
            Character.FacingDirection = -_holder.FacingDirection;
    }

    public override void HandleInput(InputHandler input)
    {
        // Mash-out: every distinct fresh press counts.
        for (int i = 0; i < (int)GameAction.Count; i++)
        {
            var action = (GameAction)i;
            if (input.IsJustPressed(action))
            {
                input.Consume(action);
                _remainingFrames -= EscapeFramesPerPress;
            }
        }
    }

    public override void PhysicsUpdate(double delta)
    {
        _remainingFrames--;

        // Holder gone or no longer grabbing? Drop free.
        if (_holder is null || !GodotObject.IsInstanceValid(_holder) ||
            _holder.FSM.CurrentStateName != "GrabState")
        {
            FSM.TransitionTo(Character.IsOnFloor() ? "IdleState" : "FallState");
            return;
        }

        // Mashed out before the throw.
        if (_remainingFrames <= 0)
        {
            // Small hop away from the holder so the two bodies separate cleanly.
            Character.CharacterVelocity = new Vector2(
                -_holder.FacingDirection * 200f, -250f);
            FSM.TransitionTo("FallState");
            return;
        }

        // Pinned in the holder's hands, just in front of them.
        Character.CharacterVelocity = Vector2.Zero;
        Character.GlobalPosition = _holder.GlobalPosition
            + new Vector2(_holder.FacingDirection * 42f, 0f);
    }
}
