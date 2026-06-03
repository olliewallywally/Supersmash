using Godot;

namespace Supersmash;

/// <summary>
/// Attach to the root Node2D of HitSpark.tscn. Automatically frees the node
/// after LifetimeSec so effects never accumulate in the scene tree.
///
/// Typical HitSpark.tscn structure:
///   HitSpark (Node2D) ← this script
///   ├── GPUParticles2D  — burst of sparks / stars
///   └── AnimatedSprite2D — optional frame-by-frame slash art
///
/// GPUParticles2D settings to match hit-freeze timing:
///   • One Shot:      true
///   • Explosiveness: 1.0  (emit all particles instantly)
///   • Lifetime:      match LifetimeSec below
///
/// The node is spawned at GlobalPosition by Hitbox, then auto-freed here.
/// Parent it to the scene root (not a character) so it doesn't move with either fighter.
/// </summary>
public partial class HitSpark : Node2D
{
    /// How long the effect lives. Match to your longest child particle/animation duration.
    [Export] public float LifetimeSec { get; set; } = 0.35f;

    private float _timer = 0f;

    public override void _Process(double delta)
    {
        _timer += (float)delta;
        if (_timer >= LifetimeSec)
            QueueFree();
    }
}
