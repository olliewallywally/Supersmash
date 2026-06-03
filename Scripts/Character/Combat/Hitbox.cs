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

    /// Targets already hit during the current activation window.
    private readonly HashSet<ulong> _alreadyHit = new();

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
    }
}
