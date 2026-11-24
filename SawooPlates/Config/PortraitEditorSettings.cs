namespace SawooPlates.Config;

public class PortraitEditorSettings
{
    public List<SavedPreset> Presets = [];
    public List<SavedPresetTag> PresetTags = [];
    public bool ShowAlignmentTool = false;
    public int AlignmentToolVerticalLines = 2;
    public Vector4 AlignmentToolVerticalColor = new(0, 0, 0, 1f);
    public int AlignmentToolHorizontalLines = 2;
    public Vector4 AlignmentToolHorizontalColor = new(0, 0, 0, 1f);

    public bool EmbedPresetStringInThumbnails = true;
    public bool NotifyGearChecksumMismatch = true;
    public bool ReequipGearsetOnUpdate = false;
    public bool AutoUpdatePotraitOnGearUpdate = false;
    public bool IgnoreDoHDoL = false;
}
