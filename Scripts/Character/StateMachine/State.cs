using Godot;
using Godot.Collections;

namespace Supersmash;

/// <summary>
/// Abstract base for all character states. Every concrete state (Idle, Run, Jump, …)
/// extends this and overrides only the methods it needs — the rest are no-ops.
///
/// Lifecycle per state:
///   1. CharacterStateMachine calls Initialize() once at scene load.
///   2. Enter(msg) is called when transitioning INTO this state.
///      msg carries context from the caller (e.g. {"jump_type": "short_hop"}).
///   3. HandleInput() then PhysicsUpdate() are called each physics tick.
///   4. Exit() is called when transitioning OUT of this state.
///
/// To request a transition, call FSM.TransitionTo("StateName") from within
/// HandleInput or PhysicsUpdate. Never call it from Enter or Exit.
/// </summary>
public abstract partial class State : Node
{
    // ── Injected references (set once by CharacterStateMachine._Ready) ────────

    protected CharacterController Character { get; private set; } = null!;
    protected CharacterStateMachine FSM      { get; private set; } = null!;

    /// Called by CharacterStateMachine during its own _Ready before any Enter() calls.
    public void Initialize(CharacterController character, CharacterStateMachine fsm)
    {
        Character = character;
        FSM       = fsm;
    }

    // ── Lifecycle hooks ───────────────────────────────────────────────────────

    /// Called when entering this state. Use to reset internal counters and
    /// apply any one-time velocity changes (e.g., apply jump velocity on enter).
    public virtual void Enter(Dictionary? msg = null) { }

    /// Called when leaving this state. Use to clean up (disable hitboxes, etc.).
    public virtual void Exit() { }

    /// Called each physics tick after HandleInput. Apply velocity changes here.
    /// <param name="delta">Fixed physics timestep in seconds (typically 1/60).</param>
    public virtual void PhysicsUpdate(double delta) { }

    /// Called each physics tick before PhysicsUpdate. Read input and decide
    /// whether to stay in this state or call FSM.TransitionTo().
    public virtual void HandleInput(InputHandler input) { }
}
