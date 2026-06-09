using Godot;

namespace Supersmash;

/// <summary>
/// In-match HUD: a damage-percent readout and stock pips per player, built in
/// code at match start (no .tscn layout to maintain), plus the game-over banner.
///
/// Percent colour shifts white → yellow → red as damage climbs, matching the
/// Smash convention so danger is readable at a glance.
/// </summary>
public partial class MatchHud : CanvasLayer
{
    private static readonly Color[] PlayerColors =
    {
        new(0.95f, 0.35f, 0.25f),   // P1 red
        new(0.30f, 0.55f, 0.95f),   // P2 blue
    };

    private CharacterController[] _players = System.Array.Empty<CharacterController>();
    private RespawnManager?       _respawn;

    private Label[]   _percentLabels = System.Array.Empty<Label>();
    private Label[]   _stockLabels   = System.Array.Empty<Label>();
    private Label     _banner        = null!;

    public void Initialize(CharacterController[] players, RespawnManager respawn)
    {
        _players = players;
        _respawn = respawn;

        _percentLabels = new Label[players.Length];
        _stockLabels   = new Label[players.Length];

        var row = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        row.AddThemeConstantOverride("separation", 180);
        AddChild(row);
        row.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        row.OffsetTop    = -150f;
        row.OffsetBottom = -30f;

        for (int i = 0; i < players.Length; i++)
        {
            row.AddChild(BuildPlayerPanel(i));

            int idx = i; // capture per-player index, not the loop variable
            players[i].Combat.DamageReceived += (_, newTotal) => UpdatePercent(idx, newTotal);
        }

        respawn.PlayerDied += OnPlayerDied;

        // Game-over banner (hidden until the match ends).
        _banner = new Label
        {
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _banner.AddThemeFontSizeOverride("font_size", 64);
        _banner.AddThemeColorOverride("font_outline_color", Colors.Black);
        _banner.AddThemeConstantOverride("outline_size", 10);
        AddChild(_banner);
        _banner.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _banner.OffsetTop = 120f;

        RefreshStocks();
    }

    private Control BuildPlayerPanel(int playerIndex)
    {
        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };

        var name = new Label
        {
            Text = $"P{playerIndex + 1}",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        name.AddThemeFontSizeOverride("font_size", 20);
        name.AddThemeColorOverride("font_color", PlayerColors[playerIndex % PlayerColors.Length]);
        box.AddChild(name);

        var percent = new Label
        {
            Text = "0%",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        percent.AddThemeFontSizeOverride("font_size", 44);
        percent.AddThemeColorOverride("font_outline_color", Colors.Black);
        percent.AddThemeConstantOverride("outline_size", 8);
        box.AddChild(percent);
        _percentLabels[playerIndex] = percent;

        var stocks = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        stocks.AddThemeFontSizeOverride("font_size", 22);
        stocks.AddThemeColorOverride("font_color", PlayerColors[playerIndex % PlayerColors.Length]);
        box.AddChild(stocks);
        _stockLabels[playerIndex] = stocks;

        return box;
    }

    // ── Updates ─────────────────────────────────────────────────────────────────

    private void UpdatePercent(int playerIndex, float newTotal)
    {
        var label = _percentLabels[playerIndex];
        label.Text = $"{Mathf.Min(newTotal, 999f):0.#}%";

        // White → yellow → red danger ramp, saturating at 150%.
        float t = Mathf.Clamp(newTotal / 150f, 0f, 1f);
        Color c = t < 0.5f
            ? new Color(1f, 1f, 1f).Lerp(new Color(1f, 0.85f, 0.2f), t * 2f)
            : new Color(1f, 0.85f, 0.2f).Lerp(new Color(1f, 0.15f, 0.1f), (t - 0.5f) * 2f);
        label.AddThemeColorOverride("font_color", c);
    }

    private void OnPlayerDied(int playerIndex, int stocksRemaining)
    {
        // Percent resets on death (RespawnManager calls ResetDamage; no signal fires).
        if (playerIndex < _percentLabels.Length)
            UpdatePercent(playerIndex, 0f);
        RefreshStocks();
    }

    private void RefreshStocks()
    {
        if (_respawn is null) return;
        for (int i = 0; i < _stockLabels.Length; i++)
        {
            int stocks = _respawn.GetStocks(_players[i].PlayerIndex);
            _stockLabels[i].Text = stocks > 0 ? new string('●', stocks) : "OUT";
        }
    }

    public void ShowGameOver(int winnerPlayerIndex)
    {
        _banner.Text = winnerPlayerIndex >= 0 ? $"P{winnerPlayerIndex + 1} WINS!" : "DRAW!";
        _banner.AddThemeColorOverride("font_color",
            winnerPlayerIndex >= 0
                ? PlayerColors[winnerPlayerIndex % PlayerColors.Length]
                : Colors.White);
        _banner.Visible = true;
    }
}
