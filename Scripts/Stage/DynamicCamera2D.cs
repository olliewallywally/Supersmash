using Godot;

namespace Supersmash;

/// <summary>
/// Attach to a Camera2D that sits as a top-level scene node (not parented to
/// any moving object). Tracks all active players, centers the view on their
/// midpoint, and adjusts zoom so all players are always visible.
///
/// Required setup in Godot:
///   1. Add a Camera2D to the scene root. Attach this script.
///   2. Drag each CharacterController into the Players array in the inspector.
///   3. Set Camera2D → Anchor Mode → Fixed TopLeft, Process Callback → Physics.
///   4. Optionally set LimitLeft/Right/Top/Bottom on the Camera2D node to keep
///      the view inside the stage boundaries (does not require code changes here).
///
/// ── Dead player handling ─────────────────────────────────────────────────
/// Players in RespawnState are excluded from position/zoom calculations.
/// They appear above the stage, which would violently pull the camera up and
/// zoom out if tracked. The remaining active players govern the frame.
/// When a single player is active the camera holds MaxZoom (close-up).
/// When ALL players are in RespawnState the camera holds its last position.
/// </summary>
public partial class DynamicCamera2D : Camera2D
{
    [ExportGroup("Players")]
    [Export] public CharacterController[] Players { get; set; } = System.Array.Empty<CharacterController>();

    [ExportGroup("Zoom")]
    /// Minimum zoom value — how far out the camera can pull (0.3 = shows a lot).
    /// Camera2D.Zoom works in Godot 4 as: higher = more zoomed IN.
    [Export] public float MinZoom     { get; set; } = 0.3f;
    [Export] public float MaxZoom     { get; set; } = 1.4f;
    /// Padding (pixels) added around the player bounding box before computing zoom.
    [Export] public float ZoomPadding { get; set; } = 220f;

    [ExportGroup("Smoothing")]
    /// Higher = camera catches up faster. Uses exponential decay (frame-rate independent).
    [Export] public float PositionSmoothSpeed { get; set; } = 4f;
    [Export] public float ZoomSmoothSpeed     { get; set; } = 2.5f;

    // ── Internal ──────────────────────────────────────────────────────────────

    private float _currentZoom;
    private float _shakeIntensity;
    private int   _shakeFrames;

    public override void _Ready()
    {
        _currentZoom = MaxZoom;
        Zoom         = Vector2.One * _currentZoom;

        // Snap to the starting midpoint without easing on the first frame.
        var active = CollectActivePlayers();
        if (active.Length > 0)
            GlobalPosition = ComputeCenter(active);
    }

    public override void _PhysicsProcess(double delta)
    {
        var active = CollectActivePlayers();

        if (active.Length == 0) return; // hold last position while all players respawn

        float dt = (float)delta;

        // ── Position ──────────────────────────────────────────────────────────
        // Exponential decay gives frame-rate-independent smoothing:
        // t → 0 when delta → 0 (barely moves), t → 1 when delta is large (snaps instantly).
        float posT = 1f - Mathf.Exp(-PositionSmoothSpeed * dt);
        GlobalPosition = GlobalPosition.Lerp(ComputeCenter(active), posT);

        // ── Zoom ──────────────────────────────────────────────────────────────
        float targetZoom = ComputeZoom(active);
        float zoomT      = 1f - Mathf.Exp(-ZoomSmoothSpeed * dt);
        _currentZoom     = Mathf.Lerp(_currentZoom, targetZoom, zoomT);
        Zoom             = Vector2.One * _currentZoom;

        // ── Screen shake ──────────────────────────────────────────────────────
        if (_shakeFrames > 0)
        {
            Offset = new Vector2(
                (float)GD.RandRange(-_shakeIntensity, _shakeIntensity),
                (float)GD.RandRange(-_shakeIntensity, _shakeIntensity));
            _shakeFrames--;
            if (_shakeFrames == 0) Offset = Vector2.Zero;
        }
    }

    public void Shake(float intensity, int frames = 7)
    {
        _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
        _shakeFrames    = Mathf.Max(_shakeFrames, frames);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private CharacterController[] CollectActivePlayers()
    {
        // Exclude players currently in RespawnState — they're above the stage and
        // would skew both the center point and the zoom calculation.
        int count = 0;
        var buffer = new CharacterController[Players.Length];
        foreach (var p in Players)
        {
            if (p is null || !Godot.GodotObject.IsInstanceValid(p)) continue;
            if (p.FSM?.CurrentStateName == "RespawnState") continue;
            buffer[count++] = p;
        }

        var result = new CharacterController[count];
        System.Array.Copy(buffer, result, count);
        return result;
    }

    private static Vector2 ComputeCenter(CharacterController[] players)
    {
        Vector2 sum = Vector2.Zero;
        foreach (var p in players) sum += p.GlobalPosition;
        return sum / players.Length;
    }

    private float ComputeZoom(CharacterController[] players)
    {
        if (players.Length <= 1) return MaxZoom; // single player: stay close

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (var p in players)
        {
            if (p.GlobalPosition.X < minX) minX = p.GlobalPosition.X;
            if (p.GlobalPosition.X > maxX) maxX = p.GlobalPosition.X;
            if (p.GlobalPosition.Y < minY) minY = p.GlobalPosition.Y;
            if (p.GlobalPosition.Y > maxY) maxY = p.GlobalPosition.Y;
        }

        // Padded bounding box in world space
        float boxW = (maxX - minX) + ZoomPadding * 2f;
        float boxH = (maxY - minY) + ZoomPadding * 2f;

        // How much zoom to fit the box into the viewport (higher zoom = more zoomed in)
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        float zoomX = viewportSize.X / Mathf.Max(boxW, 1f);
        float zoomY = viewportSize.Y / Mathf.Max(boxH, 1f);

        // Use the smaller axis so both players are always fully inside the frame.
        return Mathf.Clamp(Mathf.Min(zoomX, zoomY), MinZoom, MaxZoom);
    }
}
