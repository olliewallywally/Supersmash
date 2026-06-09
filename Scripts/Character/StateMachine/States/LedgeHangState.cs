using Godot;
using Godot.Collections;

namespace Supersmash;

/// <summary>
/// Hanging from a stage ledge. Entered via LedgeZone with the hang position and
/// the direction of the stage interior ("facing").
///
/// ── Intangibility ───────────────────────────────────────────────────────────
/// LedgeHangInvincibilityFrames of FullInvincibility on grab — the standard
/// reward for reaching the ledge. After it expires you hang vulnerable.
///
/// ── Options (after MinHangFrames so a grab can't instantly cancel) ──────────
///   up / toward stage → climb up (LedgeGetupFrames, then Idle on the stage)
///   jump              → leap up off the ledge (air jumps refreshed)
///   away / down       → drop into FallState (keeps your air jumps)
///
/// ── Anti-stall ──────────────────────────────────────────────────────────────
/// MaxHangFrames (10 s) forcibly drops the character. Regrab cooldown is
/// intentionally not enforced yet — LedgeZone grabs only falling characters,
/// which self-limits trivial regrab loops.
/// </summary>
public partial class LedgeHangState : State
{
    private const int MinHangFrames = 8;
    private const int MaxHangFrames = 600;

    private Vector2 _hangPosition;
    private int     _stageDir = 1;   // direction toward the stage interior
    private int     _timer;

    public override void Enter(Dictionary? msg = null)
    {
        _timer = 0;

        if (msg is not null)
        {
            if (msg.TryGetValue("hang_position", out Variant p)) _hangPosition = p.AsVector2();
            if (msg.TryGetValue("facing",        out Variant f)) _stageDir     = f.AsInt32();
        }

        Character.GlobalPosition    = _hangPosition;
        Character.CharacterVelocity = Vector2.Zero;
        Character.IsFastFalling     = false;
        Character.FacingDirection   = _stageDir;
        Character.AirJumpsRemaining = Character.Data.MaxAirJumps;
        Character.StateFrameCounter = 0;

        // Ledge intangibility — handed to the controller's countdown, same
        // mechanism RespawnState uses, so the two never fight over the hurtbox.
        Character.TemporaryInvincibilityFrames = Character.Data.LedgeHangInvincibilityFrames;
        Character.HurtboxContainer.CurrentInvincibility = Hurtbox.InvincibilityType.FullInvincibility;
    }

    public override void HandleInput(InputHandler input)
    {
        if (_timer < MinHangFrames) return;

        // Jump off the ledge.
        if (input.IsBuffered(GameAction.Jump))
        {
            input.Consume(GameAction.Jump);
            Character.CharacterVelocity = new Vector2(
                _stageDir * 150f, Character.Data.FullHopVelocity);
            FSM.TransitionTo("JumpState");
            return;
        }

        Vector2 stick = input.MoveStick;

        // Climb up: up or toward the stage.
        if (stick.Y < -0.5f || (Mathf.Abs(stick.X) > 0.5f && Mathf.Sign(stick.X) == _stageDir))
        {
            ClimbUp();
            return;
        }

        // Drop: down or away from the stage.
        if (stick.Y > 0.5f || (Mathf.Abs(stick.X) > 0.5f && Mathf.Sign(stick.X) == -_stageDir))
        {
            Character.CharacterVelocity = new Vector2(-_stageDir * 80f, 50f);
            FSM.TransitionTo("FallState");
        }
    }

    public override void PhysicsUpdate(double delta)
    {
        _timer++;

        // Pinned to the ledge.
        Character.CharacterVelocity = Vector2.Zero;
        Character.GlobalPosition    = _hangPosition;

        if (_timer >= MaxHangFrames)
        {
            Character.CharacterVelocity = new Vector2(-_stageDir * 80f, 50f);
            FSM.TransitionTo("FallState");
        }
    }

    private void ClimbUp()
    {
        // Teleport onto the stage lip: up and inward past the ledge edge.
        Character.GlobalPosition = _hangPosition + new Vector2(_stageDir * 48f, -72f);
        Character.AnimController?.Play("ledge_climb");
        FSM.TransitionTo("LandingState", "lag_frames", Character.Data.LedgeGetupFrames);
    }
}
