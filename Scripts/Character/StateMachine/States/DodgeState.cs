using Godot;
using Godot.Collections;

namespace Supersmash;

/// <summary>
/// Evasive options with intangibility frames. Three variants, chosen by the "kind"
/// message value set by the state that triggered the dodge:
///
///   "spotdodge" — stationary ground dodge. Short, intangible mid-window.
///   "roll"      — ground roll in "dir". Travels a fixed distance, intangible mid-window.
///   "airdodge"  — directional air dodge (Melee-style burst), then drops to Helpless.
///
/// Intangibility is implemented by setting the hurtbox to FullInvincibility during
/// the active window and restoring it to None on exit. Respawn invincibility is
/// never overwritten (dodges can't occur while respawning), so a plain None on exit
/// is safe.
/// </summary>
public partial class DodgeState : State
{
    private string _kind = "spotdodge";
    private int    _dir  = 1;
    private int    _timer;

    // Per-variant tuning (frames + speed).
    private int   _totalFrames;
    private int   _intangStart;
    private int   _intangEnd;
    private float _rollSpeed;
    private Vector2 _airDodgeVelocity;

    public override void Enter(Dictionary? msg = null)
    {
        _timer = 0;
        _kind  = "spotdodge";
        _dir   = Character.FacingDirection;

        if (msg is not null)
        {
            if (msg.TryGetValue("kind", out Variant k)) _kind = k.AsString();
            if (msg.TryGetValue("dir",  out Variant d)) _dir  = d.AsInt32();
        }

        switch (_kind)
        {
            case "roll":
                _totalFrames = 26; _intangStart = 4; _intangEnd = 20;
                _rollSpeed   = Character.Data.RunSpeed * 1.2f;
                Character.AnimController?.Play("roll");
                Character.FacingDirection = _dir; // face roll direction
                break;

            case "airdodge":
                _totalFrames = 28; _intangStart = 3; _intangEnd = 24;
                Vector2 stick = Character.Input.MoveStick;
                Vector2 dirVec = stick.LengthSquared() > 0.04f ? stick.Normalized() : Vector2.Zero;
                _airDodgeVelocity = dirVec * (Character.Data.AirSpeed * 2.2f);
                Character.AnimController?.Play("airdodge");
                break;

            default: // spotdodge
                _totalFrames = 22; _intangStart = 3; _intangEnd = 18;
                _rollSpeed   = 0f;
                Character.AnimController?.Play("spotdodge");
                break;
        }
    }

    public override void Exit()
    {
        // Lift dodge intangibility (never clobber respawn invincibility — see class doc).
        if (Character.TemporaryInvincibilityFrames <= 0)
            Character.HurtboxContainer.CurrentInvincibility = Hurtbox.InvincibilityType.None;
    }

    public override void PhysicsUpdate(double delta)
    {
        _timer++;

        // Toggle intangibility window.
        bool intangible = _timer >= _intangStart && _timer <= _intangEnd;
        if (Character.TemporaryInvincibilityFrames <= 0)
        {
            Character.HurtboxContainer.CurrentInvincibility = intangible
                ? Hurtbox.InvincibilityType.FullInvincibility
                : Hurtbox.InvincibilityType.None;
        }

        if (_kind == "airdodge")
        {
            // Apply the one-time burst then let it bleed off; gravity resumes after.
            Character.CharacterVelocity = _timer == 1
                ? _airDodgeVelocity
                : MovementComponent.ApplyGravity(
                      Character.CharacterVelocity.Lerp(Vector2.Zero, 0.08f),
                      Character.Data, isFastFalling: false, delta);

            if (Character.IsOnFloor())
            {
                FSM.TransitionTo("LandingState");
                return;
            }
            if (_timer >= _totalFrames)
            {
                FSM.TransitionTo("HelplessState");
                return;
            }
            return;
        }

        // Ground roll / spotdodge.
        if (!Character.IsOnFloor())
        {
            FSM.TransitionTo("FallState");
            return;
        }

        float vx = _kind == "roll" ? _dir * _rollSpeed : 0f;
        // Ease the roll to a stop in its back third.
        if (_kind == "roll" && _timer > _totalFrames * 2 / 3)
            vx = Mathf.Lerp(vx, 0f, 0.4f);

        Character.CharacterVelocity = new Vector2(vx, 0f);

        if (_timer >= _totalFrames)
            FSM.TransitionTo("IdleState");
    }
}
