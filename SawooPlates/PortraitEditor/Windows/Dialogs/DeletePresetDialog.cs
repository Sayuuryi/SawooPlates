using System.IO;

using Dalamud.Interface.Windowing;
using SawooPlates.Config;
using SawooPlates.PortraitEditor;
using SawooPlates.PortraitEditor.Enums;
using SawooPlates.PortraitEditor.Services;
using SawooPlates.PortraitEditor.Utils;
using SawooPlates.PortraitEditor.Windows;
using SawooPlates.PortraitEditor.Windows.Dialogs;
using SawooPlates.PortraitEditor.Windows.MenuBarButtons;
using SawooPlates.PortraitEditor.Windows.Overlays;

namespace SawooPlates.PortraitEditor.Windows.Dialogs;

public class DeletePresetDialog
{
    private readonly INotificationManager _notificationManager;
    private readonly TextService _textService;
    private readonly PluginConfig _pluginConfig;
    private readonly ThumbnailService _thumbnailService;

    private bool _shouldOpen;
    private SavedPreset? _preset;

    public DeletePresetDialog(INotificationManager notificationManager, TextService textService, PluginConfig pluginConfig, ThumbnailService thumbnailService)
    {
        _notificationManager = notificationManager;
        _textService = textService;
        _pluginConfig = pluginConfig;
        _thumbnailService = thumbnailService;
    }


    public void Open(SavedPreset preset)
    {
        _preset = preset;
        _shouldOpen = true;
    }

    public void Draw()
    {
        if (_preset == null)
            return;

        var title = _textService.Translate("EditorWindows.DeletePresetDialog.Title");

        if (_shouldOpen)
        {
            ImGui.OpenPopup(title);
            _shouldOpen = false;
        }

        if (!ImGui.IsPopupOpen(title))
            return;

        // Always center this window when appearing
        var center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new(0.5f, 0.5f));

        using var modal = ImRaii.PopupModal(title, ImGuiWindowFlags.AlwaysAutoResize);
        if (!modal) return;

        ImGui.Text(_textService.Translate("EditorWindows.DeletePresetDialog.Prompt", _preset.Name));

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var combinedButtonWidths = ImStyle.ItemSpacing.X
            + MathF.Max(Constants.DialogButtonMinWidth, ImGuiHelpers.GetButtonSize(_textService.Translate("ConfirmationButtonWindow.Delete")).X)
            + MathF.Max(Constants.DialogButtonMinWidth, ImGuiHelpers.GetButtonSize(_textService.Translate("ConfirmationButtonWindow.Cancel")).X);

        ImCursor.X += (ImStyle.ContentRegionAvail.X - combinedButtonWidths) / 2f;

        if (ImGui.Button(_textService.Translate("ConfirmationButtonWindow.Delete"), new Vector2(120, 0)))
        {
            var thumbPath = _thumbnailService.GetPortraitThumbnailPath(_preset.Id);
            if (File.Exists(thumbPath))
            {
                try
                {
                    File.Delete(thumbPath);
                }
                catch (Exception ex)
                {
                    _notificationManager.AddNotification(new()
                    {
                        Title = "Could not delete preset",
                        Content = ex.Message,
                    });
                }
            }

            _pluginConfig.PortraitEditor.Presets.Remove(_preset);
            _pluginConfig.Save();

            _preset = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SetItemDefaultFocus();
        ImGui.SameLine();
        if (ImGui.Button(_textService.Translate("ConfirmationButtonWindow.Cancel"), new Vector2(120, 0)))
        {
            _preset = null;
            ImGui.CloseCurrentPopup();
        }
    }
}
