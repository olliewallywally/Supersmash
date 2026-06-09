using Godot;

namespace Supersmash;

/// <summary>
/// Root of Match.tscn. Spawns both fighters from GameConfig, wires every
/// cross-cutting system (camera, respawn manager, HUD), and handles the
/// match-over flow.
///
/// Expected scene tree:
///   Match (Node2D)              ← this script
///   ├── Stage  (Stage.tscn instance: platforms, blast zones, spawn points, Camera)
///   ├── HitstopManager
///   ├── RespawnManager
///   └── Hud (CanvasLayer, MatchHud)
///
/// Fighters are spawned in code — not placed in the scene — because their
/// archetype (CharacterData + AttackLibrary) comes from the character select.
/// </summary>
public partial class MatchManager : Node2D
{
    [Export] public PackedScene FighterScene { get; set; } = null!;

    private RespawnManager _respawn = null!;
    private MatchHud       _hud     = null!;
    private bool           _matchOver;

    public override void _Ready()
    {
        _respawn = GetNode<RespawnManager>("RespawnManager");
        _hud     = GetNode<MatchHud>("Hud");

        var stage  = GetNode<Node2D>("Stage");
        var camera = stage.GetNode<DynamicCamera2D>("Camera");
        var spawn0 = stage.GetNode<Marker2D>("SpawnPoint0");
        var spawn1 = stage.GetNode<Marker2D>("SpawnPoint1");

        // ── Spawn fighters from the menu selection ────────────────────────────
        var p0 = SpawnFighter(0, GameConfig.Player0Selection, spawn0.GlobalPosition, facing:  1);
        var p1 = SpawnFighter(1, GameConfig.Player1Selection, spawn1.GlobalPosition, facing: -1);

        if (GameConfig.Player1IsDummy)
            p1.Input.IsAI = true;

        // ── Wire the systems ──────────────────────────────────────────────────
        var players = new[] { p0, p1 };
        camera.Players        = players;
        _respawn.Players      = players;
        _respawn.SpawnPoints  = new Node2D[] { spawn0, spawn1 };
        _respawn.StartingStocks = GameConfig.StartingStocks;

        _hud.Initialize(players, _respawn);
        _respawn.GameOver += OnGameOver;
    }

    private CharacterController SpawnFighter(int playerIndex, int rosterIndex, Vector2 position, int facing)
    {
        var pick = GameConfig.Roster[Mathf.Clamp(rosterIndex, 0, GameConfig.Roster.Length - 1)];

        var fighter = FighterScene.Instantiate<CharacterController>();
        fighter.Data        = GD.Load<CharacterData>(pick.DataPath);
        fighter.Attacks     = GD.Load<AttackLibrary>(pick.LibraryPath);
        fighter.PlayerIndex = playerIndex;

        AddChild(fighter);
        fighter.GlobalPosition  = position;
        fighter.FacingDirection = facing;
        return fighter;
    }

    // ── Match-over flow ─────────────────────────────────────────────────────────

    private void OnGameOver(int winnerPlayerIndex)
    {
        if (_matchOver) return;
        _matchOver = true;
        _hud.ShowGameOver(winnerPlayerIndex);

        // Slow-motion finish, then return to the menu after a short hold.
        Engine.TimeScale = 0.3;
        GetTree().CreateTimer(2.5).Timeout += ReturnToMenu;
    }

    private void ReturnToMenu()
    {
        Engine.TimeScale = 1.0;
        GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu.tscn");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Esc bails back to the menu at any point.
        if (@event.IsActionPressed("ui_cancel"))
        {
            Engine.TimeScale = 1.0;
            GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu.tscn");
        }
    }
}
