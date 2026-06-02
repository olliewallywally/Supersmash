using Godot;
using Godot.Collections;
using System.Collections.Generic;

namespace Supersmash;

/// <summary>
/// Manages the character's Finite State Machine.
///
/// Node structure expected in the scene tree:
///   CharacterController (CharacterBody2D)
///   └── StateMachine  ← this node
///       ├── IdleState
///       ├── RunState
///       ├── JumpState
///       ├── FallState
///       ├── AttackState
///       ├── HitstunState
///       └── HelplessState
///
/// States are registered automatically by scanning child nodes. The node's
/// Name in the scene tree is its registration key (e.g. "IdleState").
/// </summary>
public partial class CharacterStateMachine : Node
{
    // ── Configuration ─────────────────────────────────────────────────────────

    /// Name of the state to enter when the character first spawns.
    [Export] private string _initialState = "IdleState";

    // ── State registry ────────────────────────────────────────────────────────

    private readonly Dictionary<string, State> _states = new();
    private State? _currentState;

    // ── Public read-only state ────────────────────────────────────────────────

    public string CurrentStateName { get; private set; } = string.Empty;

    /// Exposes current state for debug overlays / animator queries.
    public State? CurrentState => _currentState;

    // ── Signals ───────────────────────────────────────────────────────────────

    [Signal] public delegate void StateChangedEventHandler(string from, string to);

    // ── Godot lifecycle ───────────────────────────────────────────────────────

    public override void _Ready()
    {
        var character = GetParent<CharacterController>();

        // Collect and initialise all State children before entering the first state.
        foreach (Node child in GetChildren())
        {
            if (child is not State state) continue;
            state.Initialize(character, this);
            _states[child.Name] = state;
        }

        if (_states.Count == 0)
            GD.PushWarning("[FSM] No states found as children of CharacterStateMachine.");

        TransitionTo(_initialState);
    }

    // ── Per-tick update (driven by CharacterController._PhysicsProcess) ───────

    public void HandleInput(InputHandler input) => _currentState?.HandleInput(input);

    public void PhysicsUpdate(double delta) => _currentState?.PhysicsUpdate(delta);

    // ── Transition API ────────────────────────────────────────────────────────

    /// <summary>
    /// Transitions to the state registered under <paramref name="stateName"/>.
    /// Safe to call from within HandleInput or PhysicsUpdate — never from Enter/Exit.
    /// </summary>
    /// <param name="stateName">Exact node name of the target state (e.g. "JumpState").</param>
    /// <param name="msg">Optional context dictionary passed verbatim to Enter().</param>
    public void TransitionTo(string stateName, Dictionary? msg = null)
    {
        if (!_states.TryGetValue(stateName, out State? nextState))
        {
            GD.PushError($"[FSM] Transition failed — state '{stateName}' is not registered.");
            return;
        }

        string previousName = CurrentStateName;

        _currentState?.Exit();
        CurrentStateName = stateName;
        _currentState    = nextState;
        _currentState.Enter(msg);

        EmitSignal(SignalName.StateChanged, previousName, stateName);
    }

    /// Convenience overload for transitions with a single key/value pair.
    public void TransitionTo(string stateName, string key, Variant value) =>
        TransitionTo(stateName, new Dictionary { { key, value } });
}
