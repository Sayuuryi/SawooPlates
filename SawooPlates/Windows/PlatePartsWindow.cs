using Dalamud.Interface.Windowing;
using SawooPlates.Capture;

namespace SawooPlates.Windows;

/// <summary>
/// Ctrl+click on "Use plate design": pick which parts of the design to take.
/// The preview shows only the picked parts; parts you don't pick stay as they are on your plate.
/// </summary>
public sealed class PlatePartsWindow : Window
{
    private readonly ITextureProvider _textures;

    private PlateDesignData? _design;
    private List<PlateItems.PartInfo> _parts = [];
    private readonly HashSet<int> _chosen = [];
    private bool _position;
    private Action<PlateDesignData, float>? _preview;
    private Action<PlateDesignData, PlateDesignApplier.Parts>? _apply;
    private string _title = string.Empty;

    public PlatePartsWindow(ITextureProvider textures)
        : base("Choose plate parts##SawooPlateParts", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings)
    {
        _textures = textures;
        RespectCloseHotkey = true;
    }

    /// <summary>Opens the window for one design. <paramref name="apply"/> runs when the user presses Apply.</summary>
    public void Show(string title, PlateDesignData design, List<PlateItems.PartInfo> parts,
        Action<PlateDesignData, float>? preview, Action<PlateDesignData, PlateDesignApplier.Parts> apply)
    {
        _title = title;
        _design = design;
        _parts = parts;
        _preview = preview;
        _apply = apply;
        _position = true;

        _chosen.Clear();
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i].Own.State == OwnState.Have)
                _chosen.Add(i);
        }

        IsOpen = true;
        BringToFront();
    }

    public override void PreDraw()
    {
        // centred on the screen when it opens
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f));
    }

    public override void OnClose()
    {
        _design = null;
        _apply = null;
        _preview = null;
    }

    public override void Draw()
    {
        if (_design is not { } design)
        {
            IsOpen = false;
            return;
        }

        var scale = Ui.Scale;
        var picked = Picked(design);

        if (_title.Length > 0)
            ImGui.TextColored(Ui.Accent, _title);
        ImGui.TextColored(Ui.Dim, "Parts you don't pick stay as they are on your plate.");
        ImGui.Spacing();

        // what you'll get: only the picked parts
        if (_preview != null)
        {
            using (ImRaii.Group())
                _preview(picked, 360 * scale);
            ImGui.SameLine(0, 16 * scale);
        }

        using (ImRaii.Group())
        {
            DrawList();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var button = new Vector2(110 * scale, 0);
        if (ImGui.Button("All I have", button))
        {
            for (var i = 0; i < _parts.Count; i++)
            {
                if (_parts[i].Own.State == OwnState.Have)
                    _chosen.Add(i);
            }

            _position = true;
        }

        ImGui.SameLine();
        if (ImGui.Button("None", button))
        {
            _chosen.Clear();
            _position = false;
        }

        ImGui.SameLine(0, 24 * scale);
        var nothing = _chosen.Count == 0 && !_position;
        using (ImRaii.Disabled(nothing))
        {
            if (ImGui.Button("Use", button) && !nothing && _apply is { } apply)
            {
                var parts = new PlateDesignApplier.Parts(Position: _position, Selective: true);
                IsOpen = false;
                apply(picked, parts);
            }
        }

        if (nothing && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Pick at least one part.");

        ImGui.SameLine();
        if (ImGui.Button("Cancel", button))
            IsOpen = false;
    }

    private void DrawList()
    {
        var icon = 36 * Ui.Scale;
        var kindWidth = _parts.Select(p => ImGui.CalcTextSize(p.Kind).X).DefaultIfEmpty(0).Max();

        for (var i = 0; i < _parts.Count; i++)
        {
            var part = _parts[i];
            var have = part.Own.State == OwnState.Have;
            var on = _chosen.Contains(i);

            using var id = ImRaii.PushId(i);
            var rowY = ImGui.GetCursorPosY();

            using (ImRaii.Disabled(!have))
            {
                ImGui.SetCursorPosY(rowY + (icon - ImGui.GetFrameHeight()) / 2f);
                if (ImGui.Checkbox("##Pick", ref on))
                {
                    if (on) _chosen.Add(i);
                    else _chosen.Remove(i);
                }
            }

            ImGui.SameLine();
            ImGui.SetCursorPosY(rowY);
            Ui.IconFit(_textures, part.IconId, icon);

            ImGui.SameLine();
            using (ImRaii.Group())
            {
                ImGui.SetCursorPosY(rowY + (icon - ImGui.GetTextLineHeight() * 2) / 2f);
                using (ImRaii.PushColor(ImGuiCol.Text, have ? (on ? Ui.Accent : ImGui.GetStyle().Colors[(int)ImGuiCol.Text]) : Ui.Dim))
                    ImGui.TextUnformatted(part.Name);
                ImGui.TextColored(have ? Ui.Dim : Ui.Bad, have ? part.Kind : $"{part.Kind} · you don't have this");
            }

            if (!have && ImGui.IsItemHovered() && part.Own.Hint is { Length: > 0 } hint)
                ImGui.SetTooltip(hint);

            ImGui.SetCursorPosY(rowY + icon + 4 * Ui.Scale);
            ImGui.Dummy(new Vector2(Math.Max(260 * Ui.Scale, kindWidth), 0));
        }

        // where the portrait sits is part of the design too
        ImGui.Spacing();
        var side = _design!.InvertPortraitPlacement ? "left" : "right";
        ImGui.Checkbox($"Portrait on the {side}", ref _position);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Where the portrait sits on the plate.");
    }

    /// <summary>The design with only the picked parts; 0 means "keep yours".</summary>
    private PlateDesignData Picked(PlateDesignData design)
    {
        var picked = _chosen.Select(i => _parts[i]).ToList();
        ushort Pick(PlateItems.PlatePart part) => picked.FirstOrDefault(p => p.Part == part)?.PartId ?? 0;

        return new PlateDesignData
        {
            BasePlate = Pick(PlateItems.PlatePart.Base),
            TopBorder = (byte)Pick(PlateItems.PlatePart.TopBorder),
            BottomBorder = (byte)Pick(PlateItems.PlatePart.BottomBorder),
            Decorations = picked.Where(p => p.Part == PlateItems.PlatePart.Decoration).Select(p => p.PartId).ToArray(),
            InvertPortraitPlacement = _position ? design.InvertPortraitPlacement : false,
        };
    }
}
