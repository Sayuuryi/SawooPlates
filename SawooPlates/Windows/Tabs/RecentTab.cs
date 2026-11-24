using Dalamud.Interface.Components;
using SawooPlates.Capture;

namespace SawooPlates.Windows.Tabs;

public sealed class RecentTab(
    ITextureProvider textures,
    CaptureService captureService,
    CaptureThumbnailService thumbnails,
    PortraitActions actions,
    PresetDetailPanel portraitPanel,
    PlateItems plateItems,
    PlateRenderer plateRenderer,
    ApplyControls applyControls)
{
    private enum SortMode { Newest, Oldest, Name }

    private static readonly string[] SortLabels = ["Newest first", "Oldest first", "By name"];

    private string _search = string.Empty;
    private CaptureSource? _sourceFilter;
    private byte _jobFilter;
    private SortMode _sort = SortMode.Newest;
    private readonly ListSelection _selection = new();
    private bool _scrollToSelected;
    private List<Guid> _order = [];

    private string _saveName = string.Empty;
    private CapturedPortrait? _saveTarget;
    private bool _openSavePopup;
    private bool _openClearPopup;

    /// <summary>Selects an entry, e.g. when opened from the plate button.</summary>
    public void Select(Guid id)
    {
        _selection.Select(id);
        _search = string.Empty;
        _sourceFilter = null;
        _jobFilter = 0;
        _scrollToSelected = true;
    }

    public void Draw()
    {
        DrawContent();
        DrawPopups();
    }

    private void DrawContent()
    {
        var entries = captureService.History.Entries;

        if (entries.Count == 0)
        {
            Ui.EmptyState(FontAwesomeIcon.IdCard, "Nothing here yet",
                "Open someone's Adventurer Plate and it shows up here, with its portrait and plate design.");
            return;
        }

        var filtered = Filter(entries).ToList();
        _selection.Keep(filtered.Select(e => e.Id));
        var selected = filtered.FirstOrDefault(e => e.Id == _selection.Current) ?? filtered.FirstOrDefault();
        _selection.Current = selected?.Id;

        using (var left = ImRaii.Child("##RecentLeft", new Vector2(270 * Ui.Scale, -1), false))
        {
            if (left)
                DrawList(entries, filtered);
        }

        ImGui.SameLine(0, 12 * Ui.Scale);

        using var right = ImRaii.Child("##RecentRight", new Vector2(-1, -1), false);
        if (!right.Success)
            return;

        if (selected == null)
        {
            Ui.EmptyState(FontAwesomeIcon.Search, "No matches", "Nothing matches the search or filters.");
            return;
        }

        if (_selection.IsMulti)
            DrawMulti(filtered);
        else
            DrawDetail(selected);
    }

    /// <summary>Popups opened from the list or the detail view.</summary>
    private void DrawPopups()
    {
        DrawSavePopup();
        DrawClearPopup();
    }

    private void DrawMulti(List<CapturedPortrait> filtered)
    {
        var marked = filtered.Where(e => _selection.Marked.Contains(e.Id)).ToList();

        ImGui.TextColored(Ui.Accent, $"{marked.Count} plates selected");
        ImGui.TextColored(Ui.Dim, "Ctrl+click to add or remove, Shift+click to select a range.");
        ImGui.Spacing();

        if (Ui.CtrlDeleteButton("RemoveMarked", $"Remove {marked.Count} plates from Recent.", $"Remove {marked.Count} from Recent"))
        {
            foreach (var entry in marked)
                captureService.Remove(entry.Id);
            _selection.ClearMarked();
            _selection.Current = null;
        }

        ImGui.SameLine();
        if (Ui.Button(FontAwesomeIcon.Times, "Clear selection"))
            _selection.ClearMarked();

        Ui.Section("Selected");
        foreach (var entry in marked)
        {
            Ui.JobIcon(textures, entry.JobId, ImGui.GetTextLineHeight());
            ImGui.SameLine();
            ImGui.TextUnformatted(entry.Name);
            ImGui.SameLine();
            ImGui.TextColored(Ui.Dim, $"{entry.WorldName} · {Ui.TimeAgo(entry.Time)}");
        }
    }

    // ---------------------------------------------------------------- list

    private void DrawList(IReadOnlyList<CapturedPortrait> all, List<CapturedPortrait> filtered)
    {
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##Search", "Search name, world or job", ref _search, 100);

        var half = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2f;

        ImGui.SetNextItemWidth(half);
        using (var combo = ImRaii.Combo("##Source", _sourceFilter is { } s ? Ui.SourceName(s) : "All sources"))
        {
            if (combo)
            {
                if (ImGui.Selectable("All sources", _sourceFilter == null))
                    _sourceFilter = null;

                foreach (var source in Enum.GetValues<CaptureSource>())
                {
                    if (ImGui.Selectable(Ui.SourceName(source), _sourceFilter == source))
                        _sourceFilter = source;
                }
            }
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        var jobs = all.Where(e => e.JobId != 0).GroupBy(e => e.JobId).Select(g => (Id: g.Key, Name: g.First().Job)).OrderBy(j => j.Name).ToList();
        var jobLabel = _jobFilter == 0 ? "All jobs" : jobs.FirstOrDefault(j => j.Id == _jobFilter).Name ?? "All jobs";
        using (var combo = ImRaii.Combo("##Job", jobLabel))
        {
            if (combo)
            {
                if (ImGui.Selectable("All jobs", _jobFilter == 0))
                    _jobFilter = 0;

                foreach (var job in jobs)
                {
                    if (ImGui.Selectable($"{job.Name}##{job.Id}", _jobFilter == job.Id))
                        _jobFilter = job.Id;
                }
            }
        }

        var footer = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y;
        using (var list = ImRaii.Child("##List", new Vector2(-1, -footer), true))
        {
            if (list)
            {
                _order = filtered.Select(e => e.Id).ToList();
                foreach (var entry in filtered)
                    DrawListRow(entry);
            }
        }

        ImGui.SetNextItemWidth(130 * Ui.Scale);
        var sort = (int)_sort;
        if (ImGui.Combo("##Sort", ref sort, SortLabels, SortLabels.Length))
            _sort = (SortMode)sort;

        ImGui.SameLine();
        ImGui.TextColored(Ui.Dim, $"{filtered.Count} of {all.Count}");

        ImGui.SameLine(ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX() - ImGui.GetFrameHeight());
        if (ImGuiComponents.IconButton("##Clear", FontAwesomeIcon.Broom))
            _openClearPopup = true;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Empty Recent\nClears your list of recently viewed Adventure Plates. Saved presets stay.");
    }

    private void DrawListRow(CapturedPortrait entry)
    {
        using var id = ImRaii.PushId(entry.Id.ToString());

        var height = 40 * Ui.Scale;
        var start = ImGui.GetCursorPos();
        var isSelected = _selection.IsSelected(entry.Id);

        if (ImGui.Selectable("##Row", isSelected, ImGuiSelectableFlags.None, new Vector2(0, height)))
            _selection.Click(entry.Id, _order);

        if (ImGui.IsItemHovered())
            DrawRowTooltip(entry);

        if (ImGui.BeginPopupContextItem("##RowMenu"))
        {
            DrawRowMenu(entry);
            ImGui.EndPopup();
        }

        if (isSelected && _scrollToSelected)
        {
            ImGui.SetScrollHereY();
            _scrollToSelected = false;
        }

        var end = ImGui.GetCursorPos();
        var icon = 32 * Ui.Scale;

        ImGui.SetCursorPos(start + new Vector2(2 * Ui.Scale, (height - icon) / 2f));
        Ui.JobIcon(textures, entry.JobId, icon);

        var textX = start.X + icon + 10 * Ui.Scale;
        var lineHeight = ImGui.GetTextLineHeight();
        ImGui.SetCursorPos(new Vector2(textX, start.Y + height / 2f - lineHeight));
        ImGui.TextUnformatted(entry.Name);

        ImGui.SetCursorPos(new Vector2(textX, start.Y + height / 2f));
        var sub = string.Join(" · ", new[] { entry.Job, entry.WorldName, Ui.TimeAgo(entry.Time) }.Where(x => !string.IsNullOrEmpty(x)));
        ImGui.TextColored(Ui.Dim, sub);

        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var iconText = Ui.SourceIcon(entry.Source).ToIconString();
            var w = ImGui.CalcTextSize(iconText).X;
            ImGui.SetCursorPos(new Vector2(ImGui.GetWindowContentRegionMax().X - w - 4 * Ui.Scale, start.Y + (height - lineHeight) / 2f));
            ImGui.TextColored(Ui.Dim, iconText);
        }

        ImGui.SetCursorPos(end);
    }

    private void DrawRowMenu(CapturedPortrait entry)
    {
        // right-click on one of several selected rows acts on all of them
        if (_selection.IsMulti && _selection.Marked.Contains(entry.Id))
        {
            var ids = _selection.Marked.ToList();
            if (ImGui.MenuItem($"Remove {ids.Count} plates from Recent"))
            {
                foreach (var id in ids)
                    captureService.Remove(id);
                _selection.ClearMarked();
                _selection.Current = null;
            }

            return;
        }

        ImGui.TextColored(Ui.Accent, entry.Name);
        ImGui.Separator();

        if (ImGui.MenuItem("Apply to my portrait", string.Empty, false, actions.CanApply))
            actions.Apply(entry, choose: false);
        if (ImGui.MenuItem("Save as preset"))
            OpenSave(entry);
        if (ImGui.MenuItem("Copy code"))
            actions.CopyCode(entry.Preset, entry.Name);

        ImGui.Separator();
        if (ImGui.MenuItem("Remove from Recent"))
        {
            captureService.Remove(entry.Id);
            if (_selection.Current == entry.Id)
                _selection.Current = null;
        }
    }

    private void OpenSave(CapturedPortrait capture)
    {
        _saveTarget = capture;
        _saveName = capture.DefaultPresetName;
        _openSavePopup = true;
    }

    private void DrawRowTooltip(CapturedPortrait entry)
    {
        using var tooltip = ImRaii.Tooltip();

        var size = new Vector2(96, 160) * Ui.Scale;
        if (entry.HasImage && thumbnails.TryGetBest(entry.Id, size, out var wrap) && wrap != null)
        {
            ImGui.Image(wrap.Handle, size);
            ImGui.SameLine(0, 10 * Ui.Scale);
        }

        using (ImRaii.Group())
        {
            ImGui.TextColored(Ui.Accent, entry.Name);
            if (!string.IsNullOrEmpty(entry.Title))
                ImGui.TextUnformatted(entry.Title);
            ImGui.TextColored(Ui.Dim, string.Join(" · ", new[] { entry.Job, entry.WorldName }.Where(x => !string.IsNullOrEmpty(x))));
            ImGui.Spacing();
            ImGui.TextColored(Ui.Dim, $"{Ui.SourceName(entry.Source)}, {entry.Time.ToLocalTime():g}");

            var missing = plateItems.CountMissing(entry, entry.Preset, entry.FrameKnown, entry.DecorationKnown);
            ImGui.Spacing();
            if (missing > 0)
                ImGui.TextColored(Ui.Bad, missing == 1 ? "You're missing 1 part" : $"You're missing {missing} parts");
            else
                ImGui.TextColored(Ui.Good, "You have every part");

            ImGui.TextColored(Ui.Dim, "Right-click for more.");
        }
    }

    private IEnumerable<CapturedPortrait> Filter(IEnumerable<CapturedPortrait> entries)
    {
        var query = entries;

        if (!string.IsNullOrWhiteSpace(_search))
        {
            var s = _search.Trim();
            query = query.Where(e =>
                e.Name.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                e.WorldName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                e.Job.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                e.Title.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        if (_sourceFilter is { } source)
            query = query.Where(e => e.Source == source);

        if (_jobFilter != 0)
            query = query.Where(e => e.JobId == _jobFilter);

        return _sort switch
        {
            SortMode.Oldest => query.OrderBy(e => e.Time),
            SortMode.Name => query.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase),
            _ => query.OrderByDescending(e => e.Time),
        };
    }

    // ---------------------------------------------------------------- detail

    private void DrawDetail(CapturedPortrait capture)
    {
        // header
        var icon = 40 * Ui.Scale;
        Ui.JobIcon(textures, capture.JobId, icon);
        ImGui.SameLine(0, 10 * Ui.Scale);

        using (ImRaii.Group())
        {
            ImGui.TextColored(Ui.Accent, capture.Name);
            var line = string.Join(" · ", new[] { capture.Title, capture.WorldName, capture.Job }.Where(x => !string.IsNullOrEmpty(x)));
            ImGui.TextUnformatted(line);
            Ui.Icon(Ui.SourceIcon(capture.Source), Ui.Dim);
            ImGui.SameLine();
            ImGui.TextColored(Ui.Dim, $"{Ui.SourceName(capture.Source)} · {Ui.TimeAgo(capture.Time)}");
        }

        ImGui.Spacing();
        DrawActions(capture);

        ImGui.Spacing();
        plateItems.DrawStatus(capture, capture.FrameKnown, capture.DecorationKnown);

        if (!capture.FrameKnown || !capture.DecorationKnown)
            ImGui.TextColored(Ui.Dim, "This screen doesn't show frames, accents or the plate design.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // plate on the left, parts on the right (stacked when the window is narrow)
        Ui.DetailColumns("Recent", () => DrawVisual(capture), () =>
        {
            var gearPending = capture.Gear.Count == 0 && DateTimeOffset.UtcNow - capture.Time < TimeSpan.FromSeconds(5);
            plateItems.Draw(capture, capture.Preset, capture.FrameKnown, capture.DecorationKnown, gearPending);
        });
    }

    /// <summary>The plate (or the portrait alone), with camera and lighting under it.</summary>
    private void DrawVisual(CapturedPortrait capture)
    {
        if (capture.Plate is { } plateDesign)
        {
            plateRenderer.DrawFitted(plateDesign, capture.Preset, thumbnails, capture.Id, capture.FrameKnown, capture.DecorationKnown);
        }
        else
        {
            var height = Math.Clamp(ImGui.GetWindowHeight() * 0.6f, 160 * Ui.Scale, 380 * Ui.Scale);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, (ImGui.GetContentRegionAvail().X - height * 0.6f) / 2f));
            portraitPanel.DrawPortrait(capture.Preset, thumbnails, capture.Id, height, capture.FrameKnown, capture.DecorationKnown);
        }

        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Camera and lighting"))
            portraitPanel.DrawValues(capture.Preset);
    }

    private void DrawActions(CapturedPortrait capture)
    {
        applyControls.Draw(capture.Preset, capture.Plate, PortraitActions.GetImportFlags(capture), capture.FrameKnown, capture.DecorationKnown);

        ImGui.SameLine();
        if (Ui.Button(FontAwesomeIcon.Save, "Save as preset"))
            OpenSave(capture);

        ImGui.SameLine();
        if (Ui.Button(FontAwesomeIcon.Copy, "Copy code", tooltip: "Copy the preset code to share or paste in the Portrait Editor."))
            actions.CopyCode(capture.Preset, capture.Name);

    }

    private void DrawSavePopup()
    {
        const string title = "Save as preset##SaveCapture";

        if (_openSavePopup)
        {
            ImGui.OpenPopup(title);
            _openSavePopup = false;
        }

        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f));
        using var popup = ImRaii.PopupModal(title, ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup || _saveTarget is not { } capture)
            return;

        ImGui.TextUnformatted("Name");
        if (ImGui.IsWindowAppearing())
            ImGui.SetKeyboardFocusHere();

        ImGui.SetNextItemWidth(320 * Ui.Scale);
        ImGui.InputText("##Name", ref _saveName, PortraitEditor.Utils.Constants.PresetNameMaxLength);
        ImGui.TextColored(Ui.Dim, $"It gets the tag \"{PortraitActions.CapturedTagName}\".");

        ImGui.Spacing();

        var canSave = !string.IsNullOrWhiteSpace(_saveName);
        using (ImRaii.Disabled(!canSave))
        {
            if (ImGui.Button("Save", new Vector2(120 * Ui.Scale, 0))
                || (canSave && (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter))))
            {
                actions.SaveAsPreset(capture, _saveName);
                ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(120 * Ui.Scale, 0)))
            ImGui.CloseCurrentPopup();
    }

    private void DrawClearPopup()
    {
        const string title = "Empty Recent?##ClearRecent";

        if (_openClearPopup)
        {
            ImGui.OpenPopup(title);
            _openClearPopup = false;
        }

        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f));
        using var popup = ImRaii.PopupModal(title, ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup)
            return;

        ImGui.TextUnformatted($"This empties your list of recently viewed Adventure Plates ({captureService.History.Entries.Count}).");
        ImGui.TextColored(Ui.Dim, "Saved presets stay. Plates come back when you open them again.");
        ImGui.Spacing();

        if (ImGui.Button("Empty Recent", new Vector2(140 * Ui.Scale, 0)))
        {
            captureService.ClearAll();
            _selection.ClearMarked();
            _selection.Current = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(120 * Ui.Scale, 0)))
            ImGui.CloseCurrentPopup();
    }
}
