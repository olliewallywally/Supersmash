using Godot;
using System.Collections.Generic;

namespace Supersmash;

/// <summary>
/// Drives AnimationPlayer and visual facing based on FSM state changes.
/// Completely decoupled from physics: states know nothing about this component.
///
/// ── Why states don't call animations directly ─────────────────────────────
/// If animation calls lived in PhysicsUpdate/Enter, every state would import
/// a rendering type, breaking in headless servers and making states harder to
/// unit-test. Instead, this component subscribes to FSM.StateChanged and reacts.
/// Physics stays pure; rendering stays separate.
///
/// ── Required scene structure ──────────────────────────────────────────────
///   CharacterController (CharacterBody2D)
///   ├── VisualRoot (Node2D)          ← export this; scale-flipped for facing
///   │   ├── Sprite2D / Mesh
///   │   └── AnimationPlayer          ← export this
///   ├── AnimationController (Node)   ← this script
///   ├── HurtboxContainer (Hurtbox)   ← NOT under VisualRoot (must not flip)
///   └── JabHitbox (Hitbox)           ← NOT under VisualRoot
///
/// Hitboxes must be direct children of CharacterController (or a non-visual
/// subtree). VisualRoot.Scale.X = –1 mirrors the sprite without moving boxes.
///
/// ── Animation naming convention ───────────────────────────────────────────
///   State animations : "idle", "run", "jumpsquat", "jump_rise", "fall",
///                      "hitstun", "helpless"
///   Attack animations: "attack_" + AttackId  →  "attack_Jab", "attack_NeutralAir"
///
/// ── AnimationPlayer settings (set in Godot inspector) ────────────────────
///   • Process Mode → Physics   (ensures 1 anim tick = 1 physics tick)
///   • Speed Scale  → 1.0
/// </summary>
public partial class AnimationController : Node
{
    // ── Inspector exports ─────────────────────────────────────────────────────

    [Export] private AnimationPlayer? _animationPlayer;

    /// Scale-flipped each tick to reflect FacingDirection changes.
    /// Must NOT be an ancestor of hitbox/hurtbox nodes.
    [Export] private Node2D? _visualRoot;

    /// Animation played when a state has no registered mapping and the requested
    /// animation does not exist.
    [Export] private string _fallbackAnimation { get; set; } = "idle";

    // ── State → animation name table ─────────────────────────────────────────
    // Protected so character-specific subclasses can override entries in _Ready.

    protected readonly Dictionary<string, string> StateAnimations = new()
    {
        { "IdleState",      "idle"      },
        { "RunState",       "run"       },
        { "JumpSquatState", "jumpsquat" },
        { "JumpState",      "jump_rise" },
        { "FallState",      "fall"      },
        { "HitstunState",   "hitstun"   },
        { "HelplessState",  "helpless"  },
        { "AttackState",    ""          }, // resolved per-attack in OnStateChanged
    };

    // ── Internal ──────────────────────────────────────────────────────────────

    private CharacterController _character = null!;
    private int _lastFacingDirection = 1;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        // Defer until CharacterController._Ready() has populated the FSM reference.
        CallDeferred(MethodName.LateInit);
    }

    private void LateInit()
    {
        _character           = GetParent<CharacterController>();
        _lastFacingDirection = _character.FacingDirection;
        _character.FSM.StateChanged += OnStateChanged;
    }

    public override void _ExitTree()
    {
        if (_character?.FSM is not null)
            _character.FSM.StateChanged -= OnStateChanged;
    }

    // ── Facing direction (per-tick) ───────────────────────────────────────────

    public override void _PhysicsProcess(double delta)
    {
        if (_visualRoot is null || _character is null) return;

        if (_character.FacingDirection == _lastFacingDirection) return;

        _lastFacingDirection = _character.FacingDirection;
        // Scale.X = –1 mirrors the sprite/mesh for left-facing without touching
        // hitbox positions (boxes are siblings, not children, of VisualRoot).
        _visualRoot.Scale = new Vector2(_character.FacingDirection, 1f);
    }

    // ── FSM signal handler ────────────────────────────────────────────────────

    private void OnStateChanged(string from, string to)
    {
        if (_animationPlayer is null) return;

        if (to == "AttackState" && _character.FSM.CurrentState is AttackState attackState)
        {
            // Attack animations are keyed "attack_<AttackId>" so each move
            // can have its own authored timing without extra lookup tables.
            Play("attack_" + attackState.CurrentAttackId);
            return;
        }

        if (StateAnimations.TryGetValue(to, out string? anim) && !string.IsNullOrEmpty(anim))
            Play(anim);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// Play an animation by name. Falls back to _fallbackAnimation if not found.
    public void Play(string animName)
    {
        if (_animationPlayer is null) return;

        if (_animationPlayer.HasAnimation(animName))
        {
            _animationPlayer.Play(animName);
            return;
        }

        GD.PushWarning($"[AnimationController] '{Character?.Name}' has no animation '{animName}'.");

        if (!string.IsNullOrEmpty(_fallbackAnimation) && _animationPlayer.HasAnimation(_fallbackAnimation))
            _animationPlayer.Play(_fallbackAnimation);
    }

    /// Queue an animation to play after the current one finishes.
    public void Queue(string animName)
    {
        if (_animationPlayer?.HasAnimation(animName) == true)
            _animationPlayer.Queue(animName);
    }

    // Helper so callers don't need a direct reference to the controller.
    private CharacterController? Character =>
        _character ??= GetParentOrNull<CharacterController>();
}
