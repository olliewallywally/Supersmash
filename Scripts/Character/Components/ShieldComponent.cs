using Godot;

namespace Supersmash;

/// <summary>
/// Tracks shield health and drives the shield bubble visual. Owned by the
/// character scene as a child node named "ShieldComponent"; resolved optionally
/// by CharacterController (a character with no shield node simply can't shield).
///
/// ── Health model ─────────────────────────────────────────────────────────────
///   • Drains by Data.ShieldDecayRate per tick while held (plus the damage of any
///     attack absorbed, applied as a burst via Chip()).
///   • Regenerates by Data.ShieldRegenRate per tick while not held.
///   • At 0 health the shield BREAKS — IsBroken latches true and ShieldState
///     transitions the character into a long stun.
///
/// ── Visual ───────────────────────────────────────────────────────────────────
///   The bubble (a Sprite2D assigned via export) scales with current health and
///   is shown only while shielding. Its alpha also fades as health drops.
/// </summary>
public partial class ShieldComponent : Node
{
    /// The shield bubble sprite. Hidden unless actively shielding.
    [Export] public Node2D? Bubble { get; set; }

    /// Largest scale the bubble reaches at full health.
    [Export] public float MaxBubbleScale { get; set; } = 1.0f;
    /// Smallest scale the bubble shrinks to just before breaking.
    [Export] public float MinBubbleScale { get; set; } = 0.35f;

    public float Health   { get; private set; }
    public bool  IsBroken { get; private set; }

    private CharacterData _data = null!;

    public override void _Ready()
    {
        var character = GetParentOrNull<CharacterController>();
        _data  = character?.Data ?? new CharacterData();
        Health = _data.ShieldMaxHealth;
        if (Bubble is not null) Bubble.Visible = false;
    }

    // ── Public API (called by ShieldState) ──────────────────────────────────────

    /// Begin showing the bubble. Call from ShieldState.Enter.
    public void Show()
    {
        if (Bubble is not null) Bubble.Visible = true;
        UpdateBubble();
    }

    /// Hide the bubble. Call from ShieldState.Exit.
    public void Hide()
    {
        if (Bubble is not null) Bubble.Visible = false;
    }

    /// Drain one tick of passive shield decay. Returns true if the shield just broke.
    public bool Drain()
    {
        if (IsBroken) return false;
        Health -= _data.ShieldDecayRate;
        if (Health <= 0f)
        {
            Health   = 0f;
            IsBroken = true;
            return true;
        }
        UpdateBubble();
        return false;
    }

    /// Apply a burst of shield damage from an absorbed attack.
    /// Returns true if the shield broke from this hit.
    public bool Chip(float amount)
    {
        if (IsBroken) return false;
        Health -= Mathf.Max(0f, amount);
        if (Health <= 0f)
        {
            Health   = 0f;
            IsBroken = true;
            return true;
        }
        UpdateBubble();
        return false;
    }

    /// Regenerate one tick of shield health (called while not shielding).
    public void Regen()
    {
        if (Health >= _data.ShieldMaxHealth) return;
        Health = Mathf.Min(_data.ShieldMaxHealth, Health + _data.ShieldRegenRate);
    }

    /// Clear the broken flag and restore full health (called after shield-break stun).
    public void Reset()
    {
        IsBroken = false;
        Health   = _data.ShieldMaxHealth;
    }

    // ── Internal ────────────────────────────────────────────────────────────────

    private void UpdateBubble()
    {
        if (Bubble is null) return;
        float t     = Mathf.Clamp(Health / _data.ShieldMaxHealth, 0f, 1f);
        float scale = Mathf.Lerp(MinBubbleScale, MaxBubbleScale, t);
        Bubble.Scale    = Vector2.One * scale;
        Bubble.Modulate = new Color(1f, 1f, 1f, Mathf.Lerp(0.25f, 0.6f, t));
    }
}
