using SawooPlates.Capture;
using SawooPlates.PortraitEditor.Enums;

namespace SawooPlates.Windows;

/// <summary>
/// "Apply to my portrait" and "Apply plate design" buttons.
/// Their tooltips list what you're missing; a click asks first when something is missing,
/// and Ctrl+click lets you pick which parts to apply.
/// </summary>
public sealed class ApplyControls(
    ITextureProvider textures,
    PortraitActions actions,
    PlateDesignApplier designApplier,
    PlateItems plateItems,
    CaptureService captureService)
{
    private const string ConfirmPortraitPopup = "Apply portrait?##SawooConfirmPortrait";
    private const string ConfirmPlatePopup = "Apply plate design?##SawooConfirmPlate";
    private const string ChoosePlatePopup = "Choose plate parts##SawooChoosePlate";

    private PortraitPresetData? _pendingPreset;
    private ImportFlags _pendingFlags;
    private List<PlateItems.PartInfo> _pendingMissing = [];

    private PlateDesignData? _pendingPlate;
    private List<PlateItems.PartInfo> _plateParts = [];
    private readonly HashSet<int> _chosen = [];

    private string? _openPopup;

    public void Draw(PortraitPresetData preset, PlateDesignData? plate, ImportFlags flags, bool frameKnown = true, bool decorationKnown = true)
    {
        var portraitMissing = Missing(plateItems.GetParts(null, preset, "Portrait", frameKnown, decorationKnown));

        // --- portrait
        var canPortrait = actions.CanApply;
        if (Ui.Button(FontAwesomeIcon.Magic, "Apply to my portrait", !canPortrait))
        {
            if (ImGui.GetIO().KeyCtrl)
                actions.Apply(preset, flags, choose: true);
            else if (portraitMissing.Count > 0)
                Ask(ConfirmPortraitPopup, () => { _pendingPreset = preset; _pendingFlags = flags; _pendingMissing = portraitMissing; });
            else
                actions.Apply(preset, flags);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            using var tip = ImRaii.Tooltip();
            if (canPortrait)
            {
                ImGui.TextUnformatted("Applies this portrait to your Portrait Editor.");
                ImGui.TextColored(Ui.Dim, "Hold Ctrl while clicking to choose which parts to apply.");
            }
            else
            {
                ImGui.TextUnformatted("Open your Portrait Editor first.");
            }

            plateItems.DrawMissingList(portraitMissing);
        }

        // --- plate design
        if (plate != null)
        {
            var plateParts = plateItems.GetParts(new CapturedPortrait { Preset = preset, Plate = plate }, preset, "Plate");
            var plateMissing = Missing(plateParts);
            var canPlate = designApplier.CanApply;

            ImGui.SameLine();
            if (Ui.Button(FontAwesomeIcon.IdCard, "Apply plate design", !canPlate))
            {
                if (ImGui.GetIO().KeyCtrl)
                {
                    Ask(ChoosePlatePopup, () =>
                    {
                        _pendingPlate = plate;
                        _plateParts = plateParts;
                        _chosen.Clear();
                        for (var i = 0; i < plateParts.Count; i++)
                        {
                            if (plateParts[i].Own.State == OwnState.Have)
                                _chosen.Add(i);
                        }
                    });
                }
                else if (plateMissing.Count > 0)
                {
                    Ask(ConfirmPlatePopup, () => { _pendingPlate = plate; _pendingMissing = plateMissing; });
                }
                else
                {
                    ApplyPlate(plate);
                }
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                using var tip = ImRaii.Tooltip();
                if (canPlate)
                {
                    ImGui.TextUnformatted("Puts this plate design in your design editor. Press Save there to keep it.");
                    ImGui.TextColored(Ui.Dim, "Hold Ctrl while clicking to choose which parts to apply.");
                }
                else
                {
                    ImGui.TextUnformatted("Open your Adventurer Plate and go to Edit Plate Design to use this.");
                }

                plateItems.DrawMissingList(plateMissing);
            }
        }

        DrawPopups();
    }

    private static List<PlateItems.PartInfo> Missing(List<PlateItems.PartInfo> parts)
        => parts.Where(p => p.Own.State == OwnState.Missing).ToList();

    private void Ask(string popup, Action setup)
    {
        setup();
        _openPopup = popup;
    }

    private void ApplyPlate(PlateDesignData plate)
    {
        var skipped = designApplier.Apply(plate);
        captureService.Notify(skipped > 0
            ? $"Plate design applied without {skipped} part(s) you don't have. Press Save to keep it."
            : "Plate design applied. Press Save in the design window to keep it.");
    }

    private void DrawPopups()
    {
        if (_openPopup != null)
        {
            ImGui.OpenPopup(_openPopup);
            _openPopup = null;
        }

        var buttonSize = new Vector2(120 * Ui.Scale, 0);

        // --- confirm portrait
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f));
        using (var popup = ImRaii.PopupModal(ConfirmPortraitPopup, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (popup.Success && _pendingPreset is { } pendingPreset)
            {
                ImGui.TextUnformatted(_pendingMissing.Count == 1
                    ? "The portrait will be applied without 1 part you don't have."
                    : $"The portrait will be applied without {_pendingMissing.Count} parts you don't have.");
                plateItems.DrawMissingList(_pendingMissing);
                ImGui.Spacing();
                ImGui.TextColored(Ui.Dim, "Tip: hold Ctrl when clicking Apply to choose the parts yourself.");
                ImGui.Spacing();

                if (ImGui.Button("Apply", buttonSize))
                {
                    actions.Apply(pendingPreset, _pendingFlags, choose: false);
                    _pendingPreset = null;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();
                if (ImGui.Button("Cancel", buttonSize))
                {
                    _pendingPreset = null;
                    ImGui.CloseCurrentPopup();
                }
            }
        }

        // --- confirm plate
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f));
        using (var popup = ImRaii.PopupModal(ConfirmPlatePopup, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (popup.Success && _pendingPlate is { } pendingPlate)
            {
                ImGui.TextUnformatted(_pendingMissing.Count == 1
                    ? "The plate design will be applied without 1 part you don't have."
                    : $"The plate design will be applied without {_pendingMissing.Count} parts you don't have.");
                plateItems.DrawMissingList(_pendingMissing);
                ImGui.Spacing();
                ImGui.TextColored(Ui.Dim, "Tip: hold Ctrl when clicking Apply to choose the parts yourself.");
                ImGui.Spacing();

                if (ImGui.Button("Apply", buttonSize))
                {
                    ApplyPlate(pendingPlate);
                    _pendingPlate = null;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();
                if (ImGui.Button("Cancel", buttonSize))
                {
                    _pendingPlate = null;
                    ImGui.CloseCurrentPopup();
                }
            }
        }

        // --- choose plate parts
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f));
        using (var popup = ImRaii.PopupModal(ChoosePlatePopup, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (popup.Success && _pendingPlate is { } pendingPlate)
            {
                ImGui.TextUnformatted("Pick the parts to apply. Parts you don't have can't be picked.");
                ImGui.Spacing();

                var icon = ImGui.GetFrameHeight();
                for (var i = 0; i < _plateParts.Count; i++)
                {
                    var part = _plateParts[i];
                    var have = part.Own.State == OwnState.Have;
                    var on = _chosen.Contains(i);

                    using (ImRaii.PushId(i))
                    using (ImRaii.Disabled(!have))
                    {
                        if (ImGui.Checkbox("##Pick", ref on))
                        {
                            if (on) _chosen.Add(i);
                            else _chosen.Remove(i);
                        }
                    }

                    ImGui.SameLine();
                    Ui.IconFit(textures, part.IconId, icon);
                    ImGui.SameLine();
                    ImGui.TextUnformatted(part.Name);
                    ImGui.SameLine();
                    ImGui.TextColored(have ? Ui.Dim : Ui.Bad, have ? part.Kind : $"{part.Kind} · you don't have this");
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                using (ImRaii.Disabled(_chosen.Count == 0))
                {
                    if (ImGui.Button("Apply", buttonSize))
                    {
                        ApplyPlate(Filter(pendingPlate));
                        _pendingPlate = null;
                        ImGui.CloseCurrentPopup();
                    }
                }

                ImGui.SameLine();
                if (ImGui.Button("Cancel", buttonSize))
                {
                    _pendingPlate = null;
                    ImGui.CloseCurrentPopup();
                }
            }
        }
    }

    /// <summary>The plate design with only the picked parts; the rest stay as they are on your plate.</summary>
    private PlateDesignData Filter(PlateDesignData plate)
    {
        var picked = _chosen.Select(i => _plateParts[i]).ToList();
        ushort Pick(PlateItems.PlatePart part) => picked.FirstOrDefault(p => p.Part == part)?.PartId ?? 0;

        return new PlateDesignData
        {
            BasePlate = Pick(PlateItems.PlatePart.Base),
            TopBorder = (byte)Pick(PlateItems.PlatePart.TopBorder),
            BottomBorder = (byte)Pick(PlateItems.PlatePart.BottomBorder),
            Decorations = picked.Where(p => p.Part == PlateItems.PlatePart.Decoration).Select(p => p.PartId).ToArray(),
            InvertPortraitPlacement = plate.InvertPortraitPlacement,
        };
    }
}
