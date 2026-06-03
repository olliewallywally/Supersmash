using Godot;

namespace Supersmash;

/// <summary>
/// Attach to four Area2D nodes placed at the stage edges (Top, Bottom, Left, Right).
/// When a CharacterController's physics body enters, Die() is called on it.
///
/// Required setup in Godot:
///   1. Create four Area2D nodes in the stage scene (e.g. BlastTop, BlastBottom,
///      BlastLeft, BlastRight). Position each ~600–800px beyond the visible edge.
///   2. Give each a CollisionShape2D (long, thin rectangle spanning the full edge).
///   3. Set collision layer/mask so the Area2D detects the character physics body
///      layer (not the hurtbox layer — we want to catch the body, not the hitbox).
///   4. Attach this script to each of the four nodes.
///
/// ── Why BodyEntered, not AreaEntered ────────────────────────────────────────
/// CharacterController is a CharacterBody2D (physics body), not an Area2D.
/// BodyEntered fires when any PhysicsBody2D enters the zone, which is exactly
/// what we want. AreaEntered would fire for hitbox/hurtbox Area2D children instead.
/// </summary>
public partial class BlastZone : Area2D
{
    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is CharacterController character)
            character.Die();
    }
}
