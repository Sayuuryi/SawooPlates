using SawooPlates.Capture;
using SawooPlates.Config;
using SawooPlates.PortraitEditor.Services;

namespace SawooPlates.Windows.Tabs;

/// <summary>The saved presets (same library as the Preset Browser in the Portrait Editor).</summary>
public sealed class PresetsTab(
    PluginConfig pluginConfig,
    ThumbnailService thumbnails,
    PortraitActions actions,
    PresetDetailPanel portraitPanel,
    PlateItems plateItems)
{
    private string _search = string.Empty;
    private Guid? _tagFilter;
    private Guid? _selectedId;

    private SavedPreset? _pendingDelete;
    private SavedPreset? _editing;
    private string _editName = string.Empty;
    private readonly HashSet<Guid> _editTags = [];
    private string _newTag = string.Empty;
    private bool _openEdit;

    private PortraitEditorSettings Config => pluginConfig.PortraitEditor;

    public void Draw()
    {
        if (Config.Presets.Count == 0)
        {
            Ui.EmptyState(FontAwesomeIcon.Images, "No presets yet",
                "Save one with the menu bar in the Portrait Editor, or from a portrait in Recent.");
            return;
        }

        var presets = Config.Presets
            .Where(p => p.Preset != null)
            .Where(p => _tagFilter == null || p.Tags.Contains(_tagFilter.Value))
            .Where(p => string.IsNullOrWhiteSpace(_search) || p.Name.Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        var selected = presets.FirstOrDefault(p => p.Id == _selectedId) ?? presets.FirstOrDefault();
        _selectedId = selected?.Id;

        using (var left = ImRaii.Child("##PresetsLeft", new Vector2(270 * Ui.Scale, -1), false))
        {
            if (left)
                DrawList(presets);
        }

        ImGui.SameLine(0, 12 * Ui.Scale);

        using (var right = ImRaii.Child("##PresetsRight", new Vector2(-1, -1), false))
        {
            if (right.Success)
            {
                if (selected == null)
                    Ui.EmptyState(FontAwesomeIcon.Search, "No matches", "Nothing matches the search or tag.");
                else
                    DrawDetail(selected);
            }
        }

        DrawEditPopup();
        DrawDeleteConfirm();
    }

    private void DrawList(List<SavedPreset> presets)
    {
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##Search", "Search presets", ref _search, 100);

        ImGui.SetNextItemWidth(-1);
        var tagLabel = _tagFilter == null ? "All tags" : Config.PresetTags.FirstOrDefault(t => t.Id == _tagFilter)?.Name ?? "All tags";
        using (var combo = ImRaii.Combo("##Tag", tagLabel))
        {
            if (combo)
            {
                if (ImGui.Selectable("All tags", _tagFilter == null))
                    _tagFilter = null;

                foreach (var tag in Config.PresetTags)
                {
                    var count = Config.Presets.Count(p => p.Tags.Contains(tag.Id));
                    if (ImGui.Selectable($"{tag.Name}  ({count})##{tag.Id}", _tagFilter == tag.Id))
                        _tagFilter = tag.Id;
                }
            }
        }

        var footer = ImGui.GetTextLineHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y;
        using (var list = ImRaii.Child("##List", new Vector2(-1, -footer), true))
        {
            if (list)
            {
                foreach (var preset in presets)
                    DrawListRow(preset);
            }
        }

        ImGui.TextColored(Ui.Dim, $"{presets.Count} of {Config.Presets.Count} presets");
    }

    private void DrawListRow(SavedPreset preset)
    {
        using var id = ImRaii.PushId(preset.Id.ToString());

        var height = 44 * Ui.Scale;
        var start = ImGui.GetCursorPos();

        if (ImGui.Selectable("##Row", preset.Id == _selectedId, ImGuiSelectableFlags.None, new Vector2(0, height)))
            _selectedId = preset.Id;

        if (ImGui.IsItemHovered())
            DrawRowTooltip(preset);

        var end = ImGui.GetCursorPos();

        // tiny portrait thumbnail
        var thumb = new Vector2(height * 0.6f, height);
        ImGui.SetCursorPos(start);
        if (thumbnails.TryGetThumbnail(preset.Id, (int)thumb.X, (int)thumb.Y, out _, out var wrap, out _) && wrap != null)
            ImGui.Image(wrap.Handle, thumb);
        else
            ImGui.Dummy(thumb);

        var textX = start.X + thumb.X + 10 * Ui.Scale;
        var lineHeight = ImGui.GetTextLineHeight();
        ImGui.SetCursorPos(new Vector2(textX, start.Y + height / 2f - lineHeight));
        ImGui.TextUnformatted(preset.Name);

        var tags = TagNames(preset);
        if (tags.Length > 0)
        {
            ImGui.SetCursorPos(new Vector2(textX, start.Y + height / 2f));
            ImGui.TextColored(Ui.Dim, tags);
        }

        ImGui.SetCursorPos(end);
    }

    private void DrawRowTooltip(SavedPreset preset)
    {
        using var tooltip = ImRaii.Tooltip();

        var size = new Vector2(96, 160) * Ui.Scale;
        if (thumbnails.TryGetThumbnail(preset.Id, (int)size.X, (int)size.Y, out _, out var wrap, out _) && wrap != null)
        {
            ImGui.Image(wrap.Handle, size);
            ImGui.SameLine(0, 10 * Ui.Scale);
        }

        using (ImRaii.Group())
        {
            ImGui.TextColored(Ui.Accent, preset.Name);
            var tags = TagNames(preset);
            if (tags.Length > 0)
                ImGui.TextColored(Ui.Dim, tags);

            var missing = plateItems.CountMissing(null, preset.Preset!, true, true);
            ImGui.Spacing();
            if (missing > 0)
                ImGui.TextColored(Ui.Bad, missing == 1 ? "1 part isn't unlocked for you" : $"{missing} parts aren't unlocked for you");
            else
                ImGui.TextColored(Ui.Good, "You have everything it uses");
        }
    }

    private string TagNames(SavedPreset preset)
        => string.Join(", ", preset.Tags.Select(id => Config.PresetTags.FirstOrDefault(t => t.Id == id)?.Name).Where(n => n != null));

    private void DrawDetail(SavedPreset preset)
    {
        ImGui.TextColored(Ui.Accent, preset.Name);
        var tags = TagNames(preset);
        ImGui.TextColored(Ui.Dim, tags.Length > 0 ? tags : "No tags");

        ImGui.Spacing();

        if (Ui.Button(FontAwesomeIcon.Magic, "Apply to my portrait", !actions.CanApply,
                actions.CanApply ? "Opens Advanced Import with this preset, so you can pick what to take." : "Open your Portrait Editor first."))
        {
            actions.Apply(preset.Preset!);
        }

        ImGui.SameLine();
        if (Ui.Button(FontAwesomeIcon.Copy, "Copy code"))
            actions.CopyCode(preset.Preset!);

        ImGui.SameLine();
        if (Ui.Button(FontAwesomeIcon.Pen, "Edit"))
        {
            _editing = preset;
            _editName = preset.Name;
            _editTags.Clear();
            _editTags.UnionWith(preset.Tags);
            _newTag = string.Empty;
            _openEdit = true;
        }

        ImGui.SameLine(ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX() - ImGui.GetFrameHeight());
        if (Ui.IconButton("Delete", FontAwesomeIcon.Trash, "Delete preset"))
            _pendingDelete = preset;

        var missing = plateItems.CountMissing(null, preset.Preset!, true, true);
        ImGui.Spacing();
        if (missing > 0)
        {
            Ui.Icon(FontAwesomeIcon.ExclamationCircle, Ui.Bad);
            ImGui.SameLine();
            ImGui.TextUnformatted(missing == 1
                ? "1 part isn't unlocked for you; it is skipped when you apply the preset."
                : $"{missing} parts aren't unlocked for you; they are skipped when you apply the preset.");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var height = Math.Min(380 * Ui.Scale, ImGui.GetContentRegionAvail().Y);
        using (ImRaii.Group())
            portraitPanel.DrawPortrait(preset.Preset!, thumbnails, preset.Id, height);

        ImGui.SameLine(0, 14 * Ui.Scale);

        using var items = ImRaii.Child("##Items", new Vector2(-1, -1), false);
        if (!items.Success)
            return;

        plateItems.Draw(null, preset.Preset!);

        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Camera and lighting"))
            portraitPanel.DrawValues(preset.Preset!);
    }

    private void DrawEditPopup()
    {
        const string title = "Edit preset##EditPresetMain";

        if (_openEdit)
        {
            ImGui.OpenPopup(title);
            _openEdit = false;
        }

        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f));
        using var popup = ImRaii.PopupModal(title, ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup || _editing == null)
            return;

        ImGui.TextUnformatted("Name");
        ImGui.SetNextItemWidth(320 * Ui.Scale);
        ImGui.InputText("##Name", ref _editName, PortraitEditor.Utils.Constants.PresetNameMaxLength);

        ImGui.Spacing();
        ImGui.TextUnformatted("Tags");
        foreach (var tag in Config.PresetTags)
        {
            var on = _editTags.Contains(tag.Id);
            if (ImGui.Checkbox($"{tag.Name}##{tag.Id}", ref on))
            {
                if (on) _editTags.Add(tag.Id);
                else _editTags.Remove(tag.Id);
            }
        }

        ImGui.SetNextItemWidth(200 * Ui.Scale);
        ImGui.InputTextWithHint("##NewTag", "New tag", ref _newTag, 30);
        ImGui.SameLine();
        using (ImRaii.Disabled(string.IsNullOrWhiteSpace(_newTag)))
        {
            if (ImGui.Button("Add tag"))
            {
                var name = _newTag.Trim();
                var tag = Config.PresetTags.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
                if (tag == null)
                {
                    tag = new SavedPresetTag(name);
                    Config.PresetTags.Add(tag);
                }

                _editTags.Add(tag.Id);
                _newTag = string.Empty;
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var canSave = !string.IsNullOrWhiteSpace(_editName);
        using (ImRaii.Disabled(!canSave))
        {
            if (ImGui.Button("Save", new Vector2(120 * Ui.Scale, 0)))
            {
                _editing.Name = _editName.Trim();
                _editing.Tags.Clear();
                _editing.Tags.UnionWith(_editTags);
                pluginConfig.Save();
                _editing = null;
                ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(120 * Ui.Scale, 0)))
        {
            _editing = null;
            ImGui.CloseCurrentPopup();
        }
    }

    private void DrawDeleteConfirm()
    {
        const string title = "Delete preset?##DeletePresetMain";

        if (_pendingDelete != null && !ImGui.IsPopupOpen(title))
            ImGui.OpenPopup(title);

        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f));
        using var popup = ImRaii.PopupModal(title, ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup || _pendingDelete == null)
            return;

        ImGui.TextUnformatted($"Delete \"{_pendingDelete.Name}\"? This can't be undone.");
        ImGui.Spacing();

        if (ImGui.Button("Delete", new Vector2(120 * Ui.Scale, 0)))
        {
            actions.DeletePreset(_pendingDelete);
            _pendingDelete = null;
            _selectedId = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(120 * Ui.Scale, 0)))
        {
            _pendingDelete = null;
            ImGui.CloseCurrentPopup();
        }
    }
}
