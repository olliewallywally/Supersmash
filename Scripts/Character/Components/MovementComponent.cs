using Godot;

namespace Supersmash;

/// <summary>
/// Pure physics helpers. Stateless — every method takes values in and returns
/// a new Vector2, so states remain in full control of when each is applied.
///
/// All velocity values are in pixels/second, matching Godot's coordinate space.
/// </summary>
public partial class MovementComponent : Node
{
    // ── Gravity & Fall ────────────────────────────────────────────────────────

    /// Apply gravity to the Y axis, clamped to the appropriate terminal velocity.
    public static Vector2 ApplyGravity(Vector2 vel, CharacterData data, bool isFastFalling, double delta)
    {
        float gravity    = data.Gravity * (isFastFalling ? data.FastFallMultiplier : 1f);
        float maxFall    = isFastFalling ? data.FastFallMaxSpeed : data.MaxFallSpeed;
        float newY       = Mathf.MoveToward(vel.Y, maxFall, gravity * (float)delta);
        return new Vector2(vel.X, newY);
    }

    // ── Ground Movement ───────────────────────────────────────────────────────

    /// Decelerate horizontal velocity toward zero using the character's ground traction.
    /// Uses a proportional lerp so deceleration feels snappy at high speed and
    /// smooth near zero — avoids the "sliding on ice" feel of a fixed subtraction.
    public static Vector2 ApplyGroundFriction(Vector2 vel, CharacterData data)
    {
        float newX = Mathf.Lerp(vel.X, 0f, data.GroundFriction);
        if (Mathf.Abs(newX) < 5f) newX = 0f; // snap to rest below threshold
        return new Vector2(newX, vel.Y);
    }

    /// Accelerate toward target run speed in the requested direction.
    public static Vector2 ApplyGroundAcceleration(Vector2 vel, float inputX, CharacterData data)
    {
        float target = inputX * data.RunSpeed;
        // Use turnaround friction when reversing direction mid-run.
        float friction = (vel.X != 0f && Mathf.Sign(inputX) != Mathf.Sign(vel.X))
            ? data.TurnaroundFriction
            : data.GroundFriction;
        float newX = Mathf.Lerp(vel.X, target, friction);
        return new Vector2(newX, vel.Y);
    }

    // ── Air Movement ─────────────────────────────────────────────────────────

    /// <summary>
    /// Unified horizontal air physics: passive friction + input-driven drift, in one pass.
    /// Call once per tick from any airborne state (JumpState, FallState, …).
    ///
    /// Key behaviour — over-speed is allowed and decays:
    ///   • Ground momentum carried into a jump can exceed AirSpeed (the drift cap).
    ///   • Passive friction (AirFriction) ALWAYS pulls toward zero, so that carried
    ///     over-speed bleeds back down toward the AirSpeed budget over time.
    ///   • Drift input accelerates toward (±AirSpeed) but can NEVER push magnitude
    ///     further out once you're inside the [-AirSpeed, AirSpeed] band, and gives no
    ///     extra boost while you're already over-speed in that same direction.
    ///   • Drift opposing an over-speed is allowed full effect, so you can actively
    ///     kill carried momentum faster than friction alone.
    /// </summary>
    public static Vector2 ApplyAirMovement(Vector2 vel, float inputX, CharacterData data)
    {
        float maxAir = data.AirSpeed;

        // 1. Passive decay toward zero — the mechanism that bleeds off over-speed.
        float vx = Mathf.Lerp(vel.X, 0f, data.AirFriction);

        // 2. Input-driven drift, gated so it never inflates speed past the budget.
        if (Mathf.Abs(inputX) > 0.1f)
        {
            float target  = inputX * maxAir;
            float drifted = Mathf.Lerp(vx, target, data.AirAcceleration);

            bool overSpeed   = Mathf.Abs(vx) > maxAir;
            bool sameDir     = Mathf.Sign(inputX) == Mathf.Sign(vx);

            if (!overSpeed)
                vx = Mathf.Clamp(drifted, -maxAir, maxAir); // normal in-band drift
            else if (!sameDir)
                vx = drifted;                               // opposing input: pull inward freely
            // else: holding into the over-speed — friction alone governs, no boost.
        }

        return new Vector2(vx, vel.Y);
    }

    // ── Directional Influence ─────────────────────────────────────────────────

    /// Rotate a knockback velocity vector based on the defender's DI input.
    ///
    /// DI works by rotating the launch vector up to MaxDiAngle degrees
    /// toward the direction perpendicular to the knockback. Holding directly
    /// into or away from the knockback has zero effect; holding perpendicular
    /// has maximum effect. This matches Melee / Ultimate behaviour.
    public static Vector2 ApplyDI(Vector2 knockbackVelocity, Vector2 diInput)
    {
        const float MaxDiAngle = 18f; // degrees, standard platform-fighter value

        if (diInput.LengthSquared() < 0.01f || knockbackVelocity.LengthSquared() < 0.01f)
            return knockbackVelocity;

        Vector2 kbNorm = knockbackVelocity.Normalized();
        Vector2 diNorm = diInput.Normalized();

        // dot = 1 → DI directly into/away from knockback (no effect)
        // dot = 0 → DI perpendicular to knockback (maximum rotation)
        float parallelComponent = kbNorm.Dot(diNorm);
        float diEffectiveness   = 1f - Mathf.Abs(parallelComponent);

        // Cross product (z-component) gives rotation sign.
        float rotSign   = kbNorm.Cross(diNorm);
        float rotAmount = Mathf.DegToRad(MaxDiAngle) * diEffectiveness * Mathf.Sign(rotSign);

        return knockbackVelocity.Rotated(rotAmount);
    }
}
