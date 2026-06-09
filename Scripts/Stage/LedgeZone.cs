using Godot;
using Godot.Collections;

namespace Supersmash;

/// <summary>
/// A grab-trigger volume placed at each stage ledge. When a falling character's
/// body enters, they snap into LedgeHangState at HangPosition.
///
/// Required setup in Godot (per ledge, typically two per stage):
///   1. Area2D with this script + CollisionShape2D covering the grab region
///      (roughly 30×60 px just outside and below the stage lip).
///   2. Set Facing: +1 if the stage interior is to the RIGHT of this ledge
///      (i.e. this is the LEFT ledge), –1 for the right ledge.
///   3. Collision mask = the character body layer.
///
/// Grab conditions (all must hold):
///   • The character is in FallState or HelplessState — never during hitstun
///     (being launched past the ledge shouldn't magnet you onto it), never
///     while rising in JumpState (Melee-style: no rising aerial grabs).
///   • The character is descending (velocity.Y > 0).
///   • The character is facing the stage, OR is in HelplessState (a recovery
///     special snaps regardless of facing — forgiving, Ultimate-style).
/// </summary>
public partial class LedgeZone : Area2D
{
    /// +1 → stage is to the right (left ledge). –1 → stage is to the left (right ledge).
    [Export] public int Facing { get; set; } = 1;

    /// World-space position the character's origin is pinned to while hanging.
    /// Defaults to this node's own position if left unset in the inspector.
    [Export] public Node2D? HangPosition { get; set; }

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not CharacterController character) return;

        string state = character.FSM.CurrentStateName;
        bool helpless = state == "HelplessState";
        if (state != "FallState" && !helpless) return;

        if (character.CharacterVelocity.Y <= 0f) return;                 // must be falling
        if (!helpless && character.FacingDirection != Facing) return;    // must face stage

        Vector2 hangPos = HangPosition?.GlobalPosition ?? GlobalPosition;
        character.FSM.TransitionTo("LedgeHangState", new Dictionary
        {
            { "hang_position", hangPos },
            { "facing",        Facing  },
        });
    }
}
