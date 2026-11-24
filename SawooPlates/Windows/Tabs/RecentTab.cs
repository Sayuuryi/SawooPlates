using SawooPlates.Capture;

namespace SawooPlates.Windows.Tabs;

public sealed class RecentTab(
    ITextureProvider textures,
    CaptureService captureService,
    CaptureThumbnailService thumbnails,
    PortraitActions actions,
    PresetDetailPanel portraitPanel,
    PlateItems plateItems,
    PlateDesignApplier designApplier)
{
    private enum SortMode { Newest, Oldest, Name }

    private static readonly string[] SortLabels = ["Newest first", "Oldest first", "By name"];

    private string _search = string.Empty;
    private CaptureSource? _sourceFilter;
    private byte _jobFilter;
    private SortMode _sort = SortMode.Newest;
    private Guid? _selectedId;
    private bool _scrollToSelected;

    private string _saveName = string.Empty;
    private bool _openSavePopup;
    private bool _confirmClear;
    private bool _confirmDelete;

    /// <summary>Selects an entry, e.g. when opened from the plate button.</summary>
    public void Select(Guid id)
    {
        _selectedId = id;
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
                "Open someone's Adventurer Plate and click \"Save plate\" above it. Their portrait and plate design show up here.");
            return;
        }

        var filtered = Filter(entries).ToList();
        var selected = filtered.FirstOrDefault(e => e.Id == _selectedId) ?? filtered.FirstOrDefault();
        _selectedId = selected?.Id;

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

        DrawDetail(selected);
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
        if (Ui.IconButton("ClearAll", FontAwesomeIcon.Trash, "Clear Recent"))
            _confirmClear = true;

        DrawClearConfirm();
    }

    private void DrawListRow(CapturedPortrait entry)
    {
        using var id = ImRaii.PushId(entry.Id.ToString());

        var height = 40 * Ui.Scale;
        var start = ImGui.GetCursorPos();
        var isSelected = entry.Id == _selectedId;

        if (ImGui.Selectable("##Row", isSelected, ImGuiSelectableFlags.None, new Vector2(0, height)))
            _selectedId = entry.Id;

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
                ImGui.TextColored(Ui.Bad, missing == 1 ? "You're missing 1 thing" : $"You're missing {missing} things");
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
            ImGui.TextUnformatted(missing == 1 ? "You're missing 1 thing on this plate." : $"You're missing {missing} things on this plate.");
        }
        else
        {
            Ui.Icon(FontAwesomeIcon.CheckCircle, Ui.Good);
            ImGui.SameLine();
            ImGui.TextUnformatted("You have everything on this plate that can be checked.");
        }

        if (!capture.FrameKnown || !capture.DecorationKnown)
            ImGui.TextColored(Ui.Dim, "This screen doesn't show frames, accents or the plate design.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // portrait on the left, items on the right
        var height = Math.Min(380 * Ui.Scale, ImGui.GetContentRegionAvail().Y);
        using (ImRaii.Group())
            portraitPanel.DrawPortrait(capture.Preset, thumbnails, capture.Id, height, capture.FrameKnown, capture.DecorationKnown);

        ImGui.SameLine(0, 14 * Ui.Scale);

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
        if (Ui.Button(FontAwesomeIcon.Magic, "Apply to my portrait", !actions.CanApply,
                actions.CanApply ? "Opens Advanced Import with this portrait, so you can pick what to take." : "Open your Portrait Editor first."))
        {
            actions.Apply(capture);
        }

        var plate = capture.Plate;
        if (plate != null)
        {
            ImGui.SameLine();
            if (Ui.Button(FontAwesomeIcon.IdCard, "Apply plate design", !designApplier.CanApply,
                    designApplier.CanApply
                        ? "Puts this plate design in your design editor. Parts you haven't unlocked are skipped. Press Save in the game window to keep it."
                        : "Open your Adventurer Plate, then Edit > Design, to use this."))
            {
                var skipped = designApplier.Apply(plate);
                captureService.Notify(skipped > 0
                    ? $"Plate design applied. {skipped} part(s) you haven't unlocked were skipped. Press Save to keep it."
                    : "Plate design applied. Press Save in the design window to keep it.");
            }
        }

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
        if (Ui.IconButton("Delete", FontAwesomeIcon.Trash, "Remove from Recent"))
            _confirmDelete = true;

        DrawSavePopup(capture);
        DrawDeleteConfirm(capture);
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

    private void DrawDeleteConfirm(CapturedPortrait capture)
    {
        const string title = "Remove from Recent?##DeleteCapture";

        if (_confirmDelete)
        {
            ImGui.OpenPopup(title);
            _confirmDelete = false;
        }

        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f));
        using var popup = ImRaii.PopupModal(title, ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup)
            return;

        ImGui.TextUnformatted($"Remove {capture.Name}'s portrait from Recent?");
        ImGui.Spacing();

        if (ImGui.Button("Remove", new Vector2(120 * Ui.Scale, 0)))
        {
            captureService.Remove(capture.Id);
            _selectedId = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(120 * Ui.Scale, 0)))
            ImGui.CloseCurrentPopup();
    }

    private void DrawClearConfirm()
    {
        const string title = "Clear Recent?##ClearRecent";

        if (_confirmClear)
        {
            ImGui.OpenPopup(title);
            _confirmClear = false;
        }

        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f));
        using var popup = ImRaii.PopupModal(title, ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup)
            return;

        ImGui.TextUnformatted("Remove every portrait from Recent? Saved presets stay.");
        ImGui.Spacing();

        if (ImGui.Button("Clear", new Vector2(120 * Ui.Scale, 0)))
        {
            captureService.ClearAll();
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(120 * Ui.Scale, 0)))
            ImGui.CloseCurrentPopup();
    }
}
