using Godot;
using Godot.Collections;

namespace Supersmash;

/// <summary>
/// Two-phase respawn: float then drop.
///
/// ── Float phase ────────────────────────────────────────────────────────────
/// The character is teleported to the spawn point, locked in place, and fully
/// intangible for up to DefaultFloatDuration frames (5 s). Any directional
/// stick input or Jump/Attack press instantly exits float — the player chooses
/// when to drop in.
///
/// ── Drop phase ─────────────────────────────────────────────────────────────
/// Normal gravity and air drift apply. The character still has FullInvincibility
/// until they land. On landing, invincibility is handed off to the
/// TemporaryInvincibilityFrames countdown in CharacterController (driven by
/// Data.SpawnInvincibilityFrames), and we transition to IdleState.
///
/// ── Air jumps ──────────────────────────────────────────────────────────────
/// One air jump is available during the drop so the player can adjust their
/// horizontal landing position. Fast-fall is intentionally disabled — the
/// float-drop timing is already the player's expression of intent.
/// </summary>
public partial class RespawnState : State
{
    private const int DefaultFloatDuration = 300; // 5 seconds at 60 Hz

    private Vector2 _spawnPosition;
    private bool    _floating  = true;
    private int     _floatTimer = 0;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void Enter(Dictionary? msg = null)
    {
        _floating   = true;
        _floatTimer = 0;

        if (msg != null && msg.TryGetValue("spawn_position", out Variant posVar))
            _spawnPosition = posVar.AsVector2();

        Character.GlobalPosition     = _spawnPosition;
        Character.CharacterVelocity  = Vector2.Zero;
        Character.IsFastFalling      = false;
        Character.AirJumpsRemaining  = Character.Data.MaxAirJumps;

        Character.HurtboxContainer.CurrentInvincibility = Hurtbox.InvincibilityType.FullInvincibility;
    }

    public override void HandleInput(InputHandler input)
    {
        if (_floating)
        {
            // Directional stick or any action button cancels float and begins the drop.
            bool stickMoved = input.MoveStick.Length() > 0.3f;
            bool buttonPressed = input.IsBuffered(GameAction.Jump) ||
                                 input.IsBuffered(GameAction.Attack) ||
                                 input.IsBuffered(GameAction.Special) ||
                                 input.IsBuffered(GameAction.Shield) ||
                                 input.IsBuffered(GameAction.Dodge);

            if (stickMoved || buttonPressed)
                _floating = false;

            return;
        }

        // ── Drop phase: single air jump to adjust landing position ────────────
        if (input.IsBuffered(GameAction.Jump) && Character.AirJumpsRemaining > 0)
        {
            input.Consume(GameAction.Jump);
            Character.AirJumpsRemaining--;
            Character.IsFastFalling = false;
            Character.CharacterVelocity = new Vector2(
                Character.CharacterVelocity.X,
                Character.Data.AirJumpVelocity);
        }
    }

    public override void PhysicsUpdate(double delta)
    {
        if (_floating)
        {
            // Lock in place — MoveAndSlide will run with zero velocity.
            Character.CharacterVelocity = Vector2.Zero;
            Character.GlobalPosition    = _spawnPosition;

            _floatTimer++;
            if (_floatTimer >= DefaultFloatDuration)
                _floating = false;

            return;
        }

        // ── Drop phase ────────────────────────────────────────────────────────
        Character.CharacterVelocity = MovementComponent.ApplyGravity(
            Character.CharacterVelocity, Character.Data, isFastFalling: false, (float)delta);

        Character.CharacterVelocity = MovementComponent.ApplyAirMovement(
            Character.CharacterVelocity, Character.Input.MoveStick.X, Character.Data);

        if (Character.IsOnFloor())
        {
            // Hand off invincibility to the per-frame countdown; IdleState clears nothing.
            Character.TemporaryInvincibilityFrames = Character.Data.SpawnInvincibilityFrames;
            FSM.TransitionTo("IdleState");
        }
    }

    public override void Exit()
    {
        // Safety: if we exit without landing (e.g. game-over while respawning),
        // clear invincibility so the hurtbox is never permanently inactive.
        if (Character.TemporaryInvincibilityFrames == 0)
            Character.HurtboxContainer.CurrentInvincibility = Hurtbox.InvincibilityType.None;
    }
}
