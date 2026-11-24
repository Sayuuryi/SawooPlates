using System.Text.Json;
using System.Text.Json.Nodes;
using SawooPlates.Core.Models;

namespace SawooPlates.Core.Import;

/// <summary>
/// Reads a portrait preset library from another plugin's config file (read-only).
/// Presets live under Tweaks.PortraitHelper.Presets / PresetTags; config Version 9 is the known layout.
/// </summary>
public sealed class PresetLibraryFile
{

    private static readonly JsonSerializerOptions SerializerOptions = new() { IncludeFields = true };

    public int ConfigVersion { get; private init; }
    public bool PortraitHelperEnabled { get; private init; }
    public List<SavedPreset> Presets { get; private init; } = [];
    public List<SavedPresetTag> Tags { get; private init; } = [];

    /// <summary>Presets whose code could not be read (e.g. a newer preset version).</summary>
    public int SkippedPresets { get; private init; }

    public static PresetLibraryFile Parse(string json)
    {
        if (JsonNode.Parse(json) is not JsonObject root)
            throw new InvalidDataException("The preset library file is not a JSON object.");

        var version = root["Version"]?.GetValue<int>() ?? 0;
        var enabled = root["EnabledTweaks"] is JsonArray tweaks && tweaks.Any(t => t?.GetValue<string>() == "PortraitHelper");
        var portraitHelper = root["Tweaks"]?["PortraitHelper"] as JsonObject;

        var presets = new List<SavedPreset>();
        var skipped = 0;
        if (portraitHelper?["Presets"] is JsonArray presetArray)
        {
            foreach (var node in presetArray)
            {
                var preset = node?.Deserialize<SavedPreset>(SerializerOptions);
                if (preset?.Preset == null || preset.Id == Guid.Empty)
                {
                    skipped++;
                    continue;
                }

                preset.Name ??= string.Empty;
                preset.Tags ??= [];
                presets.Add(preset);
            }
        }

        var tags = new List<SavedPresetTag>();
        if (portraitHelper?["PresetTags"] is JsonArray tagArray)
        {
            foreach (var node in tagArray)
            {
                var tag = node?.Deserialize<SavedPresetTag>(SerializerOptions);
                if (tag != null && tag.Id != Guid.Empty)
                    tags.Add(tag);
            }
        }

        return new PresetLibraryFile
        {
            ConfigVersion = version,
            PortraitHelperEnabled = enabled,
            Presets = presets,
            Tags = tags,
            SkippedPresets = skipped,
        };
    }
}

public sealed record ImportResult(int Imported, int AlreadyPresent, int TagsCreated, int ImagesCopied, int Skipped);

public static class PresetLibraryMerger
{
    /// <summary>
    /// Merges presets and tags from <paramref name="source"/> into the target lists.
    /// A preset is skipped when the target already has the same id, or the same name with the same preset code.
    /// Tags are matched by name; missing tags are created. Imported presets keep their ids, so thumbnails
    /// can be copied by id. Returns the result and the presets that were added.
    /// </summary>
    public static (ImportResult Result, List<SavedPreset> Added) Merge(
        PresetLibraryFile source,
        List<SavedPreset> targetPresets,
        List<SavedPresetTag> targetTags)
    {
        var tagMap = new Dictionary<Guid, Guid>();
        var tagsCreated = 0;

        foreach (var tag in source.Tags)
        {
            var existing = targetTags.FirstOrDefault(t => t.Id == tag.Id)
                ?? targetTags.FirstOrDefault(t => string.Equals(t.Name, tag.Name, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                existing = new SavedPresetTag(tag.Id, tag.Name);
                targetTags.Add(existing);
                tagsCreated++;
            }

            tagMap[tag.Id] = existing.Id;
        }

        var added = new List<SavedPreset>();
        var alreadyPresent = 0;

        foreach (var preset in source.Presets)
        {
            var code = preset.Preset!.ToExportedString();
            var duplicate = targetPresets.Any(p => p.Id == preset.Id
                || (p.Name == preset.Name && p.Preset?.ToExportedString() == code));

            if (duplicate)
            {
                alreadyPresent++;
                continue;
            }

            var tags = preset.Tags
                .Select(id => tagMap.TryGetValue(id, out var mapped) ? mapped : (Guid?)null)
                .Where(id => id != null)
                .Select(id => id!.Value)
                .ToHashSet();

            var copy = new SavedPreset(preset.Id, preset.Name, preset.Preset with { }, tags);
            added.Add(copy);
        }

        // keep the original order, placed after the presets that are already there
        targetPresets.AddRange(added);

        return (new ImportResult(added.Count, alreadyPresent, tagsCreated, 0, source.SkippedPresets), added);
    }
}
