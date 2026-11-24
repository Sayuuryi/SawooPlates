using SawooPlates.Core.History;
using SawooPlates.Core.Presets;
using SawooPlates.Core.Models;

namespace SawooPlates.Tests;

public class CaptureHistoryTests
{
    private static CapturedPortrait Capture(string name, ushort bannerBg, ulong contentId = 0) => new()
    {
        Name = name,
        WorldId = 73,
        WorldName = "Adamantoise",
        ContentId = contentId,
        Preset = new PortraitPresetData { BannerBg = bannerBg },
    };

    [Fact]
    public void NewestFirst()
    {
        var history = new CaptureHistory();
        history.Add(Capture("A", 1), dedupe: true, maxSize: 10);
        history.Add(Capture("B", 1), dedupe: true, maxSize: 10);

        Assert.Equal(["B", "A"], history.Entries.Select(e => e.Name));
    }

    [Fact]
    public void DedupesSamePlayerAndPreset()
    {
        var history = new CaptureHistory();
        var first = history.Add(Capture("A", 1), dedupe: true, maxSize: 10).Entry;
        history.Add(Capture("B", 1), dedupe: true, maxSize: 10);

        var (result, entry, _) = history.Add(Capture("A", 1), dedupe: true, maxSize: 10);

        Assert.Equal(AddResult.Duplicate, result);
        Assert.Equal(first.Id, entry.Id); // keeps id (and so its image)
        Assert.Equal(["A", "B"], history.Entries.Select(e => e.Name)); // moved to top
    }

    [Fact]
    public void DifferentPresetIsNotADuplicate()
    {
        var history = new CaptureHistory();
        history.Add(Capture("A", 1), dedupe: true, maxSize: 10);

        Assert.Equal(AddResult.Added, history.Add(Capture("A", 2), dedupe: true, maxSize: 10).Result);
        Assert.Equal(2, history.Entries.Count);
    }

    [Fact]
    public void ContentIdIdentifiesPlayerOverName()
    {
        var history = new CaptureHistory();
        history.Add(Capture("Old Name", 1, contentId: 42), dedupe: true, maxSize: 10);

        // renamed character, same content id and preset
        Assert.Equal(AddResult.Duplicate, history.Add(Capture("New Name", 1, contentId: 42), dedupe: true, maxSize: 10).Result);
    }

    [Fact]
    public void DedupeOffKeepsBoth()
    {
        var history = new CaptureHistory();
        history.Add(Capture("A", 1), dedupe: false, maxSize: 10);
        history.Add(Capture("A", 1), dedupe: false, maxSize: 10);

        Assert.Equal(2, history.Entries.Count);
    }

    [Fact]
    public void TrimsToMaxSizeAndReturnsRemoved()
    {
        var history = new CaptureHistory();
        history.Add(Capture("A", 1), true, 2);
        history.Add(Capture("B", 2), true, 2);

        var (_, _, removed) = history.Add(Capture("C", 3), true, 2);

        Assert.Equal(["C", "B"], history.Entries.Select(e => e.Name));
        Assert.Equal(["A"], removed.Select(e => e.Name));
    }

    [Fact]
    public void JsonRoundTrip()
    {
        var history = new CaptureHistory();
        var capture = Capture("A", 0x175) with { Source = CaptureSource.PartyPortrait, FrameKnown = false, Job = "WHM", Title = "the Fabled" };
        history.Add(capture, true, 10);

        var loaded = CaptureHistory.FromJson(history.ToJson());
        var entry = Assert.Single(loaded.Entries);

        Assert.Equal(capture.Id, entry.Id);
        Assert.Equal(CaptureSource.PartyPortrait, entry.Source);
        Assert.False(entry.FrameKnown);
        Assert.Equal(capture.Preset, entry.Preset);
        Assert.Contains("\"PartyPortrait\"", history.ToJson());
    }

    [Fact]
    public void DefaultPresetName()
    {
        Assert.Equal("A@Adamantoise – WHM", (Capture("A", 1) with { Job = "WHM" }).DefaultPresetName);
        Assert.Equal("A@Adamantoise", Capture("A", 1).DefaultPresetName);
    }

    [Fact]
    public void DuplicateKeepsNewPlateAndGear()
    {
        var history = new CaptureHistory();
        history.Add(Capture("A", 1), true, 10);

        var plate = new PlateDesignData { BasePlate = 3, TopBorder = 1, BottomBorder = 2, Decorations = [5, 6, 0, 0, 0] };
        var gear = new List<GearPiece> { new() { Slot = 2, ItemId = 1234, Stain0 = 7 } };
        var (_, entry, _) = history.Add(Capture("A", 1) with { Plate = plate, Gear = gear, JobId = 24 }, true, 10);

        Assert.Equal(plate, entry.Plate);
        Assert.Single(entry.Gear);
        Assert.Equal(24, entry.JobId);
    }

    [Fact]
    public void SetGearAndJsonRoundTripOfPlateAndGear()
    {
        var history = new CaptureHistory();
        var entry = history.Add(Capture("A", 1) with { Plate = new PlateDesignData { BasePlate = 9, Decorations = [1, 2, 3, 4, 5], PortraitOnRight = true } }, true, 10).Entry;

        Assert.True(history.SetGear(entry.Id, [new GearPiece { Slot = 3, ItemId = 42, Stain0 = 1, Stain1 = 2 }]));

        var loaded = Assert.Single(CaptureHistory.FromJson(history.ToJson()).Entries);
        Assert.Equal(42u, Assert.Single(loaded.Gear).ItemId);
        Assert.True(loaded.Plate!.PortraitOnRight);
        Assert.Equal(new ushort[] { 1, 2, 3, 4, 5 }, loaded.Plate.Decorations);
    }
}
