using Godot;
using Godot.Collections;

namespace Supersmash;

/// <summary>
/// Helpless / Freefall — entered after using a special move in the air,
/// being grabbed and thrown off-stage, or certain other out-of-control situations.
///
/// The character falls freely with gravity (no fast-fall, no air drift control
/// beyond passive drift) and CANNOT use any actions until they touch the ground.
/// Air jumps are NOT available from this state.
///
/// This is a key anti-infinite-recovery mechanic: if you burn your special in the
/// air recklessly, you deserve to be punished.
/// </summary>
public partial class HelplessState : State
{
    public override void Enter(Dictionary? msg = null)
    {
        Character.IsFastFalling     = false;
        Character.AirJumpsRemaining = 0; // cannot jump out of helpless
        Character.StateFrameCounter = 0;
    }

    public override void HandleInput(InputHandler input)
    {
        // All inputs are intentionally ignored in helpless state.
        // (Future: airdodge-cancel landing can be implemented here.)
    }

    public override void PhysicsUpdate(double delta)
    {
        Character.StateFrameCounter++;

        if (Character.IsOnFloor())
        {
            // Landing from helpless carries the full punishing landing lag.
            FSM.TransitionTo("LandingState", "lag_frames", Character.Data.HelplessLandingFrames);
            return;
        }

        // Gravity only — no player control over horizontal axis.
        Character.CharacterVelocity = MovementComponent.ApplyGravity(
            Character.CharacterVelocity, Character.Data, isFastFalling: false, delta);

        // No player control: pass zero input so only passive friction applies,
        // bleeding off horizontal momentum without any drift contribution.
        Character.CharacterVelocity = MovementComponent.ApplyAirMovement(
            Character.CharacterVelocity, inputX: 0f, Character.Data);
    }
}
