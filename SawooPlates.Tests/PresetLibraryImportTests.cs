using SawooPlates.Core.Import;
using SawooPlates.Core.Presets;
using SawooPlates.Core.Models;

namespace SawooPlates.Tests;

public class PresetLibraryImportTests
{
    private const string CodeA = "SFRQUwEA2DMiNJM7ADyeKD4o8igAPPr/yAEAbxKDOg0AAAAAcrGCLf///xqFALP/MzMzf3UBSgBJAQ==";
    private const string CodeB = "SFRQUwEADjufrNs1ADxfrCsqba0APPr/yAMAwcrEQQGwsh8zk7UQKv///xqFALP/FBUPOHUBIQFHAQ==";

    // Shape of the config file the presets are read from (Version 9), trimmed to the relevant parts.
    private const string SampleConfig = $$"""
    {
      "Version": 9,
      "EnabledTweaks": [ "Commands", "PortraitHelper" ],
      "Tweaks": {
        "AutoSorter": { "Settings": [] },
        "PortraitHelper": {
          "Presets": [
            { "Id": "01a1f4c8-11db-77f1-805a-f106839f1aff", "Name": "Adventurer Plate", "Preset": "{{CodeA}}", "Tags": [ "11111111-1111-1111-1111-111111111111" ] },
            { "Id": "01996279-be4b-7b1d-866f-67d40e687efe", "Name": "Healer", "Preset": "{{CodeB}}", "Tags": [] },
            { "Id": "01996db1-cc34-74cf-bd6d-acca4fa657d9", "Name": "Broken", "Preset": "not a code", "Tags": [] }
          ],
          "PresetTags": [
            { "Id": "11111111-1111-1111-1111-111111111111", "Name": "Main" }
          ],
          "ShowAlignmentTool": false,
          "AlignmentToolVerticalColor": { "X": 0, "Y": 0, "Z": 0, "W": 1 }
        }
      }
    }
    """;

    [Fact]
    public void ParsesPresetsTagsAndStatus()
    {
        var library = PresetLibraryFile.Parse(SampleConfig);

        Assert.Equal(9, library.ConfigVersion);
        Assert.True(library.PortraitHelperEnabled);
        Assert.Equal(2, library.Presets.Count);
        Assert.Equal(1, library.SkippedPresets);
        Assert.Equal(CodeA, library.Presets[0].Preset!.ToExportedString());
        Assert.Equal(Guid.Parse("01a1f4c8-11db-77f1-805a-f106839f1aff"), library.Presets[0].Id);
        Assert.Equal("Main", Assert.Single(library.Tags).Name);
    }

    [Fact]
    public void EmptyOrMissingSectionGivesEmptyLibrary()
    {
        var library = PresetLibraryFile.Parse("""{ "Version": 9, "EnabledTweaks": [], "Tweaks": {} }""");

        Assert.Empty(library.Presets);
        Assert.False(library.PortraitHelperEnabled);
    }

    [Fact]
    public void MergeAddsPresetsKeepsIdsAndCreatesTags()
    {
        var library = PresetLibraryFile.Parse(SampleConfig);
        var presets = new List<SavedPreset>();
        var tags = new List<SavedPresetTag>();

        var (result, added) = PresetLibraryMerger.Merge(library, presets, tags);

        Assert.Equal(2, result.Imported);
        Assert.Equal(1, result.TagsCreated);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(library.Presets.Select(p => p.Id), presets.Select(p => p.Id));
        Assert.Contains(tags[0].Id, presets[0].Tags);
        Assert.Equal(2, added.Count);
    }

    [Fact]
    public void MergeTwiceDoesNotDuplicate()
    {
        var library = PresetLibraryFile.Parse(SampleConfig);
        var presets = new List<SavedPreset>();
        var tags = new List<SavedPresetTag>();

        PresetLibraryMerger.Merge(library, presets, tags);
        var (result, _) = PresetLibraryMerger.Merge(library, presets, tags);

        Assert.Equal(0, result.Imported);
        Assert.Equal(2, result.AlreadyPresent);
        Assert.Equal(2, presets.Count);
        Assert.Single(tags);
    }

    [Fact]
    public void MergeMatchesTagsByNameAndSkipsSameNameAndCode()
    {
        var library = PresetLibraryFile.Parse(SampleConfig);
        var existingTag = new SavedPresetTag(Guid.NewGuid(), "main");
        var presets = new List<SavedPreset>
        {
            new(Guid.NewGuid(), "Healer", PortraitPresetData.FromExportedString(CodeB), []),
        };
        var tags = new List<SavedPresetTag> { existingTag };

        var (result, _) = PresetLibraryMerger.Merge(library, presets, tags);

        Assert.Equal(1, result.Imported); // "Healer" already there with the same code
        Assert.Equal(0, result.TagsCreated);
        Assert.Contains(existingTag.Id, presets.Single(p => p.Name == "Adventurer Plate").Tags);
    }

    [Fact]
    public void MergeDoesNotChangeSourceLibrary()
    {
        var library = PresetLibraryFile.Parse(SampleConfig);
        var before = library.Presets.Select(p => (p.Id, p.Name, p.Preset!.ToExportedString(), p.Tags.Count)).ToList();

        PresetLibraryMerger.Merge(library, [], [new SavedPresetTag(Guid.NewGuid(), "Main")]);

        Assert.Equal(before, library.Presets.Select(p => (p.Id, p.Name, p.Preset!.ToExportedString(), p.Tags.Count)));
    }
}
