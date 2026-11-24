using System.IO;
using SawooPlates.Config;
using SawooPlates.Core.Import;
using SawooPlates.PortraitEditor.Services;

namespace SawooPlates.Capture;

/// <summary>
/// Copies portrait presets (with tags and thumbnails) saved by another plugin on this PC into Sawoo Plates.
/// The other plugin's files are only read.
/// </summary>
public sealed class PresetImporter(
    IDalamudPluginInterface pluginInterface,
    IPluginLog log,
    PluginConfig pluginConfig,
    ThumbnailService presetThumbnails)
{
    /// <summary>Plugin whose preset library can be imported.</summary>
    public const string SourcePlugin = "HaselTweaks";

    private string ConfigPath => Path.Join(pluginInterface.ConfigFile.DirectoryName, $"{SourcePlugin}.json");

    private string PortraitsPath => Path.Join(pluginInterface.ConfigFile.DirectoryName, SourcePlugin, "Portraits");

    public bool SourceExists => File.Exists(ConfigPath);

    private PresetLibraryFile Read()
    {
        // the other plugin may be writing its config right now
        using var stream = new FileStream(ConfigPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return PresetLibraryFile.Parse(reader.ReadToEnd());
    }

    /// <summary>How many presets would be new, without changing anything. 0 when there is nothing to import.</summary>
    public int CountNew()
    {
        if (!SourceExists)
            return 0;

        try
        {
            var presets = pluginConfig.PortraitEditor.Presets.ToList();
            var tags = pluginConfig.PortraitEditor.PresetTags.ToList();
            return PresetLibraryMerger.Merge(Read(), presets, tags).Result.Imported;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not read presets to import");
            return 0;
        }
    }

    public ImportResult ImportAll()
    {
        var library = Read();
        var config = pluginConfig.PortraitEditor;

        var (result, added) = PresetLibraryMerger.Merge(library, config.Presets, config.PresetTags);

        var imagesCopied = 0;
        foreach (var preset in added)
        {
            var source = Path.Join(PortraitsPath, $"{preset.Id.ToString("D").ToLowerInvariant()}.png");
            if (!File.Exists(source))
                continue;

            try
            {
                File.Copy(source, presetThumbnails.GetPortraitThumbnailPath(preset.Id), overwrite: true);
                presetThumbnails.Invalidate(preset.Id);
                imagesCopied++;
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Could not copy thumbnail {id}", preset.Id);
            }
        }

        pluginConfig.Save();

        result = result with { ImagesCopied = imagesCopied };
        log.Information("Imported {imported} presets ({present} already there), {images} images, {tags} new tags",
            result.Imported, result.AlreadyPresent, result.ImagesCopied, result.TagsCreated);
        return result;
    }
}
