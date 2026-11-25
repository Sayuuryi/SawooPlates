using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SawooPlates.Capture;

/// <summary>
/// Takes you to the game's Edit Plate window for your own Adventurer Plate and keeps the design you want to use
/// as <see cref="Current"/>, so the guide next to that window can show what to pick in each row.
/// The game keeps the window's choices in its own data, so the picks are made in the game's menus.
/// When this opened your plate, closing Edit Plate also closes the plate and brings the Sawoo Plates window back.
/// A row only counts as picked once the window itself got the pick: changing what a drop-down shows isn't enough,
/// the window has to be told, or Save stays off and the plate doesn't change.
/// </summary>
public sealed unsafe class PlateDesignApplier : IDisposable
{
    private readonly IPluginLog log;
    private readonly IFramework framework;
    private readonly ExcelService excel;
    private readonly CaptureService captureService;
    private readonly WindowHandoff handoff;
    private readonly IAddonLifecycle addonLifecycle;

    /// <summary>Event params Edit Plate received since the last reset, to know a pick reached it.</summary>
    private readonly HashSet<int> _windowGot = [];

    public PlateDesignApplier(IPluginLog log, IFramework framework, ExcelService excel, CaptureService captureService, WindowHandoff handoff, IAddonLifecycle addonLifecycle)
    {
        this.log = log;
        this.framework = framework;
        this.excel = excel;
        this.captureService = captureService;
        this.handoff = handoff;
        this.addonLifecycle = addonLifecycle;
        addonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, DesignAddons, OnDesignEvent);
    }

    public void Dispose() => addonLifecycle.UnregisterListener(AddonEvent.PreReceiveEvent, DesignAddons, OnDesignEvent);

    private void OnDesignEvent(AddonEvent type, AddonArgs args)
    {
        if (args is not AddonReceiveEventArgs e)
            return;

        var eventType = (AtkEventType)e.AtkEventType;
        if (eventType is not (AtkEventType.ListItemClick or AtkEventType.ListItemSelect or AtkEventType.ValueUpdate or AtkEventType.ButtonClick))
            return;

        _windowGot.Add(e.EventParam);

        // while Use plate design is going on, note what Edit Plate gets, also from picks made by hand
        if (_route != null)
        {
            var index = e.AtkEventData != 0 ? ((AtkEventData*)e.AtkEventData)->ListItemData.SelectedIndex : -1;
            log.Debug("Edit Plate got {type} param {param} index {index}", eventType, e.EventParam, index);
        }
    }

    /// <summary>Which parts to show. <paramref name="Selective"/>: parts left out stay as they are on your plate.</summary>
    public readonly record struct Parts(bool Position = true, bool Selective = false)
    {
        public static Parts All => new();
    }

    /// <summary>The design being put on your plate, with the parts to use and how picking each row went.</summary>
    public sealed record Guide(PlateDesignData Design, Parts Parts)
    {
        /// <summary>Row name (as in Edit Plate) to result. Rows not in here weren't tried yet.</summary>
        public Dictionary<string, RowResult> Results { get; } = [];

        /// <summary>True once every row was tried.</summary>
        public bool Finished { get; set; }
    }

    public enum RowResult { Picked, NotInList, Failed, Kept }

    /// <summary>One row of Edit Plate: its name, the node id of its drop-down, and what it holds.</summary>
    private sealed record Row(string Name, uint DropDownId, RowKind Kind, byte Component = 0);

    private enum RowKind { Base, TopBorder, BottomBorder, Decoration }

    // node ids from the Edit Plate layout (CharaCardDesignSetting)
    private static readonly Row[] Rows =
    [
        new("Base Plate", 30, RowKind.Base),
        new("Pattern Overlay", 36, RowKind.Decoration, 2),
        new("Backing", 42, RowKind.Decoration, 1),
        new("Top Border", 48, RowKind.TopBorder),
        new("Bottom Border", 54, RowKind.BottomBorder),
        new("Portrait Frame", 60, RowKind.Decoration, 3),
        new("Plate Frame", 66, RowKind.Decoration, 4),
        new("Accent", 72, RowKind.Decoration, 5),
    ];

    private const uint InvertCheckBoxId = 78;
    public const string InvertRow = "Invert Portrait Placement";

    // the game calls the window CharaCardDesignSetting; its layout file is CharacterCardDesignSetting
    private static readonly string[] DesignAddons = ["CharaCardDesignSetting", "CharacterCardDesignSetting"];

    // "Edit" on your plate opens a small menu; "Edit Plate Design" in it opens Edit Plate
    private const uint EditPlateDesignText = 15002;
    private static readonly uint[] EditTexts = [14733, 442, 2220, 4133, 4537, 4541, 4554, 4565, 11478, 12842, 14053, 16534];

    private Route? _route;
    private bool _describedEditor;

    /// <summary>The design shown in the guide while Edit Plate is open; null when nothing is going on.</summary>
    public Guide? Current { get; private set; }

    /// <summary>The Edit Plate window, or null when it isn't open.</summary>
    public static AtkUnitBase* DesignAddon
    {
        get
        {
            var agent = AgentCharaCard.Instance();
            if (agent == null || agent->Data == null)
                return null;

            foreach (var name in DesignAddons)
            {
                var addon = AddonUtil.GetAddon<AtkUnitBase>(name);
                if (addon != null && addon->IsVisible)
                    return addon;
            }

            return null;
        }
    }

    /// <summary>True while Edit Plate of your own plate is open.</summary>
    public bool EditorOpen => DesignAddon != null;

    /// <summary>True when Edit Plate is open, or your own plate can be opened to get to it.</summary>
    public bool CanUse
    {
        get
        {
            if (EditorOpen)
                return true;

            var control = Control.Instance();
            return control != null && control->LocalPlayer != null;
        }
    }

    private static bool OwnPlateOpen
    {
        get
        {
            var agent = AgentCharaCard.Instance();
            var addon = AddonUtil.GetAddon<AtkUnitBase>("CharaCard");
            return agent != null && agent->Data != null && agent->Data->CanEdit && addon != null && addon->IsVisible;
        }
    }

    /// <summary>Progress of one use: from your plate to Edit Plate, and back when it closes.</summary>
    private sealed class Route(DateTime started, bool openedByUs)
    {
        public DateTime Started { get; } = started;
        public bool OpenedByUs { get; } = openedByUs;
        public bool PlateSeen;
        public bool EditorSeen;
        public bool ClickedEdit;
        public bool ClickedDesign;
        public DateTime NextTry;
        public bool Logged;
        public bool Told;
        public bool Picking;
    }

    /// <summary>
    /// Shows <paramref name="design"/> in the guide. When Edit Plate is closed, opens your plate and goes to it.
    /// </summary>
    public void Use(PlateDesignData design, Parts parts)
    {
        framework.RunOnFrameworkThread(() =>
        {
            Current = new Guide(design, parts);

            if (_route != null)
                return; // already on the way; the new design replaces the old one

            if (EditorOpen)
            {
                _route = new Route(DateTime.UtcNow, openedByUs: false) { EditorSeen = true };
                Watch(_route);
                return;
            }

            var control = Control.Instance();
            var agent = AgentCharaCard.Instance();
            if (control == null || control->LocalPlayer == null || agent == null)
            {
                Current = null;
                captureService.Notify("Your Adventurer Plate can't be opened right now.", Dalamud.Interface.ImGuiNotification.NotificationType.Warning);
                return;
            }

            var openedByUs = !OwnPlateOpen;
            if (openedByUs)
                agent->OpenCharaCard((GameObject*)control->LocalPlayer);

            _route = new Route(DateTime.UtcNow, openedByUs);
            var route = _route;
            handoff.StepAside(() => _route == route);
            Watch(route);
        });
    }

    private void End(Route route)
    {
        if (_route != route)
            return;

        _route = null;
        Current = null;
    }

    private void Watch(Route route)
    {
        if (_route != route)
            return;

        var editorOpen = EditorOpen;

        if (editorOpen)
        {
            route.EditorSeen = true;
            DescribeEditorOnce();

            // pick every row by itself, once the window has filled in your current design
            if (!route.Picking && Current is { Finished: false } guide)
            {
                route.Picking = true;
                _ = framework.RunOnTick(() => PickRow(route, guide, 0, 0), TimeSpan.FromMilliseconds(500));
            }
        }
        else if (route.EditorSeen)
        {
            // Edit Plate was closed: close the plate this opened, and the Sawoo Plates window comes back
            if (route.OpenedByUs && OwnPlateOpen)
            {
                var card = AddonUtil.GetAddon<AtkUnitBase>("CharaCard");
                if (card != null)
                    card->Close(true);
            }

            End(route);
            return;
        }
        else
        {
            var plateOpen = OwnPlateOpen;
            var menu = AddonUtil.GetAddon<AtkUnitBase>("CharaCardEditMenu");
            var menuOpen = menu != null && menu->IsVisible;

            if (route.PlateSeen && !plateOpen && !menuOpen)
            {
                End(route); // the plate was closed before Edit Plate opened
                return;
            }

            if (DateTime.UtcNow - route.Started > TimeSpan.FromSeconds(90))
            {
                End(route);
                return;
            }

            route.PlateSeen |= plateOpen;

            if ((plateOpen || menuOpen) && DateTime.UtcNow >= route.NextTry)
            {
                route.NextTry = DateTime.UtcNow + TimeSpan.FromMilliseconds(600);
                TakeNextStep(route, menuOpen ? menu : null);
            }
        }

        _ = framework.RunOnTick(() => Watch(route), TimeSpan.FromMilliseconds(200));
    }

    private string AddonText(uint id) => excel.TryGetRow<Addon>(id, out var row) ? row.Text.ExtractText().Trim() : string.Empty;

    /// <summary>Clicks Edit on your plate, then Edit Plate Design in the menu that opens.</summary>
    private void TakeNextStep(Route route, AtkUnitBase* menu)
    {
        if (route.ClickedDesign)
            return; // waiting for Edit Plate to show

        var designText = AddonText(EditPlateDesignText);
        bool IsDesign(string text) => designText.Length > 0 && string.Equals(text, designText, StringComparison.OrdinalIgnoreCase);

        var card = AddonUtil.GetAddon<AtkUnitBase>("CharaCard");

        if ((menu != null && GameClick.Click(menu, IsDesign)) || GameClick.Click(card, IsDesign))
        {
            route.ClickedDesign = true;
            return;
        }

        if (menu == null && !route.ClickedEdit)
        {
            var editTexts = EditTexts.Select(AddonText).Where(t => t.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (GameClick.Click(card, editTexts.Contains))
            {
                route.ClickedEdit = true;
                return;
            }
        }

        if (!route.Logged)
        {
            route.Logged = true;
            log.Information("Edit Plate Design not found. Plate: {plate}. Menu: {menu}",
                string.Join("; ", GameClick.Describe(card)), menu != null ? string.Join("; ", GameClick.Describe(menu)) : "closed");
        }

        if (!route.Told)
        {
            route.Told = true;
            captureService.Notify("Your plate is open. Open Edit Plate Design and the design is picked there.",
                Dalamud.Interface.ImGuiNotification.NotificationType.Info);
        }
    }

    /// <summary>The entry name to pick in a row, "None" to clear it, or null to leave the row as it is.</summary>
    private string? TargetName(Row row, Guide guide)
    {
        var design = guide.Design;
        var selective = guide.Parts.Selective;
        switch (row.Kind)
        {
            case RowKind.Base:
                return design.BasePlate != 0 && excel.TryGetRow<CharaCardBase>(design.BasePlate, out var basePlate) ? basePlate.Name.ToString() : null;
            case RowKind.TopBorder:
                if (design.TopBorder == 0)
                    return selective ? null : "None";
                return excel.TryGetRow<CharaCardHeader>(design.TopBorder, out var top) ? top.Name.ToString() : null;
            case RowKind.BottomBorder:
                if (design.BottomBorder == 0)
                    return selective ? null : "None";
                return excel.TryGetRow<CharaCardHeader>(design.BottomBorder, out var bottom) ? bottom.Name.ToString() : null;
            default:
                foreach (var id in design.Decorations)
                {
                    if (id != 0 && excel.TryGetRow<CharaCardDecoration>(id, out var decoration) && decoration.Component == row.Component)
                        return decoration.Name.ToString();
                }

                return selective ? null : "None";
        }
    }

    /// <summary>
    /// Picks one row the way a mouse does: opens the drop-down, clicks the entry, and checks Edit Plate got the pick.
    /// When it didn't, the pick is handed to Edit Plate directly. Then moves on to the next row.
    /// </summary>
    private void PickRow(Route route, Guide guide, int rowIndex, int attempt)
    {
        if (_route != route || !ReferenceEquals(Current, guide))
            return;

        var addon = DesignAddon;
        if (addon == null)
            return;

        if (rowIndex >= Rows.Length)
        {
            PickInvert(route, guide, addon, 0);
            return;
        }

        var row = Rows[rowIndex];
        var target = TargetName(row, guide);
        var dropDown = GameControls.DropDown(addon, row.DropDownId);

        void Next(RowResult result)
        {
            guide.Results[row.Name] = result;
            if (dropDown != null && dropDown->IsOpen)
                GameControls.Toggle(dropDown);
            _ = framework.RunOnTick(() => PickRow(route, guide, rowIndex + 1, 0), TimeSpan.FromMilliseconds(200));
        }

        void Retry(int delay) => _ = framework.RunOnTick(() => PickRow(route, guide, rowIndex, attempt + 1), TimeSpan.FromMilliseconds(delay));

        if (target == null)
        {
            Next(RowResult.Kept);
            return;
        }

        if (dropDown == null || dropDown->List == null)
        {
            log.Information("Edit Plate: no drop-down {id} for {row}", row.DropDownId, row.Name);
            Next(RowResult.Failed);
            return;
        }

        var windowParams = GameControls.WindowParams(addon, dropDown);
        var told = _windowGot.Overlaps(windowParams);
        var shows = string.Equals(GameControls.Shown(dropDown), target, StringComparison.OrdinalIgnoreCase);
        var index = GameControls.IndexOf(dropDown, target);

        if (attempt == 0 && shows)
        {
            Next(RowResult.Picked); // already yours, nothing to change
            return;
        }

        if (index < 0)
        {
            Next(RowResult.NotInList); // not unlocked, or hidden by the list filter
            return;
        }

        switch (attempt)
        {
            case 0:
                // open the list, like clicking the drop-down
                if (!dropDown->IsOpen)
                    GameControls.Toggle(dropDown);
                Retry(250);
                return;

            case 1:
                _windowGot.Clear();
                GameControls.ClickEntry(dropDown, index);
                Retry(300);
                return;

            case 2:
                if (shows && told)
                {
                    Next(RowResult.Picked);
                    return;
                }

                // the click didn't reach Edit Plate: hand it the pick directly
                log.Information("Edit Plate: clicking '{target}' in {row} didn't reach the window (shows '{shown}', told {told}), telling it directly",
                    target, row.Name, GameControls.Shown(dropDown), told);
                _windowGot.Clear();
                GameControls.TellWindow(addon, dropDown, index);
                Retry(300);
                return;

            default:
                if (shows && told)
                {
                    Next(RowResult.Picked);
                    return;
                }

                log.Information("Edit Plate: {row} shows '{shown}', window got it: {told}, after picking '{target}'. Window params: {events}",
                    row.Name, GameControls.Shown(dropDown), told, target, string.Join(",", windowParams));
                Next(RowResult.Failed);
                return;
        }
    }

    private void PickInvert(Route route, Guide guide, AtkUnitBase* addon, int attempt)
    {
        void Finish(RowResult result)
        {
            guide.Results[InvertRow] = result;
            guide.Finished = true;
            log.Information("Edit Plate picks: {results}. Buttons: {buttons}",
                string.Join(", ", guide.Results.Select(r => $"{r.Key}={r.Value}")),
                string.Join("; ", GameClick.Describe(addon).Where(t => t.StartsWith("button"))));
        }

        if (!guide.Parts.Position)
        {
            Finish(RowResult.Kept);
            return;
        }

        var box = GameControls.CheckBox(addon, InvertCheckBoxId);
        if (box == null)
        {
            Finish(RowResult.Failed);
            return;
        }

        if (box->IsChecked == guide.Design.InvertPortraitPlacement)
        {
            Finish(RowResult.Picked);
            return;
        }

        if (attempt > 0)
        {
            Finish(RowResult.Failed);
            return;
        }

        // a click flips the box first and then tells Edit Plate
        box->AtkComponentButton.SetChecked(guide.Design.InvertPortraitPlacement);
        if (!GameControls.Toggle(addon, box))
        {
            Finish(RowResult.Failed);
            return;
        }

        _ = framework.RunOnTick(() =>
        {
            var again = DesignAddon;
            if (_route == route && ReferenceEquals(Current, guide) && again != null)
                PickInvert(route, guide, again, attempt + 1);
        }, TimeSpan.FromMilliseconds(250));
    }

    /// <summary>Once per session: what Edit Plate is made of, so picking rows by itself can be added later.</summary>
    private void DescribeEditorOnce()
    {
        if (_describedEditor)
            return;

        _describedEditor = true;
        log.Information("Edit Plate layout: {layout}", string.Join("; ", GameClick.Describe(DesignAddon, withText: true)));
    }
}
