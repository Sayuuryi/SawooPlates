namespace SawooPlates.Common;

/// <summary>Links to Garland Tools pages. Quest, item and duty ids are the game's own.</summary>
public static class Garland
{
    private const string Base = "https://www.garlandtools.org/db/#";

    public static string Quest(uint questId) => $"{Base}quest/{questId}";
    public static string Item(uint itemId) => $"{Base}item/{itemId}";

    /// <summary>Duties use the ContentFinderCondition id.</summary>
    public static string Instance(uint contentFinderConditionId) => $"{Base}instance/{contentFinderConditionId}";
}
