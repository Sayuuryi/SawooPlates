using System.Text.Json.Serialization;
using SawooPlates.Core.Presets;

namespace SawooPlates.Core.Models;

public record SavedPreset
{
    public Guid Id;
    public string Name;
    public PortraitPresetData? Preset;
    public HashSet<Guid> Tags;

    /// <summary>Plate design saved together with the portrait, when it came from an Adventurer Plate.</summary>
    public PlateDesignData? Plate;

    /// <summary>Gear and dyes shown in the portrait, when it was saved from Recent.</summary>
    public List<GearPiece>? Gear;

    [JsonConstructor]
    public SavedPreset(Guid Id, string Name, PortraitPresetData? Preset, HashSet<Guid> Tags)
    {
        this.Id = Id;
        this.Name = Name;
        this.Preset = Preset;
        this.Tags = Tags;
    }
}

public record SavedPresetTag
{
    public Guid Id;
    public string Name;

    [JsonConstructor]
    public SavedPresetTag(Guid Id, string Name)
    {
        this.Id = Id;
        this.Name = Name;
    }

    public SavedPresetTag(string Name) : this(Guid.NewGuid(), Name)
    {
    }
}
