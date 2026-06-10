using Godot;

namespace Supersmash;

public partial class PauseMenu : CanvasLayer
{
    private bool _paused;

    public override void _Ready()
    {
        Layer   = 20;
        Visible = false;
        ProcessMode = ProcessModeEnum.Always;
        BuildUi();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            TogglePause();
            GetViewport().SetInputAsHandled();
        }
    }

    private void TogglePause()
    {
        _paused = !_paused;
        GetTree().Paused = _paused;
        Visible = _paused;
    }

    // ── UI (built in code, no .tscn dependency) ───────────────────────────────

    private void BuildUi()
    {
        // Semi-transparent backdrop.
        var bg = new ColorRect
        {
            Color          = new Color(0f, 0f, 0f, 0.62f),
            AnchorRight    = 1f,
            AnchorBottom   = 1f,
            MouseFilter    = Control.MouseFilterEnum.Ignore,
        };
        AddChild(bg);

        // Centred panel.
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        panel.SizeFlagsVertical   = Control.SizeFlags.ShrinkCenter;
        AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);
        panel.AddChild(vbox);

        var title = new Label { Text = "PAUSED", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 48);
        title.AddThemeColorOverride("font_color", Colors.White);
        title.AddThemeColorOverride("font_outline_color", Colors.Black);
        title.AddThemeConstantOverride("outline_size", 8);
        vbox.AddChild(title);

        vbox.AddChild(new HSeparator());

        AddButton(vbox, "Resume",     OnResumePressed);
        AddButton(vbox, "Restart",    OnRestartPressed);
        AddButton(vbox, "Main Menu",  OnMenuPressed);
    }

    private static void AddButton(Container parent, string text, System.Action callback)
    {
        var btn = new Button
        {
            Text                  = text,
            CustomMinimumSize     = new Vector2(220, 48),
        };
        btn.AddThemeFontSizeOverride("font_size", 22);
        btn.Pressed += callback;
        parent.AddChild(btn);
    }

    private void OnResumePressed()  => TogglePause();

    private void OnRestartPressed()
    {
        GetTree().Paused = false;
        Engine.TimeScale = 1.0;
        _paused = false;
        GetTree().ReloadCurrentScene();
    }

    private void OnMenuPressed()
    {
        GetTree().Paused = false;
        Engine.TimeScale = 1.0;
        _paused = false;
        GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu.tscn");
    }
}
