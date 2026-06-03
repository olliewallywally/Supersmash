using Godot;

namespace Supersmash;

/// <summary>
/// Attach this as a child of a CharacterController to turn it into a training dummy.
/// The dummy uses the same FSM + physics as a real player but its InputHandler.IsAI
/// must be set to true in the inspector so it never reads a physical device.
///
/// Reset lifecycle:
///   Hit confirmed  → HitstunState  (_wasHit latched, any pending reset cancelled)
///   Land in Idle   → countdown starts (ResetDelayFrames, default 60 = 1 second)
///   Countdown zero → position teleport, velocity zero, damage reset, back to Idle
///
/// AutoResetPercent (optional):
///   When > 0, the dummy resets immediately once its damage % reaches this value —
///   useful for finding kill percentages without waiting for a launch to finish.
///
/// ── Init-order note ──────────────────────────────────────────────────────────
/// In Godot 4, _Ready() fires on children BEFORE their parent. So when this
/// component's _Ready() runs, CharacterController._Ready() has NOT yet executed —
/// FSM is null. We use CallDeferred to connect the signal after the entire
/// scene-tree _Ready() pass is complete.
/// </summary>
public partial class TrainingDummyComponent : Node
{
    [ExportGroup("Auto-Reset")]
    /// Enable automatic position + damage reset after being hit and landing.
    [Export] public bool AutoReset { get; set; } = true;

    /// Frames to wait in IdleState after landing before resetting (60 = 1 second).
    [Export] public int ResetDelayFrames { get; set; } = 60;

    /// When > 0, resets immediately as soon as damage % hits this value, without
    /// waiting for a landing. Set to e.g. 120 to find kill thresholds precisely.
    [Export] public float AutoResetPercent { get; set; } = 0f;

    // ── Internal state ────────────────────────────────────────────────────────

    private CharacterController _character = null!;
    private Vector2 _spawnPosition;

    /// -1 = not counting down. ≥0 = frames remaining until reset.
    private int _resetCountdown = -1;

    /// True once the dummy has entered HitstunState at least once since the
    /// last reset — guards against triggering a reset from idle-at-spawn.
    private bool _wasHit = false;

    // ── Godot lifecycle ───────────────────────────────────────────────────────

    public override void _Ready()
    {
        // Defer signal subscription until after CharacterController._Ready() runs
        // and populates the FSM reference.
        CallDeferred(MethodName.LateInit);
    }

    private void LateInit()
    {
        _character     = GetParent<CharacterController>();
        _spawnPosition = _character.GlobalPosition;

        _character.FSM.StateChanged += OnStateChanged;
    }

    public override void _ExitTree()
    {
        // Disconnect to prevent null-ref errors if the node is freed mid-session.
        if (_character?.FSM is not null)
            _character.FSM.StateChanged -= OnStateChanged;
    }

    // ── Per-tick update ───────────────────────────────────────────────────────

    public override void _PhysicsProcess(double delta)
    {
        if (!AutoReset || !_wasHit) return;

        // AutoResetPercent mode: reset the moment damage crosses the threshold,
        // even mid-flight. Useful for testing kill %s at a fixed starting state.
        if (AutoResetPercent > 0f && _character.Combat.DamagePercent >= AutoResetPercent)
        {
            ExecuteReset();
            return;
        }

        if (_resetCountdown < 0) return;

        _resetCountdown--;
        if (_resetCountdown <= 0)
            ExecuteReset();
    }

    // ── FSM signal handler ────────────────────────────────────────────────────

    private void OnStateChanged(string from, string to)
    {
        if (to == "HitstunState")
        {
            _wasHit = true;
            _resetCountdown = -1; // cancel any pending reset; a new hit resets the clock
            return;
        }

        // Start the countdown when the dummy settles back to idle after being hit.
        if (to == "IdleState" && _wasHit && _resetCountdown < 0)
            _resetCountdown = ResetDelayFrames;
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    private void ExecuteReset()
    {
        // Teleport back to spawn. Setting GlobalPosition on a CharacterBody2D is safe
        // in _PhysicsProcess; MoveAndSlide will pick up the new position next tick.
        _character.GlobalPosition      = _spawnPosition;
        _character.CharacterVelocity   = Vector2.Zero;
        _character.IsFastFalling       = false;
        _character.AirJumpsRemaining   = _character.Data.MaxAirJumps;
        _character.Combat.ResetDamage();
        _character.FSM.TransitionTo("IdleState");

        _resetCountdown = -1;
        _wasHit         = false;
    }
}
