using Dalamud.Interface.ImGuiSeStringRenderer;
using Lumina.Text.ReadOnly;
using SawooPlates.Capture;
using SawooPlates.Config;
using SawooPlates.PortraitEditor;

namespace SawooPlates.Windows.Tabs;

public sealed class SettingsTab(
    IDalamudPluginInterface pluginInterface,
    ITextureProvider textureProvider,
    TextService textService,
    PluginConfig pluginConfig,
    CaptureService captureService,
    PresetImporter presetImporter,
    IServiceProvider serviceProvider)
{
    private const string Prefix = "Editor.Config";

    private string? _importMessage;

    public void Draw()
    {
        var changed = false;

        DrawGlamourerWarning();
        changed |= DrawCaptureSettings();
        changed |= DrawPortraitEditorSettings();

        if (changed)
            pluginConfig.Save();
    }

    private void DrawGlamourerWarning()
    {
        if (!pluginInterface.InstalledPlugins.Any(p => p.IsLoaded && p.InternalName == "Glamourer"))
            return;

        ImGuiUtils.DrawSection(textService.Translate("Common.Config.SectionTitle.IncompatibilityWarning"), pushDown: false);
        textureProvider.DrawIcon(60073, 24);
        ImGui.SameLine();
        ImGui.TextWrapped(textService.Translate("Editor.Config.IncompatibilityWarning.Glamourer"));
    }

    private bool DrawCaptureSettings()
    {
        var settings = pluginConfig.Capture;
        var changed = false;

        if (Group("Capture", FontAwesomeIcon.IdCard))
        {
            changed |= DrawOwnBool("AutoCaptureAdventurerPlate", ref settings.AutoCaptureAdventurerPlate);
            changed |= DrawOwnBool("AutoCapturePartyPortraits", ref settings.AutoCapturePartyPortraits);
            changed |= DrawOwnBool("AutoCaptureMvpVote", ref settings.AutoCaptureMvpVote);
            changed |= DrawOwnBool("SkipOwnCharacter", ref settings.SkipOwnCharacter);
        }

        if (Group("Buttons and menus", FontAwesomeIcon.MousePointer))
        {
            changed |= DrawOwnBool("ShowPlateButton", ref settings.ShowPlateButton);
            changed |= DrawOwnBool("ShowPartyButtons", ref settings.ShowPartyButtons);
            changed |= DrawOwnBool("ContextMenuEntry", ref settings.ContextMenuEntry);
        }

        if (Group("Recent", FontAwesomeIcon.History))
        {
            changed |= DrawOwnBool("DedupePerPlayer", ref settings.DedupePerPlayer);

            ImGui.Spacing();
            ImGui.TextUnformatted(textService.Translate("SawooPlates.Settings.MaxHistorySize.Label"));
            ImGui.SetNextItemWidth(220 * ImStyle.Scale);
            if (ImGui.SliderInt("##MaxHistory", ref settings.MaxHistorySize, 10, 1000))
            {
                settings.MaxHistorySize = Math.Clamp(settings.MaxHistorySize, 10, 1000);
                captureService.TrimHistory();
                changed = true;
            }

            ImGui.Spacing();
        }

        if (Group("Notifications", FontAwesomeIcon.Bell))
        {
            changed |= DrawOwnBool("NotifyOnCapture", ref settings.NotifyOnCapture);
            changed |= DrawOwnBool("NotifyOnCopy", ref settings.NotifyOnCopy);
        }

        return changed;
    }

    private bool DrawPortraitEditorSettings()
    {
        var config = pluginConfig.PortraitEditor;
        var changed = false;

        if (Group("Portrait Editor", FontAwesomeIcon.UserEdit))
        {
            var enableTools = pluginConfig.EnablePortraitEditorTools;
            if (DrawOwnBool("EnablePortraitEditorTools", ref enableTools))
            {
                pluginConfig.EnablePortraitEditorTools = enableTools;
                changed = true;
                serviceProvider.GetRequiredService<PortraitEditorModule>().RefreshMenuBar();
            }

            changed |= DrawBool("EmbedPresetStringInThumbnails", ref config.EmbedPresetStringInThumbnails);

            if (presetImporter.SourceExists)
            {
                ImGui.Spacing();
                if (Ui.Button(FontAwesomeIcon.FileImport, $"Import presets from {PresetImporter.SourcePlugin}",
                        tooltip: "Copies presets, tags and thumbnails. Presets you already have are skipped."))
                {
                    try
                    {
                        var result = presetImporter.ImportAll();
                        _importMessage = result.Imported == 0
                            ? "Nothing new to import."
                            : $"Imported {result.Imported} presets.";
                    }
                    catch (Exception ex)
                    {
                        _importMessage = $"Couldn't import: {ex.Message}";
                    }
                }

                if (_importMessage != null)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(Ui.Dim, _importMessage);
                }
            }
        }

        if (Group("Gear and portrait", FontAwesomeIcon.Tshirt))
        {
            changed |= DrawBool("NotifyGearChecksumMismatch", ref config.NotifyGearChecksumMismatch, drawAfterDescription: () =>
            {
                using var disabled = ImRaii.Disabled(!config.NotifyGearChecksumMismatch);
                using var indent = ImRaii.PushIndent();
                changed |= DrawBool("IgnoreDoHDoL", ref config.IgnoreDoHDoL);
            });
            changed |= DrawBool("ReequipGearsetOnUpdate", ref config.ReequipGearsetOnUpdate, drawAfterLabel: DrawNetworkWarning);
            changed |= DrawBool("AutoUpdatePotraitOnGearUpdate", ref config.AutoUpdatePotraitOnGearUpdate, drawAfterLabel: DrawNetworkWarning);
        }

        return changed;
    }

    /// <summary>A collapsible group, open by default.</summary>
    private static bool Group(string title, FontAwesomeIcon icon)
    {
        ImGui.Spacing();
        var open = ImGui.CollapsingHeader($"{title}##Group{title}", ImGuiTreeNodeFlags.DefaultOpen);

        // icon drawn over the header, right-aligned
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var text = icon.ToIconString();
            var size = ImGui.CalcTextSize(text);
            ImGui.GetWindowDrawList().AddText(new Vector2(max.X - size.X - 8 * ImStyle.Scale, min.Y + (max.Y - min.Y - size.Y) / 2f),
                ImGui.GetColorU32(ImGuiCol.TextDisabled), text);
        }

        if (open)
            ImGui.Spacing();

        return open;
    }

    private bool DrawOwnBool(string fieldName, ref bool value)
    {
        var label = textService.Translate($"SawooPlates.Settings.{fieldName}.Label");
        textService.TryGetTranslation($"SawooPlates.Settings.{fieldName}.Description", out var description);
        return DrawBool(fieldName, label, description, ref value);
    }

    private bool DrawBool(string fieldName, ref bool value, Action? drawAfterLabel = null, Action? drawAfterDescription = null)
    {
        var label = textService.Translate($"{Prefix}.{fieldName}.Label");
        textService.TryGetTranslation($"{Prefix}.{fieldName}.Description", out var description);
        return DrawBool(fieldName, label, description, ref value, drawAfterLabel, drawAfterDescription);
    }

    private static bool DrawBool(string id, string label, string? description, ref bool value, Action? drawAfterLabel = null, Action? drawAfterDescription = null)
    {
        using var pushId = ImRaii.PushId(id);

        var result = false;

        using (var table = ImRaii.Table("##Bool", 2, ImGuiTableFlags.NoSavedSettings))
        {
            if (table)
            {
                ImGui.TableSetupColumn("Checkbox", ImGuiTableColumnFlags.WidthFixed, ImStyle.FrameHeight);
                ImGui.TableSetupColumn("Text", ImGuiTableColumnFlags.WidthStretch);

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                result = ImGui.Checkbox("##Input", ref value);

                ImGui.TableNextColumn();

                ImGui.TextWrapped(label);

                if (ImGui.IsItemClicked())
                {
                    value = !value;
                    result = true;
                }

                drawAfterLabel?.Invoke();

                if (!string.IsNullOrEmpty(description))
                {
                    ImGuiHelpers.SeStringWrapped(ReadOnlySeString.FromText(description), new SeStringDrawParams() { Color = Color.Text700.ToUInt() });
                }

                drawAfterDescription?.Invoke();
            }
        }

        ImCursor.Y += -ImStyle.ItemSpacing.Y / 2;

        return result;
    }

    private void DrawNetworkWarning()
    {
        ImGui.SameLine();
        ImGuiUtils.Icon(FontAwesomeIcon.Bolt, Color.Yellow.ToUInt());
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text(textService.Translate("Common.Config.NetworkRequestWarning"));
            ImGui.EndTooltip();
        }
    }
}
