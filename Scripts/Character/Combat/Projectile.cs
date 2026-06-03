using Godot;
using System.Collections.Generic;

namespace Supersmash;

/// <summary>
/// Base class for all projectiles. Inherits Area2D so it uses Godot's overlap
/// detection without CharacterBody2D floor/wall snapping.
///
/// Movement is driven by an internal velocity vector accumulated each tick —
/// the same fixed-rate physics model used by characters.
///
/// ── Spawning from AttackState ─────────────────────────────────────────────
/// AttackState calls this sequence (see AttackState.SpawnProjectile()):
///   1. Instantiate<Projectile>() from AttackData.ProjectileScene.
///   2. Set DirectionX = Character.FacingDirection.  (before AddChild)
///   3. Set OwnerCharacter = Character.              (before AddChild)
///   4. AddChild(proj) → _Ready() fires, initialises _velocity from DirectionX.
///   5. Set GlobalPosition = spawnPoint after AddChild.
///
/// ── Collision layers ─────────────────────────────────────────────────────
///   Collision Layer  → set to your HITBOX layer (so characters' hurtboxes see it)
///   Collision Mask   → set to your HURTBOX layer
///
/// ── Subclassing ──────────────────────────────────────────────────────────
/// Override ComputeVelocity() to add homing, sine-wave oscillation, bounce, etc.
///
/// Required scene setup (example "Fireball.tscn"):
///   Projectile (Area2D) ← this script
///   ├── CollisionShape2D   ← oval or circle shape
///   └── Sprite2D / AnimatedSprite2D
/// </summary>
public partial class Projectile : Area2D
{
    [ExportGroup("Movement")]
    [Export] public float Speed       { get; set; } = 400f;
    /// Gravity applied per second (px/s²). 0 = purely horizontal trajectory.
    [Export] public float GravityScale { get; set; } = 0f;
    /// Auto-despawn after this many seconds regardless of hit count.
    [Export] public float Lifetime    { get; set; } = 3f;

    [ExportGroup("Combat")]
    /// How many targets this projectile hits before self-destructing. 0 = unlimited.
    [Export] public int   MaxHits            { get; set; } = 1;
    [Export] public float HitstunMultiplier  { get; set; } = 1f;
    /// Combat payload applied on contact. Assign a HitboxData .tres in the inspector.
    [Export] public HitboxData? HitboxInfo   { get; set; }

    // ── Runtime state set by AttackState before AddChild ─────────────────────

    /// The character that fired this projectile.
    /// Set BEFORE AddChild so _Ready() can reference it.
    public CharacterController? OwnerCharacter { get; set; }

    /// 1 = travelling right, −1 = travelling left.
    /// Set BEFORE AddChild so _Ready() initialises velocity correctly.
    public int DirectionX { get; set; } = 1;

    // ── Internal ──────────────────────────────────────────────────────────────

    protected Vector2 _velocity;
    private float _lifeTimer = 0f;
    private int   _hitCount  = 0;
    private readonly HashSet<ulong> _alreadyHit = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        AreaEntered += OnAreaEntered;

        // Initial velocity is horizontal; gravity bends the trajectory each tick.
        _velocity = new Vector2(Speed * DirectionX, 0f);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        _velocity = ComputeVelocity(dt);
        GlobalPosition += _velocity * dt;

        _lifeTimer += dt;
        if (_lifeTimer >= Lifetime)
            QueueFree();
    }

    // ── Hit detection ─────────────────────────────────────────────────────────

    private void OnAreaEntered(Area2D area)
    {
        if (HitboxInfo is null) return;
        if (area is not Hurtbox hurtbox) return;
        if (hurtbox.GetParent() is not CharacterController target) return;

        if (target == OwnerCharacter) return;                      // never self-hit
        if (!_alreadyHit.Add(target.GetInstanceId())) return;      // dedup per target

        if (OwnerCharacter is not null)
            target.ReceiveHit(OwnerCharacter, HitboxInfo, HitstunMultiplier);

        _hitCount++;
        if (MaxHits > 0 && _hitCount >= MaxHits)
            QueueFree();
    }

    // ── Virtual extension point ───────────────────────────────────────────────

    /// <summary>
    /// Called each tick to produce the velocity for this frame.
    /// Default: applies GravityScale, then returns the accumulated velocity.
    /// Override for homing, oscillation, bounce, or multi-phase behaviour.
    /// </summary>
    protected virtual Vector2 ComputeVelocity(float delta)
    {
        if (GravityScale > 0f)
            _velocity.Y += GravityScale * delta;

        return _velocity;
    }
}
