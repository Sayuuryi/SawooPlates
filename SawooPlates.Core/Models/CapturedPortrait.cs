using System.Text.Json.Serialization;
using SawooPlates.Core.Presets;

namespace SawooPlates.Core.Models;

public enum CaptureSource
{
    AdventurerPlate,
    PartyPortrait,
    MvpVote,
}

/// <summary>A portrait read from another player's plate or portrait screen.</summary>
public record CapturedPortrait
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTimeOffset Time { get; init; } = DateTimeOffset.UtcNow;
    public CaptureSource Source { get; init; }

    public ulong ContentId { get; init; }
    public string Name { get; init; } = string.Empty;
    public ushort WorldId { get; init; }
    public string WorldName { get; init; } = string.Empty;
    public string Job { get; init; } = string.Empty;
    public byte JobId { get; init; }
    public string Title { get; init; } = string.Empty;

    public PortraitPresetData Preset { get; init; } = new();

    /// <summary>False when the source does not store the frame (party/MVP portraits).</summary>
    public bool FrameKnown { get; init; } = true;

    /// <summary>False when the source does not store the accent/decoration (party/MVP portraits).</summary>
    public bool DecorationKnown { get; init; } = true;

    /// <summary>The Adventurer Plate design around the portrait. Null for party/MVP portraits.</summary>
    public PlateDesignData? Plate { get; set; }

    /// <summary>Gear and dyes the character wears in the portrait. Empty until read.</summary>
    public List<GearPiece> Gear { get; set; } = [];

    public bool HasImage { get; set; }

    /// <summary>Identifies the player: content id when known, otherwise name and world.</summary>
    [JsonIgnore]
    public string PlayerKey => ContentId != 0 ? ContentId.ToString("X") : $"{Name}@{WorldId}";

    [JsonIgnore]
    public string DefaultPresetName => string.IsNullOrEmpty(Job)
        ? $"{Name}@{WorldName}"
        : $"{Name}@{WorldName} – {Job}";
}

/// <summary>Adventurer Plate design: CharaCardBase, CharaCardHeader and CharaCardDecoration row ids.</summary>
public record PlateDesignData
{
    public ushort BasePlate { get; init; }
    public byte TopBorder { get; init; }
    public byte BottomBorder { get; init; }
    public ushort[] Decorations { get; init; } = [];
    public bool PortraitOnRight { get; init; }
}

/// <summary>One equipped item as shown in a portrait.</summary>
public record GearPiece
{
    public byte Slot { get; init; }
    public uint ItemId { get; init; }
    public byte Stain0 { get; init; }
    public byte Stain1 { get; init; }
}
