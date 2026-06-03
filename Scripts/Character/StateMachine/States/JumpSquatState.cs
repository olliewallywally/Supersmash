using Godot;
using Godot.Collections;

namespace Supersmash;

/// <summary>
/// The 3-frame grounded anticipation window between pressing jump and leaving the floor.
///
/// Responsibilities:
///   1. Count JumpSquatFrames ticks while keeping the character pinned to the ground.
///   2. Detect short-hop intent (jump button released before the window closes).
///   3. Compute and apply the launch velocity — vertical (short/full) and horizontal
///      (momentum carry from any run speed) — then hand off to JumpState.
///
/// Air jumps do NOT go through this state. They are instantaneous and live in FallState.
///
/// ── Why this is its own state (not a sub-phase of JumpState) ─────────────────────
/// JumpSquat has unique logic that nothing airborne needs:
///   • The character is still grounded and can be grabbed/hit during these frames.
///   • The short/full-hop decision is made here and is invisible to the rising arc.
///   • Momentum transfer math belongs to the moment of launch, not the arc itself.
///   Keeping it separate makes each state's contract simple and testable in isolation.
/// </summary>
public partial class JumpSquatState : State
{
    private int  _timer    = 0;
    private bool _shortHop = false;

    public override void Enter(Dictionary? msg = null)
    {
        _timer    = 0;
        _shortHop = false; // assume full hop; downgraded to short hop if button is released
        Character.IsFastFalling     = false;
        Character.StateFrameCounter = 0;
    }

    public override void HandleInput(InputHandler input)
    {
        // Short-hop detection: the instant the jump button is released during the squat,
        // lock in a short hop. The flag is one-way — re-pressing jump won't undo it.
        if (!input.IsHeld(GameAction.Jump))
            _shortHop = true;
    }

    public override void PhysicsUpdate(double delta)
    {
        _timer++;
        Character.StateFrameCounter++;

        // Stay grounded: zero Y velocity so MoveAndSlide doesn't drift us off ledges.
        Character.CharacterVelocity = new Vector2(Character.CharacterVelocity.X, 0f);

        if (_timer >= Character.Data.JumpSquatFrames)
            Launch();
    }

    // ── Launch ────────────────────────────────────────────────────────────────

    private void Launch()
    {
        CharacterData data = Character.Data;

        // ── 1. Vertical velocity ──────────────────────────────────────────────
        //
        // Short hop  → shallower arc, released during squat.
        // Full hop   → maximum height, button held through all JumpSquatFrames.
        float launchY = _shortHop ? data.ShortHopVelocity : data.FullHopVelocity;

        // ── 2. Horizontal momentum carry ──────────────────────────────────────
        //
        // Formula:  launchX = groundVelocity × JumpMomentumTransfer
        //
        // JumpMomentumTransfer (0–1) is the single tuning knob that sets the game's
        // movement feel on this axis:
        //
        //   1.0  →  Melee-like: a full-speed dash-jump carries 100% of run speed
        //           into the air. Rewards dash-dancing and aerial momentum play.
        //           Creates the "flowing" feel where ground and air speed share budget.
        //
        //   0.6  →  Ultimate-like: 60% carry. You still gain meaningful horizontal
        //           distance from a running jump, but it blunts the hard edge of
        //           full-speed approach aerials.
        //
        //   0.0  →  Floaty / platform-puzzle style: jumping always goes straight up
        //           regardless of ground speed. Very forgiving, low skill ceiling.
        //
        // MaxHorizontalJumpSpeed is a hard ceiling so characters with very high run
        // speeds can't trivially exceed the air speed budget at launch.
        float launchX = Character.CharacterVelocity.X * data.JumpMomentumTransfer;
        launchX = Mathf.Clamp(launchX, -data.MaxHorizontalJumpSpeed, data.MaxHorizontalJumpSpeed);

        Character.CharacterVelocity = new Vector2(launchX, launchY);

        FSM.TransitionTo("JumpState");
    }
}
