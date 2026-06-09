using Godot;
using Godot.Collections;

namespace Supersmash;

/// <summary>
/// The grabber's side of a grab. Sequence:
///
///   1. Startup (frames 1–6): arms nothing; pure windup.
///   2. Active (frames 7–9): the "GrabBox" Area2D monitors for enemy hurtboxes.
///      Grabs beat shields — this is the whole reason grabs exist — so the target's
///      shield state is irrelevant; only FullInvincibility avoids a grab.
///   3a. Whiff: nothing caught by frame 9 → recovery until frame 30 → Idle.
///   3b. Catch: victim is put in GrabbedState; we enter a hold of up to MaxHoldFrames.
///       During the hold, a stick flick chooses the throw:
///         forward → 45° launch  ·  back → 135°  ·  up → 88°  ·  down → 60° low pop
///       If the victim escapes (mashes out / hold expires) we return to Idle.
///
/// The throw itself reuses the normal hit pipeline (victim.ReceiveHit with a
/// code-built HitboxData), so DI, weight, and knockback scaling all apply.
///
/// Scene requirement: a child Area2D named "GrabBox" with a CollisionShape2D,
/// collision mask set to the hurtbox layer, positioned in front of the character.
/// If absent, every grab simply whiffs (logged once per attempt).
/// </summary>
public partial class GrabState : State
{
    private const int FirstActiveFrame = 7;
    private const int LastActiveFrame  = 9;
    private const int WhiffTotalFrames = 30;
    private const int MaxHoldFrames    = 90;

    private int                  _timer;
    private bool                 _holding;
    private CharacterController? _victim;
    private Area2D?              _grabBox;

    public override void Enter(Dictionary? msg = null)
    {
        _timer   = 0;
        _holding = false;
        _victim  = null;
        _grabBox = Character.FindCombatNode<Area2D>("GrabBox");

        if (_grabBox is null)
            GD.PushWarning($"[GrabState] No 'GrabBox' Area2D on {Character.Name} — grab will whiff.");
        else
            _grabBox.Monitoring = false;

        Character.StateFrameCounter = 0;
        Character.CharacterVelocity = new Vector2(Character.CharacterVelocity.X, 0f);
    }

    public override void Exit()
    {
        if (_grabBox is not null) _grabBox.Monitoring = false;

        // Never leave a victim frozen in GrabbedState if we exit abnormally
        // (e.g. the grabber gets hit out of the hold by a third party).
        if (_victim is not null && GodotObject.IsInstanceValid(_victim) &&
            _victim.FSM.CurrentStateName == "GrabbedState")
        {
            _victim.FSM.TransitionTo("FallState");
        }
        _victim = null;
    }

    public override void HandleInput(InputHandler input)
    {
        if (!_holding) return;

        // Brief gate before throws are read: without it, the stick still held
        // from the dash-in approach instantly auto-throws on the catch frame,
        // removing the deliberate throw choice entirely.
        if (_timer < 6) return;

        // Throw selection by stick direction while holding.
        Vector2 stick = input.MoveStick;
        if (stick.Length() < 0.6f) return;

        if (Mathf.Abs(stick.X) >= Mathf.Abs(stick.Y))
            Throw(forward: Mathf.Sign(stick.X) == Character.FacingDirection);
        else if (stick.Y < 0f)
            ThrowUp();
        else
            ThrowDown();
    }

    public override void PhysicsUpdate(double delta)
    {
        _timer++;

        // Pinned while grabbing — friction only.
        Character.CharacterVelocity = MovementComponent.ApplyGroundFriction(
            Character.CharacterVelocity, Character.Data);
        Character.CharacterVelocity = new Vector2(Character.CharacterVelocity.X, 0f);

        if (_holding)
        {
            // Victim escaped (mash-out or our hold expired)?
            if (_victim is null || !GodotObject.IsInstanceValid(_victim) ||
                _victim.FSM.CurrentStateName != "GrabbedState" ||
                _timer >= MaxHoldFrames)
            {
                ReleaseAndIdle();
            }
            return;
        }

        // ── Catch window ──────────────────────────────────────────────────────
        if (_grabBox is not null)
        {
            // Enable monitoring one frame EARLY: Area2D overlap lists refresh on
            // the physics step, so enabling and querying on the same frame always
            // returns empty — the first active frame would silently never catch.
            if (_timer == FirstActiveFrame - 1) _grabBox.Monitoring = true;

            if (_timer >= FirstActiveFrame && _timer <= LastActiveFrame)
                TryCatch();

            if (_timer == LastActiveFrame + 1) _grabBox.Monitoring = false;
        }

        if (_timer >= WhiffTotalFrames)
            FSM.TransitionTo("IdleState");
    }

    // ── Catch ───────────────────────────────────────────────────────────────────

    private void TryCatch()
    {
        foreach (Area2D area in _grabBox!.GetOverlappingAreas())
        {
            if (area is not Hurtbox hurtbox) continue;
            if (hurtbox.GetParent() is not CharacterController target) continue;
            if (target == Character) continue;

            _victim  = target;
            _holding = true;
            _timer   = 0;
            _grabBox.Monitoring = false;

            target.FSM.TransitionTo("GrabbedState", new Dictionary
            {
                { "holder",          Character    },
                { "max_hold_frames", MaxHoldFrames },
            });
            return;
        }
    }

    // ── Throws ──────────────────────────────────────────────────────────────────
    // Throw payloads are built in code: they're properties of the grab system,
    // not per-attack data (per-character throw data can move into AttackLibrary later).

    private void Throw(bool forward)
    {
        // Back throw: turn around first so the launch mirrors correctly.
        if (!forward) Character.FacingDirection = -Character.FacingDirection;

        ExecuteThrow(new HitboxData
        {
            Damage = 6f, BaseKnockback = 50f, KnockbackGrowth = 70f,
            LaunchAngle = 45f, HitlagFrames = 4, HitstunFrames = 22,
        });
    }

    private void ThrowUp() => ExecuteThrow(new HitboxData
    {
        Damage = 5f, BaseKnockback = 55f, KnockbackGrowth = 75f,
        LaunchAngle = 88f, HitlagFrames = 4, HitstunFrames = 24,
    });

    private void ThrowDown() => ExecuteThrow(new HitboxData
    {
        // Low pop straight up off the ground — the combo-starter throw.
        Damage = 7f, BaseKnockback = 40f, KnockbackGrowth = 40f,
        LaunchAngle = 78f, HitlagFrames = 5, HitstunFrames = 26,
    });

    private void ExecuteThrow(HitboxData data)
    {
        if (_victim is null || !GodotObject.IsInstanceValid(_victim))
        {
            ReleaseAndIdle();
            return;
        }

        var victim = _victim;
        _victim  = null;   // clear BEFORE ReceiveHit so Exit() doesn't double-release
        _holding = false;

        Character.AnimController?.Play("throw");
        victim.ReceiveHit(Character, data);

        FSM.TransitionTo("IdleState");
    }

    private void ReleaseAndIdle()
    {
        if (_victim is not null && GodotObject.IsInstanceValid(_victim) &&
            _victim.FSM.CurrentStateName == "GrabbedState")
        {
            _victim.FSM.TransitionTo("FallState");
        }
        _victim  = null;
        _holding = false;
        FSM.TransitionTo("IdleState");
    }
}
