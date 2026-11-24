using System.IO;
using Dalamud.Interface.ImGuiNotification;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using SawooPlates.Config;
using SawooPlates.PortraitEditor.Enums;
using SawooPlates.PortraitEditor.Services;
using SawooPlates.PortraitEditor.Windows;
using SawooPlates.PortraitEditor.Windows.Overlays;

namespace SawooPlates.Capture;

/// <summary>What can be done with a captured portrait: save, apply, copy.</summary>
public sealed class PortraitActions(
    IPluginLog log,
    IFramework framework,
    PluginConfig pluginConfig,
    TextService textService,
    CaptureService captureService,
    ClipboardService clipboardService,
    BannerService bannerService,
    ThumbnailService presetThumbnails,
    MenuBarState menuBarState,
    MenuBar menuBar)
{
    public const string CapturedTagName = "Captured";

    public void CopyCode(PortraitPresetData preset, string? who = null)
    {
        _ = framework.Run(async () =>
        {
            await clipboardService.SetClipboardPortraitPreset(preset).ConfigureAwait(false);

            if (captureService.Settings.NotifyOnCopy)
            {
                captureService.Notify(who == null
                    ? textService.Translate("SawooPlates.Notification.CodeCopied")
                    : textService.Translate("SawooPlates.Notification.CodeCopiedFrom", who));
            }
        });
    }

    /// <summary>Saves a capture into the Preset Browser. Returns the new preset.</summary>
    public SavedPreset SaveAsPreset(CapturedPortrait capture, string name)
    {
        var config = pluginConfig.PortraitEditor;

        var tag = config.PresetTags.FirstOrDefault(t => t.Name == CapturedTagName);
        if (tag == null)
        {
            tag = new SavedPresetTag(CapturedTagName);
            config.PresetTags.Add(tag);
        }

        var preset = new SavedPreset(Guid.CreateVersion7(), name.Trim(), capture.Preset with { }, [tag.Id]) { Plate = capture.Plate };

        var source = captureService.GetImagePath(capture.Id);
        if (File.Exists(source))
        {
            try
            {
                File.Copy(source, presetThumbnails.GetPortraitThumbnailPath(preset.Id), true);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Could not copy capture image to the preset");
            }
        }

        config.Presets.Insert(0, preset);
        pluginConfig.Save();

        captureService.Notify(textService.Translate("SawooPlates.Notification.SavedAsPreset", preset.Name));
        return preset;
    }

    /// <summary>True when the Portrait Editor is open and a portrait can be applied to it.</summary>
    public unsafe bool CanApply
    {
        get
        {
            var agent = AgentBannerEditor.Instance();
            return agent != null && agent->EditorState != null && AddonUtil.IsAddonOpen(AgentId.BannerEditor);
        }
    }

    /// <summary>Import flags for a capture: everything except fields the source did not store.</summary>
    public static ImportFlags GetImportFlags(CapturedPortrait capture)
    {
        var flags = ImportFlags.All;
        if (!capture.FrameKnown)
            flags &= ~ImportFlags.BannerFrame;
        if (!capture.DecorationKnown)
            flags &= ~ImportFlags.BannerDecoration;
        return flags;
    }

    /// <summary>Applies a capture to the open Portrait Editor (see <see cref="Apply(PortraitPresetData, ImportFlags)"/>).</summary>
    public void Apply(CapturedPortrait capture)
        => Apply(capture.Preset, GetImportFlags(capture));

    /// <summary>
    /// Applies a preset to the open Portrait Editor. With the menu bar on, it opens Advanced Import
    /// with the preset selected so the user picks the fields; otherwise it imports directly.
    /// </summary>
    public void Apply(PortraitPresetData preset, ImportFlags flags = ImportFlags.All)
    {
        if (!CanApply)
        {
            captureService.Notify(textService.Translate("SawooPlates.Notification.OpenEditorFirst"), NotificationType.Warning);
            return;
        }

        if (menuBar.IsOpen)
        {
            clipboardService.ClipboardPreset = preset;
            clipboardService.CurrentImportFlags = flags;
            menuBarState.OpenOverlay<AdvancedImportOverlay>();
        }
        else
        {
            bannerService.ImportPresetToState(preset, flags);
        }
    }

    /// <summary>Deletes a preset from the Preset Browser together with its thumbnail.</summary>
    public void DeletePreset(SavedPreset preset)
    {
        try
        {
            var path = presetThumbnails.GetPortraitThumbnailPath(preset.Id);
            if (File.Exists(path))
                File.Delete(path);
            presetThumbnails.Invalidate(preset.Id);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not delete thumbnail of preset {id}", preset.Id);
        }

        pluginConfig.PortraitEditor.Presets.Remove(preset);
        pluginConfig.Save();
    }
}
