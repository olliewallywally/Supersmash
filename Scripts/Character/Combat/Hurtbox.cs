using Godot;

namespace Supersmash;

/// <summary>
/// Marks a region of a character that can be hit by enemy Hitboxes.
/// Must be an Area2D on layer 2 (hurtbox layer), detecting layer 1 (hitbox layer).
///
/// Scene structure:
///   CharacterController
///   └── HurtboxContainer (Area2D) — this script lives here
///       └── CollisionShape2D
///
/// Hurtboxes are passive — they do not initiate anything. The Hitbox that
/// overlaps them emits HitConfirmed, and CharacterController.OnHitReceived
/// processes the actual knockback + state transition.
///
/// This class can be extended per-character to implement invincibility frames,
/// super armour, or parry windows by toggling the collision layer/mask.
/// </summary>
public partial class Hurtbox : Area2D
{
    public enum InvincibilityType { None, FullInvincibility, SuperArmour }

    private InvincibilityType _currentInvincibility = InvincibilityType.None;

    public InvincibilityType CurrentInvincibility
    {
        get => _currentInvincibility;
        set
        {
            _currentInvincibility = value;
            // Full invincibility removes the hurtbox from the physics world entirely.
            // Super armour keeps it active but lets the combat layer handle the absorption.
            SetDeferred(Area2D.PropertyName.Monitorable, value != InvincibilityType.FullInvincibility);
        }
    }

    public bool HasSuperArmour => _currentInvincibility == InvincibilityType.SuperArmour;
}
