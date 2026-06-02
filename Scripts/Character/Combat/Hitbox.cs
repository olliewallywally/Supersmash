using Godot;
using System.Collections.Generic;

namespace Supersmash;

/// <summary>
/// An active hitbox on an attacking character.
/// Attach as a child of an Area2D that has its own CollisionShape2D.
///
/// Scene structure:
///   HitboxContainer (Area2D)
///   └── Hitbox.cs script on the Area2D
///       └── CollisionShape2D (shape reflects the actual hitbox region)
///
/// The owning attack activates/deactivates this node per active frame.
/// Hitbox tracks which targets it has already hit during this attack instance
/// to prevent multi-hitting on a single swing.
/// </summary>
public partial class Hitbox : Area2D
{
    [Export] public HitboxData? Data { get; set; }

    /// Characters this hitbox has already confirmed a hit on in this activation window.
    /// Cleared when the hitbox is deactivated (end of active frames).
    private readonly HashSet<ulong> _alreadyHit = new();

    [Signal] public delegate void HitConfirmedEventHandler(CharacterController target, HitboxData data);

    public override void _Ready()
    {
        // Hitboxes start inactive; the AttackState enables them on the first active frame.
        Monitoring  = false;
        Monitorable = false;

        AreaEntered += OnAreaEntered;
    }

    /// Enable collision detection. Call on the first active frame of the attack.
    public void Activate()
    {
        _alreadyHit.Clear();
        Monitoring  = true;
        Monitorable = true;
    }

    /// Disable collision detection. Call on the first recovery frame.
    public void Deactivate()
    {
        Monitoring  = false;
        Monitorable = false;
    }

    private void OnAreaEntered(Area2D area)
    {
        if (Data is null) return;

        // Walk up to find the Hurtbox component and its owning CharacterController.
        if (area is not Hurtbox hurtbox) return;
        if (hurtbox.Owner is not CharacterController target) return;

        // Prevent hitting the same target twice in one swing.
        ulong targetId = target.GetInstanceId();
        if (_alreadyHit.Contains(targetId)) return;

        // Prevent self-hit.
        if (Owner is CharacterController attacker && attacker == target) return;

        _alreadyHit.Add(targetId);
        EmitSignal(SignalName.HitConfirmed, target, Data);
    }
}
