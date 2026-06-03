using Godot;

namespace Supersmash;

/// <summary>
/// A tapered Line2D that traces a weapon tip through world space during the
/// active window of an attack, then drains away when the window closes.
///
/// ── Setup ────────────────────────────────────────────────────────────────
///   1. Add a Line2D node anywhere under CharacterController (e.g. as a
///      sibling of the hitbox nodes, NOT under VisualRoot).
///   2. Attach this script. In the Godot inspector, enable "Top Level" on the
///      node — this detaches it from the parent's transform so all points are
///      recorded in world space.
///   3. Drag the weapon-tip Node2D (a child of VisualRoot near the blade end)
///      into TipPoint.
///   4. Set a Width and DefaultColor in the Line2D inspector.
///   5. Enter the node name in AttackData.TrailNodeName for each attack that
///      should display a trail.
///
/// ── How it works ──────────────────────────────────────────────────────────
///   • Activate(): called by AttackState at FirstActiveFrame — starts recording.
///   • Deactivate(): called at FirstRecoveryFrame — stops recording; trail drains.
///   • _PhysicsProcess(): active → prepend tip position, trim to TrailLength;
///                        inactive → remove tail point until empty.
///   • WidthCurve tapers from full-width at tip (index 0) to zero at tail.
///   • A gradient fades alpha from opaque at tip to transparent at tail.
/// </summary>
public partial class WeaponTrail : Line2D
{
    [Export] public Node2D? TipPoint    { get; set; }
    [Export] public int     TrailLength { get; set; } = 14;

    private bool _active = false;

    public override void _Ready()
    {
        // Detach from parent transform so points are stored in world coordinates.
        TopLevel         = true;
        GlobalPosition   = Vector2.Zero;
        GlobalRotation   = 0f;
        GlobalScale      = Vector2.One;

        ClearPoints();
        ApplyWidthCurve();
        ApplyGradient();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Activate()
    {
        _active = true;
        ClearPoints();
    }

    public void Deactivate()
    {
        _active = false;
    }

    // ── Per-tick ──────────────────────────────────────────────────────────────

    public override void _PhysicsProcess(double delta)
    {
        if (TipPoint is null) return;

        if (_active)
        {
            // Index 0 = freshest point (tip). Older points are pushed toward higher indices.
            AddPoint(TipPoint.GlobalPosition, 0);

            while (PointCount > TrailLength)
                RemovePoint(PointCount - 1);
        }
        else if (PointCount > 0)
        {
            // Drain from the tail one point per tick so the trail dissolves smoothly
            // rather than vanishing instantly when the active window closes.
            RemovePoint(PointCount - 1);
        }
    }

    // ── Visual setup (run once in _Ready) ─────────────────────────────────────

    private void ApplyWidthCurve()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, 1f), 0f, 0f);   // tip: full width
        curve.AddPoint(new Vector2(1f, 0f), 0f, 0f);   // tail: zero width
        WidthCurve = curve;
    }

    private void ApplyGradient()
    {
        // Fade from the Line2D's configured colour (opaque at tip) to transparent at tail.
        var gradient = new Gradient();
        gradient.SetColor(0, DefaultColor with { A = 1f });
        gradient.SetColor(1, DefaultColor with { A = 0f });
        Gradient = gradient;
    }
}
