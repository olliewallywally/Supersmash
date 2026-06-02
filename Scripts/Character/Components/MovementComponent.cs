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

    /// Drift toward target air speed. Air acceleration is intentionally lower than
    /// ground acceleration to preserve momentum from previous states (e.g., dash-off).
    public static Vector2 ApplyAirDrift(Vector2 vel, float inputX, CharacterData data)
    {
        float target = inputX * data.AirSpeed;
        float newX   = Mathf.Lerp(vel.X, target, data.AirAcceleration);
        newX = Mathf.Clamp(newX, -data.AirSpeed, data.AirSpeed);
        return new Vector2(newX, vel.Y);
    }

    /// Passive air friction when no horizontal input is held.
    public static Vector2 ApplyAirFriction(Vector2 vel, CharacterData data)
    {
        float newX = Mathf.Lerp(vel.X, 0f, data.AirFriction);
        return new Vector2(newX, vel.Y);
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
