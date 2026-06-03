using Godot;
using System;

namespace Supersmash;

// ─────────────────────────────────────────────────────────────────────────────
// Enumerations
// ─────────────────────────────────────────────────────────────────────────────

public enum GameAction
{
    Jump = 0,
    Attack,
    Special,
    Shield,
    Grab,
    Dodge,
    // !! Count must remain last — it sizes the per-action arrays below. !!
    Count,
}

[Flags]
public enum ButtonMask
{
    None    = 0,
    Jump    = 1 << 0,
    Attack  = 1 << 1,
    Special = 1 << 2,
    Shield  = 1 << 3,
    Grab    = 1 << 4,
    Dodge   = 1 << 5,
}

// ─────────────────────────────────────────────────────────────────────────────
// InputFrame
// A snapshot of all inputs captured at a single physics tick.
// Stored in the ring buffer so states can inspect recent history.
// ─────────────────────────────────────────────────────────────────────────────

public struct InputFrame
{
    /// Left-stick / WASD movement axis. Normalised to [–1, 1].
    public Vector2 MoveStick;
    /// Right-stick / C-stick for smash inputs and aerial aerials.
    public Vector2 CStick;
    /// Bitmask of all buttons held this tick.
    public ButtonMask Buttons;
    /// Engine physics tick counter at capture time. Used for buffer-window math.
    public int PhysicsFrame;
}

// ─────────────────────────────────────────────────────────────────────────────
// InputHandler
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Decoupled input layer. Runs once per physics tick (called by CharacterController).
/// Maintains a ring buffer of InputFrames and exposes buffered-press queries so
/// states never need to touch Godot's Input singleton directly.
///
/// Buffered inputs are essential for platform fighters: a jump pressed 4 frames
/// before the character lands should still register as a jump-on-landing.
/// </summary>
public partial class InputHandler : Node
{
    // ── Configuration ────────────────────────────────────────────────────────

    /// Size of the ring buffer in ticks. 60 = one full second of history.
    private const int BufferSize = 60;

    /// Default leniency window (in ticks) for buffered presses. 6 frames ≈ 100 ms.
    public const int DefaultBufferWindow = 6;

    // ── State ─────────────────────────────────────────────────────────────────

    /// Which local player this handler polls. Set before adding to scene tree.
    [Export] public int PlayerIndex { get; set; } = 0;

    /// When true, SampleFrame() writes a fully-zeroed frame every tick without
    /// querying Godot's Input singleton. Use this for AI characters, training
    /// dummies, or replay playback — anything that should not read a physical device.
    /// Avoids the "nonexistent InputMap action" spam that PlayerIndex alone causes
    /// when p1_* actions are not registered in project.godot.
    [Export] public bool IsAI { get; set; } = false;

    private readonly InputFrame[] _buffer = new InputFrame[BufferSize];
    private int _head = 0; // current write position in the ring

    // Per-action timestamps (in physics ticks, matching Engine.GetPhysicsFrames()).
    // Negative sentinel ensures nothing fires on first frame.
    private readonly int[] _lastPressedFrame  = new int[(int)GameAction.Count];
    private readonly int[] _lastConsumedFrame = new int[(int)GameAction.Count];

    private ButtonMask _previousButtons = ButtonMask.None;

    // ── Accessors ─────────────────────────────────────────────────────────────

    /// The InputFrame captured this physics tick.
    public ref InputFrame CurrentFrame => ref _buffer[_head];

    public override void _Ready()
    {
        Array.Fill(_lastPressedFrame,  int.MinValue / 2);
        Array.Fill(_lastConsumedFrame, int.MinValue / 2);
    }

    // ── Core API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Called once at the start of every physics tick by CharacterController,
    /// BEFORE the state machine is updated.
    /// </summary>
    public void SampleFrame()
    {
        // AI/dummy path: write a zero frame without touching Godot's Input singleton.
        if (IsAI)
        {
            _head = (_head + 1) % BufferSize;
            _buffer[_head] = new InputFrame { PhysicsFrame = CurrentTick };
            _previousButtons = ButtonMask.None;
            return;
        }

        int tick = CurrentTick;

        ButtonMask current    = BuildButtonMask();
        ButtonMask justPressed = current & ~_previousButtons;

        // Record the tick each action was first pressed this edge.
        for (int i = 0; i < (int)GameAction.Count; i++)
        {
            if ((justPressed & ActionToBit((GameAction)i)) != ButtonMask.None)
                _lastPressedFrame[i] = tick;
        }

        // Advance ring buffer head.
        _head = (_head + 1) % BufferSize;

        _buffer[_head] = new InputFrame
        {
            MoveStick    = ReadMoveStick(),
            CStick       = ReadCStick(),
            Buttons      = current,
            PhysicsFrame = tick,
        };

        _previousButtons = current;
    }

    /// <summary>
    /// Returns true if <paramref name="action"/> was pressed within the last
    /// <paramref name="window"/> ticks AND has not already been consumed.
    /// Use this for jump buffering, attack buffering, etc.
    /// </summary>
    public bool IsBuffered(GameAction action, int window = DefaultBufferWindow)
    {
        int pressed  = _lastPressedFrame[(int)action];
        int consumed = _lastConsumedFrame[(int)action];
        return (CurrentTick - pressed) <= window && consumed != pressed;
    }

    /// <summary>
    /// Marks the buffered press as consumed so it cannot trigger a second action.
    /// Always call this when you act on a buffered input.
    /// </summary>
    public void Consume(GameAction action)
    {
        _lastConsumedFrame[(int)action] = _lastPressedFrame[(int)action];
    }

    /// Returns true if <paramref name="action"/> is physically held this tick.
    public bool IsHeld(GameAction action) =>
        (CurrentFrame.Buttons & ActionToBit(action)) != ButtonMask.None;

    /// Returns true if <paramref name="action"/> was pressed on exactly this tick (no leniency).
    public bool IsJustPressed(GameAction action) => IsBuffered(action, 0);

    /// Returns the movement stick vector for this tick.
    public Vector2 MoveStick => CurrentFrame.MoveStick;

    /// Returns the C-stick vector for this tick.
    public Vector2 CStick => CurrentFrame.CStick;

    /// The InputFrame captured on the PREVIOUS physics tick. Used for edge detection.
    private ref InputFrame PreviousFrame => ref _buffer[(_head - 1 + BufferSize) % BufferSize];

    /// <summary>
    /// Detects a deliberate downward FLICK of the movement stick — the signature of a
    /// fast-fall input — as opposed to the stick simply being held down.
    ///
    /// The distinction is pure edge detection with hysteresis:
    ///   • Trigger only on the tick the down-axis CROSSES UP through <paramref name="engage"/>,
    ///     having been below <paramref name="release"/> on the previous tick.
    ///   • Because it requires that rising edge, a stick already held down (prev ≥ release)
    ///     never re-triggers. To fast-fall again you must return the stick toward neutral
    ///     and flick down a second time.
    ///
    /// This is exactly why holding down to buffer a down-air does NOT accidentally
    /// fast-fall: a held input has no rising edge. The two gates (engage high, release
    /// low) give a dead-band so a slightly-jittery stick can't false-trigger either.
    /// Godot's Y-axis points down, so a positive Y is "down".
    /// </summary>
    public bool IsFastFallFlick(float engage = 0.65f, float release = 0.35f) =>
        PreviousFrame.MoveStick.Y < release && CurrentFrame.MoveStick.Y >= engage;

    // ── Private helpers ───────────────────────────────────────────────────────

    private static int CurrentTick => (int)Engine.GetPhysicsFrames();

    private ButtonMask BuildButtonMask()
    {
        string p = $"p{PlayerIndex}_";
        ButtonMask m = ButtonMask.None;
        if (Input.IsActionPressed(p + "jump"))    m |= ButtonMask.Jump;
        if (Input.IsActionPressed(p + "attack"))  m |= ButtonMask.Attack;
        if (Input.IsActionPressed(p + "special")) m |= ButtonMask.Special;
        if (Input.IsActionPressed(p + "shield"))  m |= ButtonMask.Shield;
        if (Input.IsActionPressed(p + "grab"))    m |= ButtonMask.Grab;
        if (Input.IsActionPressed(p + "dodge"))   m |= ButtonMask.Dodge;
        return m;
    }

    private Vector2 ReadMoveStick()
    {
        string p = $"p{PlayerIndex}_";
        return new Vector2(
            Input.GetAxis(p + "left", p + "right"),
            Input.GetAxis(p + "up",   p + "down")
        );
    }

    private Vector2 ReadCStick()
    {
        string p = $"p{PlayerIndex}_";
        return new Vector2(
            Input.GetAxis(p + "c_left",  p + "c_right"),
            Input.GetAxis(p + "c_up",    p + "c_down")
        );
    }

    private static ButtonMask ActionToBit(GameAction action) => action switch
    {
        GameAction.Jump    => ButtonMask.Jump,
        GameAction.Attack  => ButtonMask.Attack,
        GameAction.Special => ButtonMask.Special,
        GameAction.Shield  => ButtonMask.Shield,
        GameAction.Grab    => ButtonMask.Grab,
        GameAction.Dodge   => ButtonMask.Dodge,
        _                  => ButtonMask.None,
    };
}
