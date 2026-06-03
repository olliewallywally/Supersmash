using Godot;
using Godot.Collections;
using System.Collections.Generic;

namespace Supersmash;

/// <summary>
/// A character's complete moveset as a single inspector-editable Resource.
/// Assign one .tres per fighter to CharacterController.Attacks.
///
/// Authoring: add elements to the Attacks array in the inspector; each element is
/// an AttackData sub-resource with its own AttackId ("Jab", "NeutralAir", …).
/// We expose an array (not a raw Dictionary) because Godot's inspector edits arrays
/// of sub-resources cleanly, whereas Dictionary<string, Resource> is clumsy to edit.
/// At runtime we build a name→AttackData dictionary once for O(1) lookups.
/// </summary>
[GlobalClass]
public partial class AttackLibrary : Resource
{
    [Export] public Array<AttackData> Attacks { get; set; } = new();

    // Built lazily on first lookup; null until then.
    private System.Collections.Generic.Dictionary<string, AttackData>? _lookup;

    /// <summary>Returns the AttackData for <paramref name="attackId"/>, or null if absent.</summary>
    public AttackData? Get(string attackId)
    {
        _lookup ??= BuildLookup();
        return _lookup.TryGetValue(attackId, out AttackData? data) ? data : null;
    }

    /// Call after editing Attacks at runtime (rare) to rebuild the lookup table.
    public void Invalidate() => _lookup = null;

    private System.Collections.Generic.Dictionary<string, AttackData> BuildLookup()
    {
        var map = new System.Collections.Generic.Dictionary<string, AttackData>();
        foreach (AttackData attack in Attacks)
        {
            if (attack is null || string.IsNullOrEmpty(attack.AttackId))
            {
                GD.PushWarning("[AttackLibrary] Skipping an attack with no AttackId.");
                continue;
            }
            if (!map.TryAdd(attack.AttackId, attack))
                GD.PushWarning($"[AttackLibrary] Duplicate AttackId '{attack.AttackId}' — keeping the first.");
        }
        return map;
    }
}
