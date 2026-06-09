namespace Supersmash;

/// <summary>
/// Match settings carried from the menu into Match.tscn. Plain static state —
/// it survives ChangeSceneToFile because the .NET assembly outlives the scene tree.
/// </summary>
public static class GameConfig
{
    /// One entry per playable archetype: display name + data/library resource paths.
    public record Archetype(string Name, string DataPath, string LibraryPath);

    public static readonly Archetype[] Roster =
    {
        new("Rushdown",
            "res://Resources/Characters/Rushdown/CharacterData.tres",
            "res://Resources/Characters/Rushdown/AttackLibrary.tres"),
        new("Swordfighter",
            "res://Resources/Characters/Swordfighter/CharacterData.tres",
            "res://Resources/Characters/Swordfighter/AttackLibrary.tres"),
        new("Falco",
            "res://Resources/Characters/Falco/CharacterData.tres",
            "res://Resources/Characters/Falco/AttackLibrary.tres"),
        new("Captain Falcon",
            "res://Resources/Characters/CaptainFalcon/CharacterData.tres",
            "res://Resources/Characters/CaptainFalcon/AttackLibrary.tres"),
        new("Bruiser",
            "res://Resources/Characters/Bruiser/CharacterData.tres",
            "res://Resources/Characters/Bruiser/AttackLibrary.tres"),
        new("Base Fighter",
            "res://Resources/Characters/BaseFighter/CharacterData.tres",
            "res://Resources/Characters/BaseFighter/AttackLibrary.tres"),
    };

    /// Roster index selected for each player slot.
    public static int Player0Selection { get; set; } = 0;
    public static int Player1Selection { get; set; } = 1;

    /// When true, Player 1 is a stationary training dummy (no input device).
    public static bool Player1IsDummy { get; set; } = true;

    public static int StartingStocks { get; set; } = 3;
}
