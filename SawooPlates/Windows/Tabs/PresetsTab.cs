using SawooPlates.Capture;
using SawooPlates.Config;
using SawooPlates.PortraitEditor.Services;

namespace SawooPlates.Windows.Tabs;

/// <summary>The saved presets (same library as the Preset Browser in the Portrait Editor).</summary>
public sealed class PresetsTab(
    TextService textService,
    PluginConfig pluginConfig,
    ThumbnailService thumbnails,
    PortraitActions actions,
    PresetDetailPanel detailPanel)
{
    private string _search = string.Empty;
    private Guid? _tagFilter;
    private Guid? _selectedId;
    private SavedPreset? _pendingDelete;

    public void Draw()
    {
        var config = pluginConfig.PortraitEditor;

        if (config.Presets.Count == 0)
        {
            ImGui.TextWrapped(textService.Translate("SawooPlates.Presets.Empty"));
            return;
        }

        ImGui.SetNextItemWidth(200 * ImStyle.Scale);
        ImGui.InputTextWithHint("##Search", textService.Translate("SawooPlates.Presets.SearchHint"), ref _search, 100);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(160 * ImStyle.Scale);
        var tagLabel = _tagFilter == null
            ? textService.Translate("SawooPlates.Presets.AllTags")
            : config.PresetTags.FirstOrDefault(t => t.Id == _tagFilter)?.Name ?? textService.Translate("SawooPlates.Presets.AllTags");
        using (var combo = ImRaii.Combo("##Tag", tagLabel))
        {
            if (combo)
            {
                if (ImGui.Selectable(textService.Translate("SawooPlates.Presets.AllTags"), _tagFilter == null))
                    _tagFilter = null;

                foreach (var tag in config.PresetTags)
                {
                    if (ImGui.Selectable($"{tag.Name}##{tag.Id}", _tagFilter == tag.Id))
                        _tagFilter = tag.Id;
                }
            }
        }

        var presets = config.Presets
            .Where(p => p.Preset != null)
            .Where(p => _tagFilter == null || p.Tags.Contains(_tagFilter.Value))
            .Where(p => string.IsNullOrWhiteSpace(_search) || p.Name.Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        var selected = presets.FirstOrDefault(p => p.Id == _selectedId) ?? presets.FirstOrDefault();
        _selectedId = selected?.Id;

        using (var list = ImRaii.Child("##PresetList", new Vector2(260 * ImStyle.Scale, -1), true))
        {
            if (list)
            {
                foreach (var preset in presets)
                {
                    using var id = ImRaii.PushId(preset.Id.ToString());
                    if (ImGui.Selectable(preset.Name, preset.Id == _selectedId))
                        _selectedId = preset.Id;
                }
            }
        }

        ImGui.SameLine();

        using var detail = ImRaii.Child("##PresetDetail", new Vector2(-1, -1));
        if (detail.Success && selected != null)
            DrawDetail(selected, config);

        DrawDeleteConfirm();
    }

    private void DrawDetail(SavedPreset preset, PortraitEditorSettings config)
    {
        ImGui.TextColored(Color.Gold, preset.Name);

        var tagNames = preset.Tags.Select(id => config.PresetTags.FirstOrDefault(t => t.Id == id)?.Name).Where(n => n != null);
        if (tagNames.Any())
            ImGui.TextDisabled(string.Join(", ", tagNames));

        ImGui.Spacing();

        using (ImRaii.Disabled(!actions.CanApply))
        {
            if (ImGui.Button(textService.Translate("SawooPlates.Detail.Apply")))
                actions.Apply(preset.Preset!);
        }

        if (!actions.CanApply && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(textService.Translate("SawooPlates.Detail.Apply.Disabled"));

        ImGui.SameLine();
        if (ImGui.Button(textService.Translate("SawooPlates.Detail.CopyCode")))
            actions.CopyCode(preset.Preset!);

        ImGui.SameLine();
        if (ImGuiUtils.IconButton("Delete", FontAwesomeIcon.Trash, textService.Translate("SawooPlates.Detail.Delete")))
            _pendingDelete = preset;

        ImGui.Spacing();

        var height = Math.Min(320 * ImStyle.Scale, ImStyle.ContentRegionAvail.Y);
        detailPanel.DrawPortrait(preset.Preset!, thumbnails, preset.Id, height);
        ImGui.SameLine();

        using var values = ImRaii.Child("##Values", new Vector2(-1, height));
        if (values)
            detailPanel.DrawValues(preset.Preset!);
    }

    private void DrawDeleteConfirm()
    {
        var title = textService.Translate("EditorWindows.DeletePresetDialog.Title") + "##DeletePresetMain";

        if (_pendingDelete != null && !ImGui.IsPopupOpen(title))
            ImGui.OpenPopup(title);

        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f));
        using var popup = ImRaii.PopupModal(title, ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup || _pendingDelete == null)
            return;

        ImGui.Text(textService.Translate("EditorWindows.DeletePresetDialog.Prompt", _pendingDelete.Name));
        ImGui.Spacing();

        if (ImGui.Button(textService.Translate("ConfirmationButtonWindow.Delete"), new Vector2(120 * ImStyle.Scale, 0)))
        {
            actions.DeletePreset(_pendingDelete);
            _pendingDelete = null;
            _selectedId = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button(textService.Translate("ConfirmationButtonWindow.Cancel"), new Vector2(120 * ImStyle.Scale, 0)))
        {
            _pendingDelete = null;
            ImGui.CloseCurrentPopup();
        }
    }
}
