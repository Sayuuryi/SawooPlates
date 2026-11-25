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
/// </summary>
public sealed unsafe class PlateDesignApplier(IPluginLog log, IFramework framework, ExcelService excel, CaptureService captureService, WindowHandoff handoff)
{
    /// <summary>Which parts to show. <paramref name="Selective"/>: parts left out stay as they are on your plate.</summary>
    public readonly record struct Parts(bool Position = true, bool Selective = false)
    {
        public static Parts All => new();
    }

    /// <summary>The design being put on your plate, with the parts to use.</summary>
    public sealed record Guide(PlateDesignData Design, Parts Parts);

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
            captureService.Notify("Your plate is open. Open Edit Plate Design to see what to pick.",
                Dalamud.Interface.ImGuiNotification.NotificationType.Info);
        }
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
