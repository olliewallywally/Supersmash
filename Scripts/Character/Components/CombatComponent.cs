using Godot;

namespace Supersmash;

/// <summary>
/// Owns the damage counter and implements the knockback formula.
/// Hitbox/hurtbox collision detection lives in Hitbox.cs / Hurtbox.cs;
/// this component handles the *result* of a confirmed hit.
/// </summary>
public partial class CombatComponent : Node
{
    // ── Percent Counter ───────────────────────────────────────────────────────

    /// Current damage percentage. Range [0, ∞), but typically capped visually at 999%.
    public float DamagePercent { get; private set; } = 0f;

    [Signal] public delegate void DamageReceivedEventHandler(float amount, float newTotal);
    [Signal] public delegate void KnockbackAppliedEventHandler(Vector2 velocity);

    public void AddDamage(float amount)
    {
        DamagePercent += Mathf.Max(0f, amount);
        EmitSignal(SignalName.DamageReceived, amount, DamagePercent);
    }

    public void ResetDamage() => DamagePercent = 0f;

    // ── Knockback Formula ─────────────────────────────────────────────────────

    /// <summary>
    /// Calculates raw knockback magnitude from a confirmed hit.
    ///
    /// Derivation of the formula (Melee/Ultimate-inspired):
    ///
    ///   percentTerm = ((p/10) + (p * damage / 20)) * (200 / (weight + 100)) * 1.4 + 18
    ///   knockback   = percentTerm * (growthRate / 100) + baseKnockback
    ///
    /// Where:
    ///   p           = defender's current damage percent
    ///   damage      = hitbox's damage value
    ///   weight      = defender's weight stat
    ///   growthRate  = hitbox's KnockbackGrowth (scales how quickly kills happen)
    ///   baseKnockback = minimum knockback regardless of percent
    ///
    /// Returns a dimensionless magnitude. Multiply by KnockbackToVelocity's
    /// scale factor to convert to pixels/second.
    /// </summary>
    public float CalculateKnockback(HitboxData hitbox, CharacterData defenderData)
    {
        float p  = DamagePercent;
        float d  = hitbox.Damage;
        float w  = defenderData.Weight;

        float percentTerm = ((p / 10f) + (p * d / 20f))
                          * (200f / (w + 100f))
                          * 1.4f
                          + 18f;

        return percentTerm * (hitbox.KnockbackGrowth / 100f) + hitbox.BaseKnockback;
    }

    /// <summary>
    /// Converts a knockback magnitude and angle into a launch velocity vector,
    /// optionally mirroring the angle for a left-facing attacker.
    ///
    /// Angle convention: 0° = right, 90° = up, 270° = down (spike).
    /// Godot's Y-axis is inverted, so we negate the Y component.
    /// </summary>
    public static Vector2 KnockbackToVelocity(
        float magnitude,
        float angleDegrees,
        int   attackerFacing) // 1 = right, –1 = left
    {
        // Mirror angle when attacker faces left so attacks always launch away from the attacker.
        float angle = Mathf.DegToRad(attackerFacing >= 0 ? angleDegrees : 180f - angleDegrees);

        // Scale factor converts dimensionless KB units → pixels/sec.
        // Tune this to match your stage size; ~18 is a reasonable starting point.
        const float KbScale = 18f;

        return new Vector2(
             Mathf.Cos(angle) * magnitude * KbScale,
            -Mathf.Sin(angle) * magnitude * KbScale   // negate: up is negative Y in Godot
        );
    }

    // ── Hitstun ───────────────────────────────────────────────────────────────

    /// Returns the number of hitstun frames for this hit at the defender's current percent.
    /// Hitstun scales with knockback so high-percent hits grant longer punish windows.
    public static int CalculateHitstun(HitboxData hitbox, float knockbackMagnitude)
    {
        // Base hitstun from the data resource, plus a small bonus per KB unit.
        // The 0.4 coefficient is tunable; Melee uses ~0.4 * KB.
        return hitbox.HitstunFrames + Mathf.RoundToInt(knockbackMagnitude * 0.4f);
    }
}
