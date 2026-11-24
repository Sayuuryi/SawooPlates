using SawooPlates.Capture;

namespace SawooPlates.Windows.Tabs;

public sealed class RecentTab(
    TextService textService,
    CaptureService captureService,
    CaptureThumbnailService thumbnails,
    PortraitActions actions,
    PresetDetailPanel detailPanel)
{
    private enum SortMode { Newest, Oldest, Name }

    private string _search = string.Empty;
    private int _sourceFilter = -1; // -1 = all
    private string _jobFilter = string.Empty;
    private SortMode _sort = SortMode.Newest;
    private Guid? _selectedId;

    private string _saveName = string.Empty;
    private bool _openSavePopup;
    private bool _confirmClear;

    public void Draw()
    {
        var entries = captureService.History.Entries;

        if (entries.Count == 0)
        {
            ImGui.TextWrapped(textService.Translate("SawooPlates.Recent.Empty"));
            return;
        }

        DrawFilters(entries);

        var filtered = Filter(entries).ToList();
        var selected = filtered.FirstOrDefault(e => e.Id == _selectedId) ?? filtered.FirstOrDefault();
        _selectedId = selected?.Id;

        var listWidth = 260 * ImStyle.Scale;

        using (var list = ImRaii.Child("##RecentList", new Vector2(listWidth, -1), true))
        {
            if (list)
            {
                foreach (var entry in filtered)
                {
                    using var id = ImRaii.PushId(entry.Id.ToString());
                    var label = string.IsNullOrEmpty(entry.Job) ? entry.Name : $"{entry.Name}  ({entry.Job})";
                    if (ImGui.Selectable(label, entry.Id == _selectedId))
                        _selectedId = entry.Id;

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip($"{entry.Name}@{entry.WorldName}\n{SourceName(entry.Source)} · {entry.Time.ToLocalTime():g}");
                }
            }
        }

        ImGui.SameLine();

        using var detail = ImRaii.Child("##RecentDetail", new Vector2(-1, -1));
        if (!detail || selected == null)
            return;

        DrawDetail(selected);
    }

    private void DrawFilters(IReadOnlyList<CapturedPortrait> entries)
    {
        ImGui.SetNextItemWidth(200 * ImStyle.Scale);
        ImGui.InputTextWithHint("##Search", textService.Translate("SawooPlates.Recent.SearchHint"), ref _search, 100);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(150 * ImStyle.Scale);
        var sourceLabel = _sourceFilter < 0 ? textService.Translate("SawooPlates.Recent.AllSources") : SourceName((CaptureSource)_sourceFilter);
        using (var combo = ImRaii.Combo("##Source", sourceLabel))
        {
            if (combo)
            {
                if (ImGui.Selectable(textService.Translate("SawooPlates.Recent.AllSources"), _sourceFilter < 0))
                    _sourceFilter = -1;

                foreach (var source in Enum.GetValues<CaptureSource>())
                {
                    if (ImGui.Selectable(SourceName(source), _sourceFilter == (int)source))
                        _sourceFilter = (int)source;
                }
            }
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(90 * ImStyle.Scale);
        var jobLabel = string.IsNullOrEmpty(_jobFilter) ? textService.Translate("SawooPlates.Recent.AllJobs") : _jobFilter;
        using (var combo = ImRaii.Combo("##Job", jobLabel))
        {
            if (combo)
            {
                if (ImGui.Selectable(textService.Translate("SawooPlates.Recent.AllJobs"), string.IsNullOrEmpty(_jobFilter)))
                    _jobFilter = string.Empty;

                foreach (var job in entries.Select(e => e.Job).Where(j => !string.IsNullOrEmpty(j)).Distinct().Order())
                {
                    if (ImGui.Selectable(job, _jobFilter == job))
                        _jobFilter = job;
                }
            }
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(110 * ImStyle.Scale);
        using (var combo = ImRaii.Combo("##Sort", textService.Translate($"SawooPlates.Recent.Sort.{_sort}")))
        {
            if (combo)
            {
                foreach (var mode in Enum.GetValues<SortMode>())
                {
                    if (ImGui.Selectable(textService.Translate($"SawooPlates.Recent.Sort.{mode}"), _sort == mode))
                        _sort = mode;
                }
            }
        }

        ImGui.SameLine();
        if (ImGuiUtils.IconButton("ClearHistory", FontAwesomeIcon.Trash, textService.Translate("SawooPlates.Recent.ClearAll.Tooltip")))
            _confirmClear = true;

        DrawClearConfirm();
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

        if (_sourceFilter >= 0)
            query = query.Where(e => (int)e.Source == _sourceFilter);

        if (!string.IsNullOrEmpty(_jobFilter))
            query = query.Where(e => e.Job == _jobFilter);

        return _sort switch
        {
            SortMode.Oldest => query.OrderBy(e => e.Time),
            SortMode.Name => query.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase),
            _ => query.OrderByDescending(e => e.Time),
        };
    }

    private void DrawDetail(CapturedPortrait capture)
    {
        ImGui.TextColored(Color.Gold, $"{capture.Name}@{capture.WorldName}");
        var sub = string.Join(" · ", new[] { capture.Job, capture.Title, SourceName(capture.Source), capture.Time.ToLocalTime().ToString("g") }.Where(s => !string.IsNullOrEmpty(s)));
        ImGui.TextDisabled(sub);

        if (!capture.FrameKnown || !capture.DecorationKnown)
        {
            using (Color.WarningForeground.Push(ImGuiCol.Text))
                ImGui.TextWrapped(textService.Translate("SawooPlates.Detail.PartialCapture"));
        }

        ImGui.Spacing();
        DrawButtons(capture);
        ImGui.Spacing();

        var height = Math.Min(320 * ImStyle.Scale, ImStyle.ContentRegionAvail.Y);
        detailPanel.DrawPortrait(capture.Preset, thumbnails, capture.Id, height, capture.FrameKnown, capture.DecorationKnown);
        ImGui.SameLine();

        using var values = ImRaii.Child("##Values", new Vector2(-1, height));
        if (values)
            detailPanel.DrawValues(capture.Preset, capture.FrameKnown, capture.DecorationKnown);
    }

    private void DrawButtons(CapturedPortrait capture)
    {
        if (ImGui.Button(textService.Translate("SawooPlates.Detail.SaveToPresets")))
        {
            _saveName = capture.DefaultPresetName;
            _openSavePopup = true;
        }

        ImGui.SameLine();
        using (ImRaii.Disabled(!actions.CanApply))
        {
            if (ImGui.Button(textService.Translate("SawooPlates.Detail.Apply")))
                actions.Apply(capture);
        }

        if (!actions.CanApply && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(textService.Translate("SawooPlates.Detail.Apply.Disabled"));

        ImGui.SameLine();
        if (ImGui.Button(textService.Translate("SawooPlates.Detail.CopyCode")))
            actions.CopyCode(capture.Preset, capture.Name);

        ImGui.SameLine();
        if (ImGuiUtils.IconButton("Delete", FontAwesomeIcon.Trash, textService.Translate("SawooPlates.Detail.Delete")))
        {
            captureService.Remove(capture.Id);
            _selectedId = null;
        }

        DrawSavePopup(capture);
    }

    private void DrawSavePopup(CapturedPortrait capture)
    {
        var title = textService.Translate("SawooPlates.Detail.SaveToPresets") + "##SaveCapture";

        if (_openSavePopup)
        {
            ImGui.OpenPopup(title);
            _openSavePopup = false;
        }

        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f));
        using var popup = ImRaii.PopupModal(title, ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup)
            return;

        ImGui.Text(textService.Translate("EditorWindows.CreatePresetDialog.Name.Label"));
        if (ImGui.IsWindowAppearing())
            ImGui.SetKeyboardFocusHere();

        ImGui.SetNextItemWidth(300 * ImStyle.Scale);
        ImGui.InputText("##Name", ref _saveName, PortraitEditor.Utils.Constants.PresetNameMaxLength);
        ImGui.TextDisabled(textService.Translate("SawooPlates.Detail.SaveTagHint", PortraitActions.CapturedTagName));

        ImGui.Spacing();

        var canSave = !string.IsNullOrWhiteSpace(_saveName);
        using (ImRaii.Disabled(!canSave))
        {
            if (ImGui.Button(textService.Translate("ConfirmationButtonWindow.Save"), new Vector2(120 * ImStyle.Scale, 0))
                || (canSave && (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter))))
            {
                actions.SaveAsPreset(capture, _saveName);
                ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();
        if (ImGui.Button(textService.Translate("ConfirmationButtonWindow.Cancel"), new Vector2(120 * ImStyle.Scale, 0)))
            ImGui.CloseCurrentPopup();
    }

    private void DrawClearConfirm()
    {
        var title = textService.Translate("SawooPlates.Recent.ClearAll.Tooltip") + "##ClearHistory";

        if (_confirmClear)
        {
            ImGui.OpenPopup(title);
            _confirmClear = false;
        }

        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f));
        using var popup = ImRaii.PopupModal(title, ImGuiWindowFlags.AlwaysAutoResize);
        if (!popup)
            return;

        ImGui.Text(textService.Translate("SawooPlates.Recent.ClearAll.Prompt"));
        ImGui.Spacing();

        if (ImGui.Button(textService.Translate("ConfirmationButtonWindow.Delete"), new Vector2(120 * ImStyle.Scale, 0)))
        {
            captureService.ClearAll();
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button(textService.Translate("ConfirmationButtonWindow.Cancel"), new Vector2(120 * ImStyle.Scale, 0)))
            ImGui.CloseCurrentPopup();
    }

    private string SourceName(CaptureSource source)
        => textService.Translate($"SawooPlates.Source.{source}");
}
