using SawooPlates.Capture;
using SawooPlates.PortraitEditor.Services;

namespace SawooPlates.Windows;

/// <summary>
/// The list of things that make up a plate (portrait parts, plate design, gear), each with
/// whether the player has it. Kept short: icon, name, what it is, and a check or cross.
/// </summary>
public sealed class PlateItems(
    ITextureProvider textures,
    ExcelService excel,
    BannerService bannerService,
    CollectionService collection)
{
    private sealed record Row(uint IconId, string Name, string Kind, Ownership Own, List<(uint Rgb, string Name)>? Dyes = null, uint PreviewId = 0, string? Extra = null, PlatePart Part = PlatePart.None, ushort PartId = 0);

    /// <summary>Which plate design slot a row belongs to, for applying only some parts.</summary>
    public enum PlatePart { None, Base, TopBorder, BottomBorder, Decoration }

    /// <summary>One thing in a group, as shown in tooltips and the choose-parts window.</summary>
    public sealed record PartInfo(uint IconId, string Name, string Kind, Ownership Own, PlatePart Part, ushort PartId);

    /// <summary>Rows of one group ("Portrait", "Plate" or "Gear").</summary>
    public List<PartInfo> GetParts(CapturedPortrait? capture, PortraitPresetData preset, string group, bool frameKnown = true, bool decorationKnown = true)
        => BuildGroups(capture, preset, frameKnown, decorationKnown)
            .Where(g => g.Title == group)
            .SelectMany(g => g.Rows)
            .Select(Info)
            .ToList();

    private static PartInfo Info(Row r) => new(r.IconId, r.Name, r.Kind, r.Own, r.Part, r.PartId);

    /// <summary>
    /// Short status under the buttons: missing portrait/plate parts, missing gear, and the Ctrl tip.
    /// Hovering a line lists the items with their icons.
    /// </summary>
    public void DrawStatus(CapturedPortrait capture, bool frameKnown = true, bool decorationKnown = true)
    {
        var groups = BuildGroups(capture, capture.Preset, frameKnown, decorationKnown);
        var parts = groups.Where(g => g.Title != "Gear").SelectMany(g => g.Rows).Where(r => r.Own.State == OwnState.Missing).Select(Info).ToList();
        var gear = groups.Where(g => g.Title == "Gear").SelectMany(g => g.Rows).Where(r => r.Own.State == OwnState.Missing).Select(Info).ToList();
        var what = capture.Plate != null ? "plate" : "portrait";

        if (parts.Count > 0)
        {
            StatusLine(FontAwesomeIcon.ExclamationCircle, Ui.Bad,
                parts.Count == 1 ? $"You're missing 1 part of this {what}." : $"You're missing {parts.Count} parts of this {what}.");
            if (ImGui.IsItemHovered())
            {
                using var tip = ImRaii.Tooltip();
                DrawMissingList(parts, "These are skipped when you apply it:");
            }
        }
        else
        {
            StatusLine(FontAwesomeIcon.CheckCircle, Ui.Good, $"You have every part of this {what}.");
        }

        // gear is only a heads-up: it doesn't stop anything from being applied
        if (gear.Count > 0)
        {
            StatusLine(FontAwesomeIcon.Tshirt, Ui.Warn,
                gear.Count == 1 ? "1 gear piece isn't in your collection." : $"{gear.Count} gear pieces aren't in your collection.");
            if (ImGui.IsItemHovered())
            {
                using var tip = ImRaii.Tooltip();
                DrawMissingList(gear, "Not in your inventory, armoury, glamour dresser or armoire:");
            }
        }

        ImGui.TextColored(Ui.Dim, "Hold Ctrl when you click Apply to choose which parts to use.");
    }

    private static void StatusLine(FontAwesomeIcon icon, Vector4 color, string text)
    {
        using (ImRaii.Group())
        {
            Ui.Icon(icon, color);
            ImGui.SameLine();
            ImGui.TextUnformatted(text);
        }
    }

    /// <summary>Small list of missing things for a tooltip: icon, name, kind.</summary>
    public void DrawMissingList(IReadOnlyList<PartInfo> missing, string? title = null)
    {
        if (missing.Count == 0)
            return;

        ImGui.Spacing();
        ImGui.TextColored(Ui.Bad, title ?? (missing.Count == 1 ? "You're missing 1 part:" : $"You're missing {missing.Count} parts:"));
        var size = ImGui.GetTextLineHeight();
        foreach (var part in missing)
        {
            Ui.IconFit(textures, part.IconId, size);
            ImGui.SameLine();
            ImGui.TextUnformatted(part.Name);
            ImGui.SameLine();
            ImGui.TextColored(Ui.Dim, part.Kind);
        }
    }

    private sealed record Group(string Title, List<Row> Rows)
    {
        public int Have => Rows.Count(r => r.Own.State == OwnState.Have);
        public int Missing => Rows.Count(r => r.Own.State == OwnState.Missing);
    }

    /// <summary>Missing portrait and plate parts. Gear is counted separately (see <see cref="GetMissingGear"/>).</summary>
    public int CountMissing(CapturedPortrait? capture, PortraitPresetData preset, bool frameKnown, bool decorationKnown)
        => BuildGroups(capture, preset, frameKnown, decorationKnown).Where(g => g.Title != "Gear").Sum(g => g.Missing);

    /// <summary>Gear shown in the portrait that isn't in your inventory, armoury, dresser or armoire.</summary>
    public List<PartInfo> GetMissingGear(CapturedPortrait capture)
        => GetParts(capture, capture.Preset, "Gear").Where(p => p.Own.State == OwnState.Missing).ToList();

    public void Draw(CapturedPortrait? capture, PortraitPresetData preset, bool frameKnown = true, bool decorationKnown = true, bool gearPending = false, bool showGearNote = true)
    {
        var groups = BuildGroups(capture, preset, frameKnown, decorationKnown);

        var cells = new List<Action>();
        foreach (var group in groups)
        {
            cells.Add(() =>
            {
                var known = group.Rows.Count(r => r.Own.State != OwnState.Unknown);
                Ui.Section(group.Title, known > 0 ? $"{group.Have} of {known} owned" : null);
                DrawRows(group);
            });
        }

        if (showGearNote && capture != null && capture.Gear.Count == 0)
        {
            cells.Add(() =>
            {
                Ui.Section("Gear");
                ImGui.TextWrapped(gearPending
                    ? "Reading gear..."
                    : "No gear was read for this portrait. Open the plate again to fill it in.");
            });
        }

        // Portrait, Plate and Gear next to each other when there's room, so less scrolling
        var columns = Math.Clamp((int)(ImGui.GetContentRegionAvail().X / (300 * Ui.Scale)), 1, Math.Max(1, cells.Count));
        if (columns == 1)
        {
            foreach (var cell in cells)
                cell();
            return;
        }

        // roomy cells with a thin line between groups; the padding only applies to this outer table
        var padding = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(14 * Ui.Scale, 2 * Ui.Scale));
        var table = ImRaii.Table("##PartGroups", columns, ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.NoSavedSettings);
        padding.Dispose();

        using (table)
        {
            if (!table)
                return;

            foreach (var (cell, index) in cells.Select((c, i) => (c, i)))
            {
                ImGui.TableNextColumn();
                using var id = ImRaii.PushId(index);
                cell();
            }
        }
    }

    private void DrawRows(Group group)
    {
        var icon = 30 * Ui.Scale;

        using var table = ImRaii.Table($"##{group.Title}", 3, ImGuiTableFlags.None);
        if (!table)
            return;

        ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, icon);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Own", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());

        foreach (var (row, index) in group.Rows.Select((r, i) => (r, i)))
        {
            using var id = ImRaii.PushId(index);
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            var rowMin = ImGui.GetCursorScreenPos();
            Ui.IconFit(textures, row.IconId, icon);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Name);

            if (row.Dyes is { Count: > 0 })
            {
                ImGui.TextColored(Ui.Dim, row.Kind);
                foreach (var dye in row.Dyes)
                {
                    ImGui.SameLine(0, 10 * Ui.Scale);
                    Ui.DyeChip(dye.Rgb, dye.Name);
                }
            }
            else
            {
                ImGui.TextColored(Ui.Dim, row.Kind);
            }

            ImGui.TableNextColumn();
            var rowRight = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (icon - ImGui.GetTextLineHeight()) / 2f);
            Ui.OwnBadge(row.Own, tooltip: false);

            // whole row (within its column) is the hover target for the details tooltip
            var rowMax = new Vector2(rowRight, rowMin.Y + icon);
            if (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(rowMin, rowMax))
            {
                DrawTooltip(row);
                if (row.Own.Link != null && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                    Dalamud.Utility.Util.OpenLink(row.Own.Link);
            }
        }
    }

    private void DrawTooltip(Row row)
    {
        using var tooltip = ImRaii.Tooltip();
        var wrap = ImGui.GetFontSize() * 20;

        var preview = row.PreviewId != 0 ? row.PreviewId : row.IconId;
        if (preview != 0)
        {
            if (row.PreviewId != 0)
                Ui.ImageFit(textures, preview, new Vector2(280, 160) * Ui.Scale);
            else
                Ui.IconFit(textures, preview, 64 * Ui.Scale);
            ImGui.Spacing();
        }

        ImGui.TextColored(Ui.Accent, row.Name);
        ImGui.TextColored(Ui.Dim, row.Kind);

        if (row.Extra != null)
            ImGui.TextUnformatted(row.Extra);

        if (row.Dyes is { Count: > 0 })
        {
            foreach (var dye in row.Dyes)
                Ui.DyeChip(dye.Rgb, dye.Name);
        }

        ImGui.Spacing();
        switch (row.Own.State)
        {
            case OwnState.Have:
                Ui.Icon(FontAwesomeIcon.Check, Ui.Good);
                ImGui.SameLine();
                ImGui.TextColored(Ui.Good, row.Own.Where is { } where ? $"You have this ({where})" : "You have this");
                break;

            case OwnState.Missing:
                Ui.Icon(FontAwesomeIcon.Times, Ui.Bad);
                ImGui.SameLine();
                ImGui.TextColored(Ui.Bad, "You don't have this");
                break;

            default:
                Ui.Icon(FontAwesomeIcon.Question, Ui.Dim);
                ImGui.SameLine();
                ImGui.TextColored(Ui.Dim, "Can't be checked");
                break;
        }

        if (row.Own.Hint != null)
        {
            using (ImRaii.TextWrapPos(wrap))
                ImGui.TextUnformatted(row.Own.Hint);
        }

        if (row.Own.Link != null)
        {
            ImGui.Spacing();
            ImGui.TextColored(Ui.Dim, "Right-click to open it on Garland Tools.");
        }
    }

    private List<Group> BuildGroups(CapturedPortrait? capture, PortraitPresetData preset, bool frameKnown, bool decorationKnown)
    {
        var groups = new List<Group>();

        // --- portrait
        var portrait = new List<Row>();

        if (excel.TryGetRow<BannerBg>(preset.BannerBg, out var bg))
            portrait.Add(new Row((uint)bg.Icon, bg.Name.ToString(), "Background", collection.BannerBg(preset.BannerBg), PreviewId: (uint)bg.Image));

        if (!frameKnown)
            portrait.Add(new Row(0, "Not shown on this screen", "Frame", Ownership.Unknown("Party and MVP portraits don't show the frame.")));
        else if (excel.TryGetRow<BannerFrame>(preset.BannerFrame, out var frame))
            portrait.Add(new Row((uint)frame.Icon, frame.Name.ToString(), "Frame", collection.BannerFrame(preset.BannerFrame), PreviewId: (uint)frame.Image));

        if (!decorationKnown)
            portrait.Add(new Row(0, "Not shown on this screen", "Accent", Ownership.Unknown("Party and MVP portraits don't show the accent.")));
        else if (excel.TryGetRow<BannerDecoration>(preset.BannerDecoration, out var accent))
            portrait.Add(new Row((uint)accent.Icon, accent.Name.ToString(), "Accent", collection.BannerDecoration(preset.BannerDecoration), PreviewId: (uint)accent.Image));

        var poseIcon = excel.TryGetRow<BannerTimeline>(preset.BannerTimeline, out var pose) ? (uint)pose.Icon : 0u;
        portrait.Add(new Row(poseIcon, bannerService.GetBannerTimelineName(preset.BannerTimeline), "Pose", collection.BannerTimeline(preset.BannerTimeline)));

        if (preset.Expression == 0)
        {
            portrait.Add(new Row(0, "None", "Expression", new Ownership(OwnState.Have)));
        }
        else if (excel.TryGetRow<BannerFacial>(preset.Expression, out var facial) && excel.TryGetRow<Emote>(facial.Emote.RowId, out var emote))
        {
            portrait.Add(new Row(emote.Icon, emote.Name.ToString(), "Expression", collection.Expression(preset.Expression)));
        }

        groups.Add(new Group("Portrait", portrait));

        // --- plate design
        if (capture?.Plate is { } plate)
        {
            var rows = new List<Row>();

            if (plate.BasePlate != 0 && excel.TryGetRow<CharaCardBase>(plate.BasePlate, out var basePlate))
                rows.Add(new Row((uint)basePlate.Image, basePlate.Name.ToString(), "Base plate", collection.PlateBase(plate.BasePlate), PreviewId: (uint)basePlate.Image, Part: PlatePart.Base, PartId: plate.BasePlate));

            if (plate.TopBorder != 0 && excel.TryGetRow<CharaCardHeader>(plate.TopBorder, out var top))
                rows.Add(new Row((uint)top.TopImage, top.Name.ToString(), "Top border", collection.PlateHeader(plate.TopBorder), PreviewId: (uint)top.TopImage, Part: PlatePart.TopBorder, PartId: plate.TopBorder));

            if (plate.BottomBorder != 0 && excel.TryGetRow<CharaCardHeader>(plate.BottomBorder, out var bottom))
                rows.Add(new Row((uint)bottom.BottomImage, bottom.Name.ToString(), "Bottom border", collection.PlateHeader(plate.BottomBorder), PreviewId: (uint)bottom.BottomImage, Part: PlatePart.BottomBorder, PartId: plate.BottomBorder));

            foreach (var decoration in plate.Decorations.Where(d => d != 0))
            {
                if (!excel.TryGetRow<CharaCardDecoration>(decoration, out var row))
                    continue;

                var kind = row.Component switch
                {
                    1 => "Backing",
                    2 => "Pattern overlay",
                    3 => "Portrait frame",
                    4 => "Plate frame",
                    5 => "Accent",
                    _ => "Decoration",
                };

                rows.Add(new Row((uint)row.Image, row.Name.ToString(), kind, collection.PlateDecoration(decoration), PreviewId: (uint)row.Image, Part: PlatePart.Decoration, PartId: decoration));
            }

            if (rows.Count > 0)
                groups.Add(new Group("Plate", rows));
        }

        // --- gear
        if (capture is { Gear.Count: > 0 })
        {
            var rows = new List<Row>();
            foreach (var piece in capture.Gear.OrderBy(g => SlotOrder(g.Slot)))
            {
                if (!excel.TryGetRow<Item>(piece.ItemId, out var item))
                    continue;

                var kind = item.ItemUICategory.IsValid ? item.ItemUICategory.Value.Name.ToString() : "Gear";
                var dyes = new List<(uint, string)>();
                foreach (var stain in new[] { piece.Stain0, piece.Stain1 })
                {
                    if (stain != 0 && excel.TryGetRow<Stain>(stain, out var stainRow))
                        dyes.Add((stainRow.Color, stainRow.Name.ToString()));
                }

                var level = item.LevelItem.RowId;
                var extra = level > 1 ? $"Item level {level}" : null;
                rows.Add(new Row(item.Icon, item.Name.ToString(), kind, collection.Item(piece.ItemId) with { Link = Garland.Item(piece.ItemId) }, dyes, Extra: extra));
            }

            if (rows.Count > 0)
                groups.Add(new Group("Gear", rows));
        }

        return groups;
    }

    // head to feet, then accessories, then weapons
    private static int SlotOrder(byte slot) => slot switch
    {
        2 => 0, 3 => 1, 4 => 2, 6 => 3, 7 => 4,
        8 => 5, 9 => 6, 10 => 7, 11 => 8, 12 => 9,
        0 => 10, 1 => 11,
        _ => 20 + slot,
    };
}
