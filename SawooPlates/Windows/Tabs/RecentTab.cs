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
    private bool _openSavePopup;

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

    private void DrawMulti(List<CapturedPortrait> filtered)
    {
        var marked = filtered.Where(e => _selection.Marked.Contains(e.Id)).ToList();

        ImGui.TextColored(Ui.Accent, $"{marked.Count} plates selected");
        ImGui.TextColored(Ui.Dim, "Ctrl+click to add or remove, Shift+click to select a range.");
        ImGui.Spacing();

        if (Ui.CtrlDeleteButton("DeleteMarked", $"Remove {marked.Count} plates from Recent", "Remove selected"))
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
        if (Ui.CtrlDeleteButton("ClearAll", "Remove every plate from Recent. Saved presets stay."))
        {
            captureService.ClearAll();
            _selection.ClearMarked();
            _selection.Current = null;
        }
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

    private void DrawRowTooltip(CapturedPortrait entry)
    {
        using var tooltip = ImRaii.Tooltip();

        var size = new Vector2(96, 160) * Ui.Scale;
        if (entry.HasImage && thumbnails.TryGetThumbnail(entry.Id, (int)size.X, (int)size.Y, out _, out var wrap, out _) && wrap != null)
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
                ImGui.TextColored(Ui.Good, "You have everything");
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

        // summary
        var missing = plateItems.CountMissing(capture, capture.Preset, capture.FrameKnown, capture.DecorationKnown);
        ImGui.Spacing();
        if (missing > 0)
        {
            Ui.Icon(FontAwesomeIcon.ExclamationCircle, Ui.Bad);
            ImGui.SameLine();
            ImGui.TextUnformatted(missing == 1 ? "You're missing 1 part of this plate." : $"You're missing {missing} parts of this plate.");
        }
        else
        {
            Ui.Icon(FontAwesomeIcon.CheckCircle, Ui.Good);
            ImGui.SameLine();
            ImGui.TextUnformatted("You have every part of this plate.");
        }

        // gear is only a heads-up: it doesn't stop the plate from being applied
        var missingGear = plateItems.GetMissingGear(capture);
        if (missingGear.Count > 0)
        {
            Ui.Icon(FontAwesomeIcon.Tshirt, Ui.Warn);
            ImGui.SameLine();
            ImGui.TextUnformatted(missingGear.Count == 1 ? "1 gear piece isn't in your collection." : $"{missingGear.Count} gear pieces aren't in your collection.");
            if (ImGui.IsItemHovered())
            {
                using var tip = ImRaii.Tooltip();
                plateItems.DrawMissingList(missingGear, "Not found in your inventory, armoury, glamour dresser or armoire:");
            }
        }

        if (!capture.FrameKnown || !capture.DecorationKnown)
            ImGui.TextColored(Ui.Dim, "This screen doesn't show frames, accents or the plate design.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // portrait on the left, items on the right
        if (capture.Plate is { } plateDesign)
        {
            // the whole Adventurer Plate across the top, the list of parts below it
            var width = Math.Min(ImGui.GetContentRegionAvail().X, 640 * Ui.Scale);
            plateRenderer.Draw(plateDesign, capture.Preset, thumbnails, capture.Id, width, capture.FrameKnown, capture.DecorationKnown);
            ImGui.Spacing();
        }
        else
        {
            var height = Math.Min(380 * Ui.Scale, ImGui.GetContentRegionAvail().Y);
            using (ImRaii.Group())
                portraitPanel.DrawPortrait(capture.Preset, thumbnails, capture.Id, height, capture.FrameKnown, capture.DecorationKnown);

            ImGui.SameLine(0, 14 * Ui.Scale);
        }

        using var items = ImRaii.Child("##Items", new Vector2(-1, -1), false);
        if (!items.Success)
            return;

        var gearPending = capture.Gear.Count == 0 && DateTimeOffset.UtcNow - capture.Time < TimeSpan.FromSeconds(5);
        plateItems.Draw(capture, capture.Preset, capture.FrameKnown, capture.DecorationKnown, gearPending);

        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Camera and lighting"))
            portraitPanel.DrawValues(capture.Preset);
    }

    private void DrawActions(CapturedPortrait capture)
    {
        applyControls.Draw(capture.Preset, capture.Plate, PortraitActions.GetImportFlags(capture), capture.FrameKnown, capture.DecorationKnown);

        ImGui.SameLine();
        if (Ui.Button(FontAwesomeIcon.Save, "Save as preset"))
        {
            _saveName = capture.DefaultPresetName;
            _openSavePopup = true;
        }

        ImGui.SameLine();
        if (Ui.Button(FontAwesomeIcon.Copy, "Copy code", tooltip: "Copy the preset code to share or paste in the Portrait Editor."))
            actions.CopyCode(capture.Preset, capture.Name);

        ImGui.SameLine(ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX() - ImGui.GetFrameHeight());
        if (Ui.CtrlDeleteButton("Delete", "Remove from Recent"))
        {
            captureService.Remove(capture.Id);
            _selection.Current = null;
        }

        DrawSavePopup(capture);
    }

    private void DrawSavePopup(CapturedPortrait capture)
    {
        const string title = "Save as preset##SaveCapture";

        if (_openSavePopup)
        {
            ImGui.OpenPopup(title);
            _openSavePopup = false;
        }

        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f));
        using var popup = ImRaii.PopupModal(title, ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup)
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

}
