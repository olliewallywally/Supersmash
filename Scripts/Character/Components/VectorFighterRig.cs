using Godot;

namespace Supersmash;

/// <summary>
/// A procedurally-built, code-animated vector fighter. Replaces the pixel
/// AnimatedSprite2D as the character's visual. Original geometry — no external
/// art assets — so it is fully shippable.
///
/// ── How it works ──────────────────────────────────────────────────────────
/// _Ready() assembles a humanoid out of Polygon2D limbs whose colours come from
/// the CharacterData palette (HairColor, ShirtColor, …). Every limb is a node
/// pivoting at a joint, so animation is just per-frame Rotation / Position.
///
/// _PhysicsProcess() reads the parent CharacterController's FSM state, velocity,
/// and StateFrameCounter, then poses the rig. Because StateFrameCounter freezes
/// during hitstop, attack swings freeze on the contact frame for free.
///
/// Place this under VisualRoot (which AnimationController X-flips with facing),
/// so the rig mirrors correctly without any per-limb flip logic here.
/// </summary>
public partial class VectorFighterRig : Node2D
{
    // ── Joint anchors (local space; origin = character centre) ────────────────
    private static readonly Vector2 HipAnchor      = new(0f, 6f);
    private static readonly Vector2 ShoulderAnchor = new(0f, -20f);
    private static readonly Vector2 HeadAnchor     = new(0f, -30f);

    private const float LegLength = 30f;
    private const float ArmLength = 24f;

    // ── Parts ─────────────────────────────────────────────────────────────────
    private Node2D    _root      = null!;   // whole-body bob/squash container
    private Polygon2D _rearLeg   = null!;
    private Polygon2D _frontLeg  = null!;
    private Polygon2D _torso     = null!;
    private Polygon2D _rearArm   = null!;
    private Polygon2D _frontArm  = null!;
    private Node2D    _swordPivot = null!;
    private Polygon2D _head      = null!;
    private Polygon2D _hair      = null!;
    private Polygon2D _pauldron  = null!;
    private Polygon2D _shadow    = null!;

    // ── Source data ─────────────────────────────────────────────────────────────
    private CharacterController _character = null!;
    private CharacterData       _data      = null!;
    private bool _hasSword;

    // Smoothed pose values so transitions don't snap.
    private float _curTorsoLean;
    private float _curSquash = 1f;

    public override void _Ready()
    {
        _character = FindController();
        _data      = _character?.Data ?? new CharacterData();
        _hasSword  = _data.HasSword;

        Build();
    }

    private CharacterController FindController()
    {
        Node n = this;
        while (n is not null)
        {
            if (n is CharacterController c) return c;
            n = n.GetParent();
        }
        return null!;
    }

    // ── Construction ────────────────────────────────────────────────────────────

    private void Build()
    {
        _root = new Node2D();
        AddChild(_root);

        // Soft contact shadow (under the feet, drawn first / lowest).
        _shadow = MakeEllipse(26f, 7f, new Color(0f, 0f, 0f, 0.22f));
        _shadow.Position = new Vector2(0f, 40f);
        _root.AddChild(_shadow);

        // Legs (drawn behind torso).
        _rearLeg  = MakeLimb(9f, 7f, LegLength, Darken(_data.PantsColor, 0.78f), _data.AccentColor);
        _frontLeg = MakeLimb(9f, 7f, LegLength, _data.PantsColor, _data.AccentColor);
        _rearLeg.Position = _frontLeg.Position = HipAnchor;
        _root.AddChild(_rearLeg);

        // Rear arm (behind torso).
        _rearArm = MakeLimb(7f, 5f, ArmLength, Darken(_data.ShirtColor, 0.8f), _data.SkinColor);
        _rearArm.Position = ShoulderAnchor;
        _root.AddChild(_rearArm);

        // Torso.
        _torso = MakePolygon(_data.ShirtColor, new Vector2[]
        {
            new(-12f, -22f), new(12f, -22f), new(10f, 8f), new(-10f, 8f),
        });
        _root.AddChild(_torso);

        // Pauldron / shoulder accent — a signature silhouette piece.
        _pauldron = MakePolygon(_data.AccentColor, new Vector2[]
        {
            new(2f, -26f), new(20f, -24f), new(22f, -14f), new(6f, -16f),
        });
        _root.AddChild(_pauldron);

        // Front leg + front arm (in front of torso).
        _root.AddChild(_frontLeg);

        // Head + hair.
        _head = MakeCircle(11f, _data.SkinColor);
        _head.Position = HeadAnchor;
        _root.AddChild(_head);

        _hair = MakeHair(_data.HairColor);
        _hair.Position = HeadAnchor;
        _root.AddChild(_hair);

        // Front arm last (foreground) — holds the sword if any.
        _frontArm = MakeLimb(7f, 5f, ArmLength, _data.ShirtColor, _data.SkinColor);
        _frontArm.Position = ShoulderAnchor;
        _root.AddChild(_frontArm);

        // Sword pivots from the front hand (tip of the front arm).
        _swordPivot = new Node2D { Position = new Vector2(0f, ArmLength) };
        _frontArm.AddChild(_swordPivot);
        if (_hasSword)
            BuildSword(_swordPivot);
    }

    private void BuildSword(Node2D parent)
    {
        // Guard.
        var guard = MakePolygon(Darken(_data.AccentColor, 0.7f), new Vector2[]
        {
            new(-9f, -2f), new(9f, -2f), new(9f, 2f), new(-9f, 2f),
        });
        parent.AddChild(guard);

        // Grip (short, behind the hand).
        var grip = MakePolygon(new Color(0.18f, 0.14f, 0.10f), new Vector2[]
        {
            new(-2f, 0f), new(2f, 0f), new(2f, 10f), new(-2f, 10f),
        });
        parent.AddChild(grip);

        // Blade — a long tapering greatsword pointing "up" from the guard
        // (local -Y), so arm rotation sweeps it naturally.
        float len = _data.SwordLength;
        var blade = MakePolygon(_data.SwordColor, new Vector2[]
        {
            new(-6f, -2f), new(6f, -2f), new(5f, -len), new(0f, -len - 8f), new(-5f, -len),
        });
        parent.AddChild(blade);

        // Centre fuller line for a metallic read.
        var fuller = MakePolygon(Lighten(_data.SwordColor, 1.12f), new Vector2[]
        {
            new(-1.4f, -4f), new(1.4f, -4f), new(1.4f, -len + 4f), new(-1.4f, -len + 4f),
        });
        parent.AddChild(fuller);
    }

    // ── Per-frame animation ─────────────────────────────────────────────────────

    public override void _PhysicsProcess(double delta)
    {
        if (_character is null) return;

        string state = _character.FSM?.CurrentStateName ?? "IdleState";
        int    f     = _character.StateFrameCounter;
        float  vx    = Mathf.Abs(_character.CharacterVelocity.X);

        // Default resting pose.
        float bob        = 0f;
        float squash     = 1f;
        float torsoLean  = 0f;
        float rearLeg    = 0f;
        float frontLeg   = 0f;
        float rearArm    = 0.35f;
        float frontArm   = _hasSword ? 0.55f : 0.35f;
        bool  showSword  = _hasSword;

        switch (state)
        {
            case "IdleState":
            case "ShieldState":
                bob       = Mathf.Sin(f * 0.12f) * 1.6f;
                rearArm   = 0.35f + Mathf.Sin(f * 0.12f) * 0.04f;
                frontArm  = (_hasSword ? 0.55f : 0.30f) + Mathf.Sin(f * 0.12f) * 0.04f;
                break;

            case "RunState":
            case "DashState":
            {
                float ph = f * 0.55f;
                rearLeg   = Mathf.Sin(ph) * 0.75f;
                frontLeg  = Mathf.Sin(ph + Mathf.Pi) * 0.75f;
                rearArm   = 0.35f + Mathf.Sin(ph + Mathf.Pi) * 0.5f;
                frontArm  = (_hasSword ? 0.7f : 0.35f) + Mathf.Sin(ph) * 0.4f;
                torsoLean = 0.16f;
                bob       = Mathf.Abs(Mathf.Sin(ph)) * 2f;
                break;
            }

            case "JumpSquatState":
                squash    = 0.82f;
                rearLeg   = 0.3f; frontLeg = -0.3f;
                break;

            case "JumpState":
                rearLeg   = 0.5f; frontLeg = 0.25f;
                rearArm   = 0.9f; frontArm = _hasSword ? 0.9f : 0.8f;
                torsoLean = 0.1f;
                break;

            case "FallState":
                rearLeg   = 0.35f; frontLeg = 0.15f;
                rearArm   = 1.1f;  frontArm = _hasSword ? 1.0f : 1.0f;
                break;

            case "CrouchState":
                squash    = 0.66f;
                rearLeg   = 0.6f; frontLeg = -0.6f;
                break;

            case "LandingState":
                squash    = 0.8f;
                rearLeg   = 0.25f; frontLeg = -0.25f;
                break;

            case "HitstunState":
                torsoLean = -0.4f;
                rearArm   = 1.6f; frontArm = 1.4f;
                rearLeg   = -0.4f; frontLeg = 0.4f;
                bob       = Mathf.Sin(f * 0.9f) * 1.5f;
                break;

            case "HelplessState":
                torsoLean = 0.2f;
                rearArm   = 1.3f; frontArm = 1.3f;
                rearLeg   = Mathf.Sin(f * 0.3f) * 0.3f;
                frontLeg  = -Mathf.Sin(f * 0.3f) * 0.3f;
                break;

            case "DodgeState":
                squash    = 0.9f;
                torsoLean = 0.3f;
                rearArm   = 0.8f; frontArm = 0.8f;
                break;

            case "GrabState":
                frontArm  = 1.7f; rearArm = 1.5f;
                torsoLean = 0.12f;
                break;

            case "LedgeHangState":
                rearArm   = 2.7f; frontArm = 2.7f;
                rearLeg   = 0.2f; frontLeg = -0.1f;
                break;

            case "AttackState":
                PoseAttack(f, ref rearArm, ref frontArm, ref torsoLean,
                           ref rearLeg, ref frontLeg);
                break;
        }

        // Smooth lean & squash so state changes ease rather than snap.
        float k = 1f - Mathf.Exp(-22f * (float)delta);
        _curTorsoLean = Mathf.Lerp(_curTorsoLean, torsoLean, k);
        _curSquash    = Mathf.Lerp(_curSquash, squash, k);

        // Apply. Torso/head lean use +θ = forward directly (the torso polygon
        // extends upward, so a positive rotation tips its top toward +X).
        // Limbs extend DOWN, where +θ swings them backward — so negate, giving
        // every pose value the intuitive "positive = toward facing" meaning.
        _root.Position     = new Vector2(0f, bob);
        _root.Scale        = new Vector2(1f, _curSquash);
        _torso.Rotation    = _curTorsoLean;
        _head.Rotation     = _curTorsoLean * 0.6f;
        _hair.Rotation     = _curTorsoLean * 0.6f;
        _head.Position     = HeadAnchor + new Vector2(_curTorsoLean * 10f, 0f);
        _hair.Position     = _head.Position;
        _pauldron.Rotation = _curTorsoLean;

        _rearLeg.Rotation  = -rearLeg;
        _frontLeg.Rotation = -frontLeg;
        _rearArm.Rotation  = -rearArm;
        _frontArm.Rotation = -frontArm;

        if (_swordPivot.GetChildCount() > 0)
            _swordPivot.Visible = showSword;
    }

    // ── Attack posing ───────────────────────────────────────────────────────────

    private void PoseAttack(int f, ref float rearArm, ref float frontArm,
                            ref float torsoLean, ref float rearLeg, ref float frontLeg)
    {
        string id = (_character.FSM?.CurrentState as AttackState)?.CurrentAttackId ?? "Jab";

        // 0→1 swing progress over the move's visible window (~18 frames),
        // eased so the swing snaps out then settles.
        float p = Mathf.Clamp(f / 16f, 0f, 1f);
        float swing = Mathf.Sin(p * Mathf.Pi);          // 0→1→0 arc
        float follow = Mathf.SmoothStep(0f, 1f, p);     // monotonic for recovery

        torsoLean = 0.1f + swing * 0.18f;
        rearLeg   = -0.2f; frontLeg = 0.3f;

        switch (id)
        {
            case "UpTilt":
            case "UpAir":
            case "UpSmash":
                // Overhead vertical arc.
                frontArm = Mathf.Lerp(2.6f, 0.2f, follow);
                rearArm  = Mathf.Lerp(2.4f, 0.6f, follow);
                torsoLean = -0.1f + swing * 0.1f;
                break;

            case "BackAir":
                // Sweep behind (local -X via negative angle).
                frontArm = Mathf.Lerp(-0.4f, -2.4f, follow);
                rearArm  = Mathf.Lerp(-0.2f, -2.0f, follow);
                torsoLean = -swing * 0.2f;
                break;

            case "NeutralAir":
                // Spin-ish: both arms out, sword sweeps a wide circle.
                frontArm = 0.6f + Mathf.Sin(p * Mathf.Tau) * 1.4f;
                rearArm  = 0.6f - Mathf.Sin(p * Mathf.Tau) * 1.4f;
                break;

            case "ForwardSmash":
            case "NeutralSpecial":
                // Big committed forward slash with windup.
                frontArm = p < 0.4f
                    ? Mathf.Lerp(2.2f, 2.4f, p / 0.4f)          // windup raised
                    : Mathf.Lerp(2.4f, 1.5f, (p - 0.4f) / 0.6f); // slash down-forward
                rearArm  = 1.0f + swing * 0.6f;
                torsoLean = 0.05f + swing * 0.35f;
                break;

            default: // Jab, ForwardTilt, ForwardAir — quick forward poke/slash
                frontArm = Mathf.Lerp(0.4f, 1.9f, swing);
                rearArm  = 0.8f + swing * 0.4f;
                break;
        }
    }

    // ── Geometry helpers ────────────────────────────────────────────────────────

    private static Polygon2D MakePolygon(Color color, Vector2[] pts)
    {
        var poly = new Polygon2D { Color = color };
        poly.Polygon = pts;
        return poly;
    }

    /// A limb that pivots at its top (0,0) and extends down to (0,length).
    /// Tapered from width w1 (top) to w2 (bottom), with a small accent "boot/cuff".
    private Polygon2D MakeLimb(float w1, float w2, float length, Color color, Color cuff)
    {
        var limb = MakePolygon(color, new Vector2[]
        {
            new(-w1 * 0.5f, 0f), new(w1 * 0.5f, 0f),
            new(w2 * 0.5f, length), new(-w2 * 0.5f, length),
        });
        var foot = MakePolygon(cuff, new Vector2[]
        {
            new(-w2 * 0.6f, length - 4f), new(w2 * 0.6f + 3f, length - 4f),
            new(w2 * 0.6f + 3f, length),  new(-w2 * 0.6f, length),
        });
        limb.AddChild(foot);
        return limb;
    }

    private static Polygon2D MakeCircle(float r, Color color)
    {
        const int seg = 14;
        var pts = new Vector2[seg];
        for (int i = 0; i < seg; i++)
        {
            float a = Mathf.Tau * i / seg;
            pts[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }
        return MakePolygon(color, pts);
    }

    private static Polygon2D MakeEllipse(float rx, float ry, Color color)
    {
        const int seg = 16;
        var pts = new Vector2[seg];
        for (int i = 0; i < seg; i++)
        {
            float a = Mathf.Tau * i / seg;
            pts[i] = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
        }
        return MakePolygon(color, pts);
    }

    /// Spiky hair — a jagged crown sitting on top of the head.
    private static Polygon2D MakeHair(Color color)
    {
        return MakePolygon(color, new Vector2[]
        {
            new(-11f, 2f), new(-12f, -8f), new(-7f, -6f), new(-9f, -16f),
            new(-3f, -8f), new(-2f, -19f), new(3f, -9f), new(6f, -17f),
            new(7f, -7f), new(12f, -11f), new(11f, -2f), new(11f, 3f),
        });
    }

    private static Color Darken(Color c, float f) => new(c.R * f, c.G * f, c.B * f, c.A);
    private static Color Lighten(Color c, float f) =>
        new(Mathf.Min(c.R * f, 1f), Mathf.Min(c.G * f, 1f), Mathf.Min(c.B * f, 1f), c.A);
}
