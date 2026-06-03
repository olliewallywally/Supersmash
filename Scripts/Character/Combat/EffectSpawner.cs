using Godot;
using System.Collections.Generic;

namespace Supersmash;

/// <summary>
/// Static utility for spawning one-shot visual effects into the scene.
/// Maintains a C#-side cache of PackedScene handles so each resource path
/// is loaded from disk only once per session.
///
/// Usage:
///   EffectSpawner.Spawn("res://Scenes/Effects/HitSpark.tscn", parent, pos, rot);
///
/// The returned node is parented under <paramref name="parent"/>. For effects
/// that must persist independently of both fighters, pass the scene root:
///   EffectSpawner.Spawn(path, character.GetTree().CurrentScene, globalPos);
/// </summary>
public static class EffectSpawner
{
    private static readonly Dictionary<string, PackedScene> _cache = new();

    /// <summary>
    /// Instantiate the scene at <paramref name="scenePath"/>, add it under
    /// <paramref name="parent"/>, and place it at <paramref name="globalPos"/>.
    /// Returns the spawned node, or <c>null</c> if the resource fails to load.
    /// </summary>
    public static Node2D? Spawn(string scenePath, Node parent, Vector2 globalPos, float rotation = 0f)
    {
        if (!_cache.TryGetValue(scenePath, out var packed))
        {
            packed = ResourceLoader.Load<PackedScene>(scenePath);
            if (packed is null)
            {
                GD.PushWarning($"[EffectSpawner] No scene at '{scenePath}'.");
                return null;
            }
            _cache[scenePath] = packed;
        }

        var instance = packed.Instantiate<Node2D>();
        parent.AddChild(instance);
        instance.GlobalPosition = globalPos;
        instance.Rotation       = rotation;
        return instance;
    }

    /// Release cached PackedScene references. Call on scene transition to free memory.
    public static void ClearCache() => _cache.Clear();
}
