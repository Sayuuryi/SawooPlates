namespace SawooPlates.Config;

public class CaptureSettings
{
    public bool AutoCaptureAdventurerPlate = false;
    public bool AutoCapturePartyPortraits = false;
    public bool AutoCaptureMvpVote = false;

    public bool SkipOwnCharacter = true;
    public bool DedupePerPlayer = true;
    public int MaxHistorySize = 200;

    public bool ShowPlateButton = true;
    public bool ShowPartyButtons = true;
    public bool ContextMenuEntry = true;

    public bool NotifyOnCapture = false;
    public bool NotifyOnCopy = true;
}
