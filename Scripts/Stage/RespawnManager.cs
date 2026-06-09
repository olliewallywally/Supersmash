using Godot;
using System.Collections.Generic;

namespace Supersmash;

/// <summary>
/// Manages stocks, receives death events, and coordinates respawn sequences.
/// Place one instance in the main game scene.
///
/// Required setup in Godot:
///   1. Add a Node to the scene root. Attach this script.
///   2. Drag each CharacterController into Players[].
///   3. Add Node2D "spawn point" nodes to the stage and drag them into SpawnPoints[].
///      SpawnPoints[0] → Player 0's spawn, SpawnPoints[1] → Player 1's, etc.
///      Spawn points should be above the main platform, high enough to give
///      a brief falling arc before landing (~400–600px above floor).
///   4. Set StartingStocks (default 3).
///
/// Signals:
///   PlayerDied(playerIndex, stocksRemaining)  — fired each death
///   PlayerEliminated(playerIndex)             — fired at 0 stocks
///   GameOver(winnerPlayerIndex)               — –1 if all eliminated simultaneously
/// </summary>
public partial class RespawnManager : Node
{
    [ExportGroup("Match Settings")]
    [Export] public int StartingStocks { get; set; } = 3;

    [ExportGroup("Wiring")]
    [Export] public CharacterController[] Players    { get; set; } = System.Array.Empty<CharacterController>();
    [Export] public Node2D[]              SpawnPoints { get; set; } = System.Array.Empty<Node2D>();

    // ── Signals ───────────────────────────────────────────────────────────────

    [Signal] public delegate void PlayerDiedEventHandler(int playerIndex, int stocksRemaining);
    [Signal] public delegate void PlayerEliminatedEventHandler(int playerIndex);
    /// winnerPlayerIndex is –1 on a simultaneous last-stock elimination (rare draw).
    [Signal] public delegate void GameOverEventHandler(int winnerPlayerIndex);

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly Dictionary<int, int> _stocks = new();

    public int GetStocks(int playerIndex) =>
        _stocks.TryGetValue(playerIndex, out int s) ? s : 0;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        foreach (var character in Players)
        {
            if (character is null) continue;
            _stocks[character.PlayerIndex] = StartingStocks;
            character.Died += () => OnPlayerDied(character);
        }
    }

    // ── Death handler ─────────────────────────────────────────────────────────

    private void OnPlayerDied(CharacterController character)
    {
        int idx = character.PlayerIndex;

        // Damage percent resets with the lost stock — Smash convention.
        character.Combat.ResetDamage();

        _stocks[idx] = Mathf.Max(0, _stocks[idx] - 1);
        EmitSignal(SignalName.PlayerDied, idx, _stocks[idx]);

        if (_stocks[idx] <= 0)
        {
            EmitSignal(SignalName.PlayerEliminated, idx);
            CheckGameOver();
            // Eliminated players are not respawned. Remove them from the active scene
            // or put them in a permanent Eliminated state as a future extension.
            return;
        }

        // Initiate respawn: transition the character's FSM to RespawnState with the
        // target spawn position embedded in the message dictionary.
        Vector2 spawnPos = GetSpawnPosition(idx);
        character.FSM.TransitionTo("RespawnState", new Godot.Collections.Dictionary
        {
            { "spawn_position", spawnPos },
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Vector2 GetSpawnPosition(int playerIndex)
    {
        if (playerIndex < SpawnPoints.Length && SpawnPoints[playerIndex] is Node2D point)
            return point.GlobalPosition;

        // Fallback: symmetrical default positions above centre stage.
        // Replace this with real spawn points as soon as the stage scene exists.
        float offsetX = playerIndex == 0 ? -160f : 160f;
        return new Vector2(offsetX, -480f);
    }

    private void CheckGameOver()
    {
        var alive = new List<int>();
        foreach (var kvp in _stocks)
            if (kvp.Value > 0) alive.Add(kvp.Key);

        if (alive.Count == 1)
            EmitSignal(SignalName.GameOver, alive[0]);
        else if (alive.Count == 0)
            EmitSignal(SignalName.GameOver, -1);
    }
}
