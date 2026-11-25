using SawooPlates.Capture;
using SawooPlates.PortraitEditor.Enums;

namespace SawooPlates.Windows;

/// <summary>
/// "Apply to my portrait" and "Use plate design" buttons.
/// Their tooltips list what you're missing; a click asks first when something is missing,
/// and Ctrl+click lets you pick which parts to use.
/// </summary>
public sealed class ApplyControls(
    PortraitActions actions,
    PlateDesignApplier designApplier,
    PlateItems plateItems,
    PlatePartsWindow partsWindow)
{
    private const string ConfirmPortraitPopup = "Apply portrait?##SawooConfirmPortrait";

    private PortraitPresetData? _pendingPreset;
    private ImportFlags _pendingFlags;
    private List<PlateItems.PartInfo> _pendingMissing = [];


    private string? _openPopup;

    /// <param name="preview">Draws a plate design at a width; used for the preview in the choose-parts window.</param>
    public void Draw(PortraitPresetData preset, PlateDesignData? plate, ImportFlags flags, bool frameKnown = true, bool decorationKnown = true,
        Action<PlateDesignData, float>? preview = null, string? title = null)
    {
        var portraitMissing = Missing(plateItems.GetParts(null, preset, "Portrait", frameKnown, decorationKnown));

        // --- portrait
        var canPortrait = actions.CanApplyOrOpen;
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
                ImGui.TextUnformatted(actions.CanApply
                    ? "Applies this portrait to your Portrait Editor."
                    : "Opens your Portrait Editor and applies this portrait.");
                ImGui.TextColored(Ui.Dim, "Hold Ctrl while clicking to choose which parts to apply.");
            }
            else
            {
                ImGui.TextUnformatted("Your Portrait Editor can't be opened right now.");
            }

            plateItems.DrawMissingList(portraitMissing);
        }

        // --- plate design
        if (plate != null)
        {
            var plateParts = plateItems.GetParts(new CapturedPortrait { Preset = preset, Plate = plate }, preset, "Plate");
            var plateMissing = Missing(plateParts);
            var canPlate = designApplier.CanUse;

            ImGui.SameLine();
            if (Ui.Button(FontAwesomeIcon.IdCard, "Use plate design", !canPlate))
            {
                if (ImGui.GetIO().KeyCtrl)
                    partsWindow.Show(title ?? string.Empty, plate, plateParts, preview, designApplier.Use);
                else
                    designApplier.Use(plate, PlateDesignApplier.Parts.All);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                using var tip = ImRaii.Tooltip();
                if (canPlate)
                {
                    ImGui.TextUnformatted("Opens Edit Plate on your Adventurer Plate with a list of what to pick in each row.");
                    ImGui.TextColored(Ui.Dim, "Hold Ctrl while clicking to choose which parts to use.");
                }
                else
                {
                    ImGui.TextUnformatted("Your Adventurer Plate can't be opened right now.");
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

    }
}
