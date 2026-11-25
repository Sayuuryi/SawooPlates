using Dalamud.Interface.Windowing;
using SawooPlates.Capture;

namespace SawooPlates.Windows;

/// <summary>
/// Next to the game's Edit Plate window: what to pick in each of its rows for the design being used.
/// </summary>
public sealed unsafe class PlateGuideWindow : Window
{
    private readonly ITextureProvider _textures;
    private readonly PlateDesignApplier _designs;
    private readonly PlateItems _plateItems;

    private IDisposable? _style;
    private PlateDesignApplier.Guide? _rowsFor;
    private List<(string Row, PlateItems.PartInfo? Part)> _rows = [];

    // the rows of Edit Plate, top to bottom, and the kind names used for plate parts
    private static readonly (string Row, string Kind)[] Rows =
    [
        ("Base Plate", "Base plate"),
        ("Pattern Overlay", "Pattern overlay"),
        ("Backing", "Backing"),
        ("Top Border", "Top border"),
        ("Bottom Border", "Bottom border"),
        ("Portrait Frame", "Portrait frame"),
        ("Plate Frame", "Plate frame"),
        ("Accent", "Accent"),
    ];

    public PlateGuideWindow(ITextureProvider textures, PlateDesignApplier designs, PlateItems plateItems)
        : base("##SawooPlateGuide",
            ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav)
    {
        _textures = textures;
        _designs = designs;
        _plateItems = plateItems;

        IsOpen = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
    }

    public override bool DrawConditions()
    {
        if (_designs.Current == null)
            return false;

        var addon = PlateDesignApplier.DesignAddon;
        if (addon == null)
            return false;

        // to the right of Edit Plate
        var right = new Vector2(addon->X + addon->GetScaledWidth(true) + 6, addon->Y);
        Position = ImGui.GetMainViewport().Pos + right;
        PositionCondition = ImGuiCond.Always;
        return true;
    }

    public override void PreDraw() => _style = OverlayStyle.PushPanel();

    public override void PostDraw()
    {
        _style?.Dispose();
        _style = null;
        OverlayStyle.SendToBack(WindowName);
    }

    public override void Draw()
    {
        if (_designs.Current is not { } guide)
            return;

        if (!ReferenceEquals(_rowsFor, guide))
        {
            _rowsFor = guide;
            _rows = BuildRows(guide);
        }

        var scale = ImGuiHelpers.GlobalScale;
        ImGui.TextColored(Ui.Accent, "Pick these in Edit Plate");
        ImGui.TextColored(Ui.Dim, "Then press Save.");
        ImGui.Spacing();

        var icon = 28 * scale;
        var labelWidth = Rows.Max(r => ImGui.CalcTextSize(r.Row).X) + 12 * scale;

        foreach (var (row, part) in _rows)
        {
            var y = ImGui.GetCursorPosY();
            ImGui.SetCursorPosY(y + (icon - ImGui.GetTextLineHeight()) / 2f);
            ImGui.TextColored(Ui.Dim, row);

            ImGui.SameLine(labelWidth);
            ImGui.SetCursorPosY(y);
            if (part != null)
            {
                Ui.IconFit(_textures, part.IconId, icon);
                ImGui.SameLine();
            }
            else
            {
                ImGui.Dummy(new Vector2(icon, icon));
                ImGui.SameLine();
            }

            ImGui.SetCursorPosY(y + (icon - ImGui.GetTextLineHeight()) / 2f);
            if (part == null)
            {
                ImGui.TextColored(Ui.Dim, guide.Parts.Selective ? "Keep yours" : "None");
            }
            else if (part.Own.State == OwnState.Missing)
            {
                ImGui.TextColored(Ui.Bad, part.Name);
                ImGui.SameLine();
                ImGui.TextColored(Ui.Bad, "· you don't have this");
                if (ImGui.IsItemHovered() && part.Own.Hint is { Length: > 0 } hint)
                    ImGui.SetTooltip(hint);
            }
            else
            {
                ImGui.TextUnformatted(part.Name);
            }

            ImGui.SetCursorPosY(y + icon + 2 * scale);
        }

        ImGui.Spacing();
        ImGui.TextColored(Ui.Dim, "Invert Portrait Placement");
        ImGui.SameLine(labelWidth + icon + ImGui.GetStyle().ItemSpacing.X);
        if (guide.Parts.Position)
            ImGui.TextUnformatted(guide.Design.InvertPortraitPlacement ? "On" : "Off");
        else
            ImGui.TextColored(Ui.Dim, "Keep yours");
    }

    private List<(string Row, PlateItems.PartInfo? Part)> BuildRows(PlateDesignApplier.Guide guide)
    {
        var parts = _plateItems.GetParts(new CapturedPortrait { Plate = guide.Design }, new PortraitPresetData(), "Plate");
        return Rows.Select(r => (r.Row, parts.FirstOrDefault(p => p.Kind == r.Kind))).ToList();
    }
}
