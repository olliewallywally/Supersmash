using Godot;

namespace Supersmash;

/// <summary>
/// Title screen + character select in one. Populates the two archetype pickers
/// from GameConfig.Roster, writes the selections back to GameConfig, and starts
/// the match. UI nodes live in MainMenu.tscn; this script only wires them.
/// </summary>
public partial class MainMenu : Control
{
    private OptionButton _p0Picker = null!;
    private OptionButton _p1Picker = null!;
    private CheckBox     _dummyToggle = null!;
    private SpinBox      _stocksBox = null!;

    public override void _Ready()
    {
        _p0Picker    = GetNode<OptionButton>("%P0Picker");
        _p1Picker    = GetNode<OptionButton>("%P1Picker");
        _dummyToggle = GetNode<CheckBox>("%DummyToggle");
        _stocksBox   = GetNode<SpinBox>("%StocksBox");

        foreach (var archetype in GameConfig.Roster)
        {
            _p0Picker.AddItem(archetype.Name);
            _p1Picker.AddItem(archetype.Name);
        }

        // Restore previous selections so re-matches keep the same picks.
        _p0Picker.Selected        = GameConfig.Player0Selection;
        _p1Picker.Selected        = GameConfig.Player1Selection;
        _dummyToggle.ButtonPressed = GameConfig.Player1IsDummy;
        _stocksBox.Value          = GameConfig.StartingStocks;

        GetNode<Button>("%FightButton").Pressed += OnFightPressed;
        GetNode<Button>("%QuitButton").Pressed  += () => GetTree().Quit();
    }

    private void OnFightPressed()
    {
        GameConfig.Player0Selection = _p0Picker.Selected;
        GameConfig.Player1Selection = _p1Picker.Selected;
        GameConfig.Player1IsDummy   = _dummyToggle.ButtonPressed;
        GameConfig.StartingStocks   = (int)_stocksBox.Value;

        GetTree().ChangeSceneToFile("res://Scenes/Match.tscn");
    }
}
