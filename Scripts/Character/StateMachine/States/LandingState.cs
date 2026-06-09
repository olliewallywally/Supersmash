using Godot;
using Godot.Collections;

namespace Supersmash;

/// <summary>
/// Brief grounded recovery window after touching down. The number of lag frames
/// comes from the "lag_frames" message value; callers choose the right amount:
///
///   FallState / JumpState   → Data.SoftLandingFrames     (2 — barely noticeable)
///   HelplessState           → Data.HelplessLandingFrames (20 — the punish window)
///   Aerial AttackState      → aerial landing lag (with LCancelMultiplier applied
///                             when the L-cancel timing was met — future extension)
///   Air dodge (DodgeState)  → fixed 10-frame skid
///
/// No actions are available during landing lag — that's the entire point of it.
/// Inputs pressed during the lag stay in the ring buffer and fire on the first
/// frame of IdleState, so a buffered jump still comes out frame-perfectly.
/// </summary>
public partial class LandingState : State
{
    private int _lagFrames;
    private int _timer;

    public override void Enter(Dictionary? msg = null)
    {
        _timer     = 0;
        _lagFrames = Character.Data.SoftLandingFrames;

        if (msg is not null && msg.TryGetValue("lag_frames", out Variant v))
            _lagFrames = v.AsInt32();

        Character.IsFastFalling     = false;
        Character.AirJumpsRemaining = Character.Data.MaxAirJumps;
        Character.StateFrameCounter = 0;
    }

    public override void PhysicsUpdate(double delta)
    {
        _timer++;

        // Skid: residual horizontal momentum bleeds off under ground friction.
        Character.CharacterVelocity = MovementComponent.ApplyGroundFriction(
            Character.CharacterVelocity, Character.Data);
        Character.CharacterVelocity = new Vector2(Character.CharacterVelocity.X, 0f);

        if (_timer >= _lagFrames)
            FSM.TransitionTo("IdleState");
    }
}
