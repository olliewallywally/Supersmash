using Godot;
using System.Collections.Generic;

namespace Supersmash;

/// <summary>
/// An active hitbox on an attacking character (an Area2D with a CollisionShape2D child).
///
/// Lifecycle: AttackState arms it (sets Data + HitstunMultiplier, calls Activate) on the
/// first active frame and Deactivate()s it on the first recovery frame. While live, any
/// enemy Hurtbox that overlaps triggers a hit, routed to that target's ReceiveHit.
///
/// Double-hit prevention: _alreadyHit records each target's instance id on contact and
/// is cleared on Activate(), so one target is hit at most once per activation window.
/// (Multi-hit moves will later re-arm per hit, or use a per-hit refresh interval.)
/// </summary>
public partial class Hitbox : Area2D
{
    [Export] public HitboxData? Data { get; set; }

    /// Per-attack hitstun scaling, set by AttackState from AttackData.HitstunMultiplier.
    public float HitstunMultiplier { get; set; } = 1f;

    /// The character that owns/threw this hitbox. Set once by CharacterController._Ready.
    /// Used to attribute the hit and to prevent self-damage.
    public CharacterController? AttackerRef { get; set; }

    /// Assign a HitSpark.tscn packed scene to display a spark at the contact point.
    /// Leave null to suppress the visual (e.g. during early development).
    [Export] public PackedScene? HitSparkScene { get; set; }

    /// Targets already hit during the current activation window.
    private readonly HashSet<ulong> _alreadyHit = new();

    /// Add <paramref name="instanceId"/> to the already-hit set without a confirmed hit.
    /// Called by AttackState when the tipper (sweetspot) hits first, so the sourspot
    /// cannot land a second hit on the same target in the same activation window.
    public void SuppressTarget(ulong instanceId) => _alreadyHit.Add(instanceId);

    /// Emitted after a confirmed hit, for VFX / SFX / camera-shake listeners.
    [Signal] public delegate void HitConfirmedEventHandler(CharacterController target, HitboxData data);

    public override void _Ready()
    {
        // Inactive until an attack arms it.
        Monitoring  = false;
        Monitorable = false;
        AreaEntered += OnAreaEntered;
    }

    public void Activate()
    {
        _alreadyHit.Clear();
        Monitoring  = true;
        Monitorable = true;
    }

    public void Deactivate()
    {
        Monitoring  = false;
        Monitorable = false;
    }

    private void OnAreaEntered(Area2D area)
    {
        if (Data is null) return;
        if (area is not Hurtbox hurtbox) return;
        if (hurtbox.GetParent() is not CharacterController target) return;

        // Never hit ourselves.
        if (AttackerRef is not null && AttackerRef == target) return;

        // Hit each target at most once per activation.
        ulong targetId = target.GetInstanceId();
        if (!_alreadyHit.Add(targetId)) return;

        target.ReceiveHit(AttackerRef ?? target, Data, HitstunMultiplier);
        EmitSignal(SignalName.HitConfirmed, target, Data);

        SpawnHitSpark(area.GlobalPosition);
    }

    private void SpawnHitSpark(Vector2 hurtboxPosition)
    {
        if (HitSparkScene is null) return;

        // Midpoint between hitbox centre and hurtbox centre approximates contact.
        Vector2 contactPos = (GlobalPosition + hurtboxPosition) * 0.5f;

        // Orient the spark so directional art (slashes, etc.) faces from attacker → defender.
        float rotation = (hurtboxPosition - GlobalPosition).Angle();

        var spark = HitSparkScene.Instantiate<Node2D>();
        spark.GlobalPosition = contactPos;
        spark.Rotation       = rotation;

        // Parent to the scene root so the spark is independent of both fighters.
        GetTree().CurrentScene.AddChild(spark);
    }
}
