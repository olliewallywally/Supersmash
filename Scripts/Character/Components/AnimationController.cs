using Godot;
using System.Collections.Generic;

namespace Supersmash;

/// <summary>
/// Drives AnimatedSprite2D and visual facing based on FSM state changes.
/// Completely decoupled from physics: states know nothing about this component.
///
/// ── Why states don't call animations directly ─────────────────────────────
/// Separating animation from physics means states are headless-server-safe and
/// trivially unit-testable. This component subscribes to FSM.StateChanged.
///
/// ── Required scene structure ──────────────────────────────────────────────
///   CharacterController (CharacterBody2D)
///   ├── VisualRoot (Node2D)              ← export; scale-flipped for facing
///   │   └── AnimatedSprite2D             ← export; holds SpriteFrames resource
///   ├── AnimationController (Node)       ← this script
///   ├── HurtboxContainer (Hurtbox)
///   └── JabHitbox (Hitbox)
///
/// Hitboxes must be siblings of VisualRoot (not children) so the X-flip that
/// mirrors the sprite does not also mirror the hitbox positions.
///
/// ── Animation naming convention ───────────────────────────────────────────
///   State animations : "idle", "run", "jumpsquat", "jump_rise", "fall",
///                      "hitstun", "helpless", "respawn"
///   Attack animations: "attack_" + AttackId  →  "attack_Jab", "attack_NeutralAir"
///   These names must match entries in the SpriteFrames resource assigned in the
///   inspector (res://Resources/SpriteFrames/adventurer_base.tres).
///
/// ── Hitstop (freeze-frame) ────────────────────────────────────────────────
///   SetPaused(true)  → SpeedScale = 0  (holds the current frame in place)
///   SetPaused(false) → SpeedScale = 1  (resumes from the same frame)
/// </summary>
public partial class AnimationController : Node
{
    // ── Inspector exports ─────────────────────────────────────────────────────

    [Export] private AnimatedSprite2D? _sprite;

    /// Scale-flipped each tick to reflect FacingDirection changes.
    /// Must NOT be an ancestor of hitbox/hurtbox nodes.
    [Export] private Node2D? _visualRoot;

    [Export] private string _fallbackAnimation { get; set; } = "idle";

    // ── State → animation name table ─────────────────────────────────────────
    // Protected so character-specific subclasses can add/override entries.

    protected readonly Dictionary<string, string> StateAnimations = new()
    {
        { "IdleState",      "idle"      },
        { "RunState",       "run"       },
        { "JumpSquatState", "jumpsquat" },
        { "JumpState",      "jump_rise" },
        { "FallState",      "fall"      },
        { "HitstunState",   "hitstun"   },
        { "HelplessState",  "helpless"  },
        { "RespawnState",   "respawn"   },
        { "AttackState",    ""          }, // resolved per-attack below
    };

    // ── Internal ──────────────────────────────────────────────────────────────

    private CharacterController _character = null!;
    private int    _lastFacingDirection = 1;
    private string _pendingAnimation    = string.Empty;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        CallDeferred(MethodName.LateInit);
    }

    private void LateInit()
    {
        _character           = GetParent<CharacterController>();
        _lastFacingDirection = _character.FacingDirection;
        _character.FSM.StateChanged += OnStateChanged;

        if (_sprite is not null)
            _sprite.AnimationFinished += OnAnimationFinished;
    }

    public override void _ExitTree()
    {
        if (_character?.FSM is not null)
            _character.FSM.StateChanged -= OnStateChanged;

        if (_sprite is not null)
            _sprite.AnimationFinished -= OnAnimationFinished;
    }

    // ── Facing direction (per-tick) ───────────────────────────────────────────

    public override void _PhysicsProcess(double delta)
    {
        if (_visualRoot is null || _character is null) return;
        if (_character.FacingDirection == _lastFacingDirection) return;

        _lastFacingDirection = _character.FacingDirection;
        _visualRoot.Scale    = new Vector2(_character.FacingDirection, 1f);
    }

    // ── FSM signal handler ────────────────────────────────────────────────────

    private void OnStateChanged(string from, string to)
    {
        if (_sprite is null) return;

        if (to == "AttackState" && _character.FSM.CurrentState is AttackState attackState)
        {
            Play("attack_" + attackState.CurrentAttackId);
            return;
        }

        if (StateAnimations.TryGetValue(to, out string? anim) && !string.IsNullOrEmpty(anim))
            Play(anim);
    }

    // ── Animation finished handler ────────────────────────────────────────────

    private void OnAnimationFinished()
    {
        if (string.IsNullOrEmpty(_pendingAnimation)) return;
        string next     = _pendingAnimation;
        _pendingAnimation = string.Empty;
        Play(next);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// Play an animation immediately. Falls back to _fallbackAnimation if not found.
    public void Play(string animName)
    {
        if (_sprite is null) return;

        StringName name = animName;
        if (_sprite.SpriteFrames?.HasAnimation(name) == true)
        {
            _sprite.Play(name);
            return;
        }

        GD.PushWarning($"[AnimationController] '{_character?.Name}' has no animation '{animName}'.");

        if (!string.IsNullOrEmpty(_fallbackAnimation))
        {
            StringName fallback = _fallbackAnimation;
            if (_sprite.SpriteFrames?.HasAnimation(fallback) == true)
                _sprite.Play(fallback);
        }
    }

    /// Queue an animation to play after the current one finishes (non-looping only).
    public void Queue(string animName)
    {
        _pendingAnimation = animName;
    }

    /// Freeze (SpeedScale → 0) or unfreeze (SpeedScale → 1).
    /// Called by HitstopManager (attacker) and HitstunState (defender) around hitlag.
    public void SetPaused(bool paused)
    {
        if (_sprite is null) return;
        _sprite.SpeedScale = paused ? 0f : 1f;
    }
}
