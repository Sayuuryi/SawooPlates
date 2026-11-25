using System.IO;
using Dalamud.Interface.ImGuiNotification;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
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
            if (existing.Gear is not { Count: > 0 } && capture.Gear.Count > 0)
            {
                existing.Gear = [.. capture.Gear];
                pluginConfig.Save();
            }

            captureService.Notify($"Already saved as \"{existing.Name}\".");
            return existing;
        }

        var tag = config.PresetTags.FirstOrDefault(t => t.Name == CapturedTagName);
        if (tag == null)
        {
            tag = new SavedPresetTag(CapturedTagName);
            config.PresetTags.Add(tag);
        }

        var preset = new SavedPreset(Guid.CreateVersion7(), name.Trim(), capture.Preset with { }, [tag.Id]) { Plate = capture.Plate, Gear = capture.Gear.Count > 0 ? [.. capture.Gear] : null };

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

    /// <summary>True when a portrait can be applied now, or the Portrait Editor can be opened for your current gearset first.</summary>
    public unsafe bool CanApplyOrOpen
    {
        get
        {
            if (CanApply)
                return true;

            var gearsets = RaptureGearsetModule.Instance();
            return gearsets != null && gearsets->IsValidGearset(gearsets->CurrentGearsetIndex);
        }
    }

    /// <summary>The editor is open and its character has loaded, so an import isn't overwritten by the editor's own load.</summary>
    private unsafe bool EditorReady
    {
        get
        {
            if (!CanApply)
                return false;

            var agent = AgentBannerEditor.Instance();
            return agent->IsAddonReady() && agent->EditorState->CharaView != null && agent->EditorState->CharaView->CharaViewPortraitCharacterLoaded;
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
    /// Applies a preset to the Portrait Editor, opening it for your current gearset first when it's closed.
    /// With <paramref name="choose"/> (or Ctrl held) it opens Advanced Import so the user picks the fields.
    /// </summary>
    public void Apply(PortraitPresetData preset, ImportFlags flags = ImportFlags.All, bool? choose = null)
    {
        var pick = choose ?? ImGui.GetIO().KeyCtrl;
        if (CanApply)
        {
            ApplyNow(preset, flags, pick);
            return;
        }

        framework.RunOnFrameworkThread(() =>
        {
            if (!OpenEditor())
            {
                captureService.Notify(textService.Translate("SawooPlates.Notification.OpenEditorFirst"), NotificationType.Warning);
                return;
            }

            ApplyWhenReady(preset, flags, pick, DateTime.UtcNow.AddSeconds(10));
        });
    }

    private unsafe bool OpenEditor()
    {
        var gearsets = RaptureGearsetModule.Instance();
        var agent = AgentGearSet.Instance();
        if (gearsets == null || agent == null)
            return false;

        var id = gearsets->CurrentGearsetIndex;
        if (!gearsets->IsValidGearset(id))
            return false;

        agent->OpenBannerEditorForGearset(id);
        return true;
    }

    private void ApplyWhenReady(PortraitPresetData preset, ImportFlags flags, bool pick, DateTime giveUpAt)
    {
        if (EditorReady)
        {
            // give the editor a moment to finish setting up before changing it
            _ = framework.RunOnTick(() =>
            {
                if (CanApply)
                    ApplyNow(preset, flags, pick);
            }, TimeSpan.FromSeconds(1));
            return;
        }

        if (DateTime.UtcNow > giveUpAt)
        {
            captureService.Notify("The Portrait Editor didn't open. Open it and try again.", NotificationType.Warning);
            return;
        }

        _ = framework.RunOnTick(() => ApplyWhenReady(preset, flags, pick, giveUpAt), TimeSpan.FromMilliseconds(200));
    }

    private void ApplyNow(PortraitPresetData preset, ImportFlags flags, bool pick)
    {
        // Ctrl+click: pick which parts to take; plain click: apply everything right away
        if (pick && menuBar.IsOpen)
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
