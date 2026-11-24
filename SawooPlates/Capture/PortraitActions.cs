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
    public const string CapturedTagName = "Saved";

    public void CopyCode(PortraitPresetData preset, string? who = null)
    {
        _ = framework.Run(async () =>
        {
            await clipboardService.SetClipboardPortraitPreset(preset).ConfigureAwait(false);

            captureService.Notify(who == null
                ? textService.Translate("SawooPlates.Notification.CodeCopied")
                : textService.Translate("SawooPlates.Notification.CodeCopiedFrom", who));
        });
    }

    /// <summary>Saves a capture into the Preset Browser. Returns the new preset.</summary>
    public SavedPreset SaveAsPreset(CapturedPortrait capture, string name)
    {
        var config = pluginConfig.PortraitEditor;

        // the same portrait and plate design saved before: reuse it
        var code = capture.Preset.ToExportedString();
        var existing = config.Presets.FirstOrDefault(p => p.Preset?.ToExportedString() == code && SamePlate(p.Plate, capture.Plate));
        if (existing != null)
        {
            captureService.Notify($"Already saved as \"{existing.Name}\".");
            return existing;
        }

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

    private static bool SamePlate(PlateDesignData? a, PlateDesignData? b)
    {
        if (a == null || b == null)
            return a == b;

        return a.BasePlate == b.BasePlate && a.TopBorder == b.TopBorder && a.BottomBorder == b.BottomBorder
            && a.InvertPortraitPlacement == b.InvertPortraitPlacement && a.Decorations.SequenceEqual(b.Decorations);
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
    public void Apply(CapturedPortrait capture, bool? choose = null)
        => Apply(capture.Preset, GetImportFlags(capture), choose);

    /// <summary>
    /// Applies a preset to the open Portrait Editor. With the menu bar on, it opens Advanced Import
    /// with the preset selected so the user picks the fields; otherwise it imports directly.
    /// </summary>
    public void Apply(PortraitPresetData preset, ImportFlags flags = ImportFlags.All, bool? choose = null)
    {
        if (!CanApply)
        {
            captureService.Notify(textService.Translate("SawooPlates.Notification.OpenEditorFirst"), NotificationType.Warning);
            return;
        }

        // Ctrl+click: pick which parts to take; plain click: apply everything right away
        if ((choose ?? ImGui.GetIO().KeyCtrl) && menuBar.IsOpen)
        {
            clipboardService.ClipboardPreset = preset;
            clipboardService.CurrentImportFlags = flags;
            menuBarState.OpenOverlay<AdvancedImportOverlay>();
            return;
        }

        bannerService.ImportPresetToState(preset, flags);
        captureService.Notify("Applied to your portrait. Press Save in the Portrait Editor to keep it.");
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
