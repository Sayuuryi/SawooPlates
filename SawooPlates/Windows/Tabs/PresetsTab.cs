using SawooPlates.PortraitEditor.Enums;
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
    PlateItems plateItems,
    PlateRenderer plateRenderer,
    ApplyControls applyControls)
{
    private string _search = string.Empty;
    private Guid? _tagFilter;
    private readonly ListSelection _selection = new();
    private List<Guid> _order = [];

    private SavedPreset? _editing;
    private string _editName = string.Empty;
    private readonly HashSet<Guid> _editTags = [];
    private string _newTag = string.Empty;
    private bool _openEdit;

    private List<SavedPreset> _deleting = [];
    private bool _openDelete;

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

        _selection.Keep(presets.Select(p => p.Id));
        var selected = presets.FirstOrDefault(p => p.Id == _selection.Current) ?? presets.FirstOrDefault();
        _selection.Current = selected?.Id;

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
                else if (_selection.IsMulti)
                    DrawMulti(presets);
                else
                    DrawDetail(selected);
            }
        }

        DrawEditPopup();
        DrawDeletePopup();
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
                _order = presets.Select(p => p.Id).ToList();
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

        if (ImGui.Selectable("##Row", _selection.IsSelected(preset.Id), ImGuiSelectableFlags.None, new Vector2(0, height)))
            _selection.Click(preset.Id, _order);

        if (ImGui.IsItemHovered())
            DrawRowTooltip(preset);

        if (ImGui.BeginPopupContextItem("##RowMenu"))
        {
            DrawRowMenu(preset);
            ImGui.EndPopup();
        }

        var end = ImGui.GetCursorPos();

        // tiny portrait thumbnail
        var thumb = new Vector2(height * 0.6f, height);
        ImGui.SetCursorPos(start);
        if (thumbnails.TryGetBest(preset.Id, thumb, out var wrap) && wrap != null)
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

    private void DrawRowMenu(SavedPreset preset)
    {
        if (_selection.IsMulti && _selection.Marked.Contains(preset.Id))
        {
            var marked = Config.Presets.Where(p => _selection.Marked.Contains(p.Id)).ToList();
            if (ImGui.MenuItem($"Delete {marked.Count} presets..."))
                AskDelete(marked);
            return;
        }

        ImGui.TextColored(Ui.Accent, preset.Name);
        ImGui.Separator();

        if (ImGui.MenuItem("Apply to my portrait", string.Empty, false, actions.CanApply))
            actions.Apply(preset.Preset!, choose: false);
        if (ImGui.MenuItem("Copy code"))
            actions.CopyCode(preset.Preset!);
        if (ImGui.MenuItem("Edit name and tags"))
            OpenEdit(preset);

        ImGui.Separator();
        if (ImGui.MenuItem("Delete preset..."))
            AskDelete([preset]);
    }

    private void AskDelete(List<SavedPreset> presets)
    {
        _deleting = presets;
        _openDelete = true;
    }

    private void OpenEdit(SavedPreset preset)
    {
        _editing = preset;
        _editName = preset.Name;
        _editTags.Clear();
        _editTags.UnionWith(preset.Tags);
        _newTag = string.Empty;
        _openEdit = true;
    }

    private void DrawDeletePopup()
    {
        const string title = "Delete?##DeletePresets";

        if (_openDelete)
        {
            ImGui.OpenPopup(title);
            _openDelete = false;
        }

        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f));
        using var popup = ImRaii.PopupModal(title, ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup || _deleting.Count == 0)
            return;

        ImGui.TextUnformatted(_deleting.Count == 1 ? $"Delete \"{_deleting[0].Name}\"?" : $"Delete {_deleting.Count} presets?");
        ImGui.TextColored(Ui.Dim, "They're also removed from the Portrait Editor's Preset Browser.");
        ImGui.Spacing();

        using (ImRaii.PushColor(ImGuiCol.Text, Ui.Bad))
        {
            if (ImGui.Button("Delete", new Vector2(120 * Ui.Scale, 0)))
            {
                foreach (var preset in _deleting)
                    actions.DeletePreset(preset);
                _deleting = [];
                _selection.ClearMarked();
                _selection.Current = null;
                ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(120 * Ui.Scale, 0)))
        {
            _deleting = [];
            ImGui.CloseCurrentPopup();
        }
    }

    private void DrawRowTooltip(SavedPreset preset)
    {
        using var tooltip = ImRaii.Tooltip();

        var size = new Vector2(96, 160) * Ui.Scale;
        if (thumbnails.TryGetBest(preset.Id, size, out var wrap) && wrap != null)
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

            var missing = plateItems.CountMissing(AsCapture(preset), preset.Preset!, true, true);
            ImGui.Spacing();
            if (missing > 0)
                ImGui.TextColored(Ui.Bad, missing == 1 ? "You're missing 1 part" : $"You're missing {missing} parts");
            else
                ImGui.TextColored(Ui.Good, "You have every part");

            ImGui.TextColored(Ui.Dim, "Right-click for more.");
        }
    }

    private static CapturedPortrait AsCapture(SavedPreset preset) => new() { Preset = preset.Preset!, Plate = preset.Plate };

    private string TagNames(SavedPreset preset)
        => string.Join(", ", preset.Tags.Select(id => Config.PresetTags.FirstOrDefault(t => t.Id == id)?.Name).Where(n => n != null));

    /// <summary>Selects one preset, e.g. right after it was saved from a plate.</summary>
    public void Select(Guid id)
    {
        _search = string.Empty;
        _tagFilter = null;
        _selection.Select(id);
    }

    private void DrawMulti(List<SavedPreset> presets)
    {
        var marked = presets.Where(p => _selection.Marked.Contains(p.Id)).ToList();

        ImGui.TextColored(Ui.Accent, $"{marked.Count} presets selected");
        ImGui.TextColored(Ui.Dim, "Ctrl+click to add or remove, Shift+click to select a range.");
        ImGui.Spacing();

        using (ImRaii.PushColor(ImGuiCol.Text, Ui.Bad))
        {
            if (Ui.Button(FontAwesomeIcon.Trash, $"Delete {marked.Count} presets..."))
                AskDelete(marked);
        }

        ImGui.SameLine();
        if (Ui.Button(FontAwesomeIcon.Times, "Clear selection"))
            _selection.ClearMarked();

        Ui.Section("Selected");
        foreach (var preset in marked)
            ImGui.BulletText(preset.Name);
    }

    private void DrawDetail(SavedPreset preset)
    {
        ImGui.TextColored(Ui.Accent, preset.Name);
        var tags = TagNames(preset);
        ImGui.TextColored(Ui.Dim, tags.Length > 0 ? tags : "No tags");

        ImGui.Spacing();

        applyControls.Draw(preset.Preset!, preset.Plate, ImportFlags.All);

        ImGui.SameLine();
        if (Ui.Button(FontAwesomeIcon.Copy, "Copy code"))
            actions.CopyCode(preset.Preset!);

        ImGui.SameLine();
        if (Ui.Button(FontAwesomeIcon.Pen, "Edit"))
            OpenEdit(preset);

        ImGui.Spacing();
        plateItems.DrawStatus(AsCapture(preset));

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (preset.Plate is { } plateDesign)
        {
            plateRenderer.Draw(plateDesign, preset.Preset!, thumbnails, preset.Id, ImGui.GetContentRegionAvail().X);
            ImGui.Dummy(new Vector2(0, 8 * Ui.Scale));
        }
        else
        {
            var height = Math.Min(380 * Ui.Scale, ImGui.GetContentRegionAvail().Y);
            using (ImRaii.Group())
                portraitPanel.DrawPortrait(preset.Preset!, thumbnails, preset.Id, height);

            ImGui.SameLine(0, 14 * Ui.Scale);
        }

        using var items = ImRaii.Child("##Items", new Vector2(-1, -1), false);
        if (!items.Success)
            return;

        plateItems.Draw(AsCapture(preset), preset.Preset!, showGearNote: false);

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

}
