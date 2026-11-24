using System.Threading.Tasks;

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

public class CreatePresetDialog
{
    private readonly IPluginLog _logger;
    private readonly PluginConfig _pluginConfig;
    private readonly TextService _textService;
    private readonly ThumbnailService _thumbnailService;

    private bool _shouldOpen;
    private bool _isSaving;
    private string? _name;
    private PortraitPresetData? _preset;
    private BgraImage? _image;
    private HashSet<Guid>? _tags;

    public CreatePresetDialog(IPluginLog logger, PluginConfig pluginConfig, TextService textService, ThumbnailService thumbnailService)
    {
        _logger = logger;
        _pluginConfig = pluginConfig;
        _textService = textService;
        _thumbnailService = thumbnailService;
    }


    public void Open(string name, PortraitPresetData? preset, BgraImage? image)
    {
        _name = name;
        _preset = preset;
        _image = image;
        _tags = [];
        _isSaving = false;
        _shouldOpen = true;
    }

    public void Draw()
    {
        if (_preset == null || _image == null || _tags == null)
            return;

        var title = _textService.Translate("EditorWindows.CreatePresetDialog.Title");

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

        using var disabledDueToSaving = ImRaii.Disabled(_isSaving);

        ImGui.Text(_textService.Translate("EditorWindows.CreatePresetDialog.Name.Label"));

        if (ImGui.IsWindowAppearing())
            ImGui.SetKeyboardFocusHere();

        var name = _name ?? string.Empty;
        if (ImGui.InputText("##PresetName", ref name, Constants.PresetNameMaxLength))
            _name = name;

        var availableTags = _pluginConfig.PortraitEditor.PresetTags;
        if (availableTags.Count != 0)
        {
            ImGui.Spacing();
            ImGui.Text(_textService.Translate("EditorWindows.CreatePresetDialog.Tags.Label"));

            var tagNames = _tags
                .Select(id => availableTags.FirstOrDefault((t) => t.Id == id)?.Name ?? string.Empty)
                .Where(name => !string.IsNullOrEmpty(name));

            var preview = tagNames.Any() ? string.Join(", ", tagNames) : _textService.Translate("EditorWindows.CreatePresetDialog.Tags.None");

            ImGui.Spacing();
            using var tagsCombo = ImRaii.Combo("##PresetTag", preview, ImGuiComboFlags.HeightLarge);
            if (tagsCombo)
            {
                foreach (var tag in availableTags)
                {
                    var isSelected = _tags.Contains(tag.Id);

                    if (ImGui.Selectable($"{tag.Name}##PresetTag{tag.Id}", isSelected))
                    {
                        if (isSelected)
                        {
                            _tags.Remove(tag.Id);
                        }
                        else
                        {
                            _tags.Add(tag.Id);
                        }
                    }
                }
            }
        }

        var disabled = string.IsNullOrWhiteSpace(_name);
        var shouldSave = !disabled && (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter));

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var combinedButtonWidths = ImStyle.ItemSpacing.X
            + MathF.Max(Constants.DialogButtonMinWidth, ImGuiHelpers.GetButtonSize(_textService.Translate("ConfirmationButtonWindow.Save")).X)
            + MathF.Max(Constants.DialogButtonMinWidth, ImGuiHelpers.GetButtonSize(_textService.Translate("ConfirmationButtonWindow.Cancel")).X);

        ImCursor.X += (ImStyle.ContentRegionAvail.X - combinedButtonWidths) / 2f;

        using (ImRaii.Disabled(disabled))
        {
            if (ImGui.Button(_textService.Translate(_isSaving ? "ConfirmationButtonWindow.Saving" : "ConfirmationButtonWindow.Save"), new Vector2(120, 0)) || shouldSave)
            {
                _isSaving = true;

                _ = Task.Run(() =>
                {
                    try
                    {
                        var guid = Guid.CreateVersion7();
                        var thumbPath = _thumbnailService.GetPortraitThumbnailPath(guid);

                        _image.SaveAsPng(thumbPath, _preset.ToExportedString());

                        _pluginConfig.PortraitEditor.Presets.Insert(0, new(guid, name.Trim(), _preset, _tags));
                        _pluginConfig.Save();

                        _name = string.Empty;
                        _preset = null;
                        _image.Dispose();
                        _image = null;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Could not create preset");
                    }

                    _isSaving = false;
                    ImGui.CloseCurrentPopup();
                });
            }
        }

        ImGui.SetItemDefaultFocus();
        ImGui.SameLine();
        if (ImGui.Button(_textService.Translate("ConfirmationButtonWindow.Cancel"), new Vector2(120, 0)))
        {
            _name = string.Empty;
            _preset = null;
            _image.Dispose();
            _image = null;
            ImGui.CloseCurrentPopup();
        }
    }
}
