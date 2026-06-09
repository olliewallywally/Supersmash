using Godot;
using Godot.Collections;

namespace Supersmash;

/// <summary>
/// The initial dash burst — and with it, the dash-dance.
///
/// ── Phases ───────────────────────────────────────────────────────────────────
///   Frames [1 .. DashDanceWindowFrames]: reversal window. Flicking the stick the
///     OPPOSITE way restarts the dash in the new direction (this state re-enters
///     itself) — chaining these is dash-dancing, the core neutral-game movement.
///   Frames [1 .. InitialDashFrames]: the burst itself. Velocity is pinned at
///     InitialDashSpeed in the dash direction.
///   After InitialDashFrames: if the stick is still held toward the dash, settle
///     into RunState; if neutral, skid into IdleState.
///
/// Entered from IdleState on a hard stick flick (|x| > DashThreshold), as opposed
/// to the soft tilt that walks into RunState.
/// </summary>
public partial class DashState : State
{
    /// Stick magnitude required to trigger a dash rather than a walk/run.
    public const float DashThreshold = 0.75f;

    private int _dir;
    private int _timer;

    public override void Enter(Dictionary? msg = null)
    {
        _timer = 0;
        _dir   = Character.FacingDirection;

        if (msg is not null && msg.TryGetValue("dir", out Variant d))
            _dir = d.AsInt32();

        Character.FacingDirection   = _dir;
        Character.StateFrameCounter = 0;
        Character.CharacterVelocity = new Vector2(
            Character.Data.InitialDashSpeed * _dir, 0f);
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
            // Dash attack would go here; ForwardTilt keeps the option useful for now.
            FSM.TransitionTo("AttackState", "attack_type", "ForwardTilt");
            return;
        }

        if (input.IsBuffered(GameAction.Grab))
        {
            input.Consume(GameAction.Grab);
            FSM.TransitionTo("GrabState");
            return;
        }

        // ── Dash-dance reversal ───────────────────────────────────────────────
        // A hard flick the other way inside the window restarts the dash.
        if (_timer <= Character.Data.DashDanceWindowFrames &&
            Mathf.Abs(input.MoveStick.X) > DashThreshold &&
            Mathf.Sign(input.MoveStick.X) == -_dir)
        {
            FSM.TransitionTo("DashState", "dir", -_dir);
        }
    }

    public override void PhysicsUpdate(double delta)
    {
        if (!Character.IsOnFloor())
        {
            FSM.TransitionTo("FallState");
            return;
        }

        _timer++;
        Character.StateFrameCounter++;

        // Burst phase: velocity pinned at dash speed.
        if (_timer <= Character.Data.InitialDashFrames)
        {
            Character.CharacterVelocity = new Vector2(
                Character.Data.InitialDashSpeed * _dir, 0f);
            return;
        }

        // Burst over — settle into run or skid to idle based on the stick.
        float stickX = Character.Input.MoveStick.X;
        if (Mathf.Abs(stickX) > 0.3f && Mathf.Sign(stickX) == _dir)
            FSM.TransitionTo("RunState");
        else
            FSM.TransitionTo("IdleState");
    }
}
