using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SawooPlates.Capture;

/// <summary>
/// Puts a saved plate design into the game's "Edit Design" window for your own Adventurer Plate.
/// Only parts you have unlocked are used; the game's own Save button keeps the result, Cancel restores your old design.
/// </summary>
public sealed unsafe class PlateDesignApplier(IPluginLog log, IFramework framework, ExcelService excel, CollectionService collection, CaptureService captureService, WindowHandoff handoff)
{
    /// <summary>
    /// Which parts to write. Base plate and borders are written when their id isn't 0.
    /// <paramref name="MergeDecorations"/>: each picked decoration replaces yours of the same kind (backing, frame...)
    /// and your others stay; otherwise your decorations are replaced by the design's.
    /// </summary>
    public readonly record struct Parts(bool Decorations = true, bool Position = true, bool MergeDecorations = false)
    {
        public static Parts All => new();
    }

    // the game calls the window CharaCardDesignSetting; its layout file is CharacterCardDesignSetting
    private static readonly string[] DesignAddons = ["CharaCardDesignSetting", "CharacterCardDesignSetting"];

    /// <summary>True while the design editor of your own plate is open.</summary>
    public bool CanApply
    {
        get
        {
            var agent = AgentCharaCard.Instance();
            if (agent == null || agent->Data == null)
                return false;

            // the design editor only opens for your own plate
            foreach (var name in DesignAddons)
            {
                var addon = AddonUtil.GetAddon<AtkUnitBase>(name);
                if (addon != null && addon->IsVisible)
                    return true;
            }

            return false;
        }
    }

    /// <summary>Passed to the callback of <see cref="ApplyOrOpen"/> when the plate was closed before the design editor opened.</summary>
    public const int Cancelled = -2;

    /// <summary>True when the design editor is open, or your own plate can be opened to get to it.</summary>
    public bool CanApplyOrOpen
    {
        get
        {
            if (CanApply)
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

    /// <summary>
    /// Applies the design. When the design editor is closed, opens your own Adventurer Plate and applies
    /// as soon as you open Edit Plate Design there. <paramref name="done"/> gets the number of skipped parts,
    /// -1 when the editor didn't open in time, or <see cref="Cancelled"/> when the plate was closed first.
    /// </summary>
    public void ApplyOrOpen(PlateDesignData design, Parts parts, Action<int> done)
    {
        if (CanApply)
        {
            done(Apply(design, parts));
            return;
        }

        framework.RunOnFrameworkThread(() =>
        {
            var control = Control.Instance();
            var agent = AgentCharaCard.Instance();
            if (control == null || control->LocalPlayer == null || agent == null)
            {
                done(-1);
                return;
            }

            if (!OwnPlateOpen)
                agent->OpenCharaCard((GameObject*)control->LocalPlayer);

            handoff.StepAside(() => CanApply || OwnPlateOpen);
            WaitForDesignEditor(design, parts, done, new Route(DateTime.UtcNow));
        });
    }

    /// <summary>Progress of the way from your plate to Edit Plate Design.</summary>
    private sealed class Route(DateTime started)
    {
        public DateTime Started { get; } = started;
        public bool PlateSeen;
        public bool ClickedEdit;
        public bool ClickedDesign;
        public DateTime NextTry;
        public bool Logged;
        public bool Told;
    }

    // "Edit" on your plate opens a small menu; "Edit Plate Design" in it opens the design editor
    private const uint EditPlateDesignText = 15002;
    private static readonly uint[] EditTexts = [14733, 442, 2220, 4133, 4537, 4541, 4554, 4565, 11478, 12842, 14053, 16534];

    private string AddonText(uint id) => excel.TryGetRow<Addon>(id, out var row) ? row.Text.ExtractText().Trim() : string.Empty;

    private void WaitForDesignEditor(PlateDesignData design, Parts parts, Action<int> done, Route route)
    {
        if (CanApply)
        {
            // give the window a moment to fill in your current design before changing it
            _ = framework.RunOnTick(() => done(CanApply ? Apply(design, parts) : -1), TimeSpan.FromMilliseconds(500));
            return;
        }

        var plateOpen = OwnPlateOpen;
        var menu = AddonUtil.GetAddon<AtkUnitBase>("CharaCardEditMenu");
        var menuOpen = menu != null && menu->IsVisible;

        if (route.PlateSeen && !plateOpen && !menuOpen)
        {
            done(Cancelled); // the plate was closed without opening the design editor
            return;
        }

        if (DateTime.UtcNow - route.Started > TimeSpan.FromSeconds(90))
        {
            done(-1);
            return;
        }

        route.PlateSeen |= plateOpen;

        if ((plateOpen || menuOpen) && DateTime.UtcNow >= route.NextTry)
        {
            route.NextTry = DateTime.UtcNow + TimeSpan.FromMilliseconds(600);
            TakeNextStep(route, menuOpen ? menu : null);
        }

        _ = framework.RunOnTick(() => WaitForDesignEditor(design, parts, done, route), TimeSpan.FromMilliseconds(200));
    }

    /// <summary>Clicks Edit on your plate, then Edit Plate Design in the menu that opens.</summary>
    private void TakeNextStep(Route route, AtkUnitBase* menu)
    {
        var designText = AddonText(EditPlateDesignText);
        bool IsDesign(string text) => designText.Length > 0 && string.Equals(text, designText, StringComparison.OrdinalIgnoreCase);

        if (route.ClickedDesign)
            return; // waiting for the editor to show

        var card = AddonUtil.GetAddon<AtkUnitBase>("CharaCard");

        // the design entry may be right on the plate or in the menu
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

        // couldn't find the way: say what's there, and let the user do the last click
        if (!route.Logged)
        {
            route.Logged = true;
            log.Information("Edit Plate Design not found. Plate: {plate}. Menu: {menu}",
                string.Join("; ", GameClick.Describe(card)), menu != null ? string.Join("; ", GameClick.Describe(menu)) : "closed");
        }

        if (!route.Told)
        {
            route.Told = true;
            captureService.Notify("Your plate is open. Open Edit Plate Design and the design goes in by itself.",
                Dalamud.Interface.ImGuiNotification.NotificationType.Info);
        }
    }

    /// <summary>Writes the design into the editor. Returns how many parts were left out because they aren't unlocked.</summary>
    public int Apply(PlateDesignData design, Parts parts)
    {
        if (!CanApply)
            return -1;

        var data = AgentCharaCard.Instance()->Data;
        ref var plate = ref data->PlateDesign;
        var skipped = 0;

        if (design.BasePlate != 0)
        {
            if (collection.PlateBase(design.BasePlate).State == OwnState.Have)
                plate.BasePlate = design.BasePlate;
            else
                skipped++;
        }

        if (design.TopBorder != 0)
        {
            if (collection.PlateHeader(design.TopBorder).State == OwnState.Have)
                plate.TopBorder = design.TopBorder;
            else
                skipped++;
        }

        if (design.BottomBorder != 0)
        {
            if (collection.PlateHeader(design.BottomBorder).State == OwnState.Have)
                plate.BottomBorder = design.BottomBorder;
            else
                skipped++;
        }

        if (parts.Decorations)
        {
            var incoming = new List<ushort>();
            foreach (var id in design.Decorations.Where(d => d != 0))
            {
                if (collection.PlateDecoration(id).State == OwnState.Have)
                    incoming.Add(id);
                else
                    skipped++;
            }

            var slots = plate.Decorations;
            var result = new List<ushort>();
            if (parts.MergeDecorations)
            {
                // keep yours, except the kinds being replaced
                var replaced = incoming.Select(Component).ToHashSet();
                for (var i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != 0 && !replaced.Contains(Component(slots[i])))
                        result.Add(slots[i]);
                }
            }

            result.AddRange(incoming);
            for (var i = 0; i < slots.Length; i++)
                slots[i] = i < result.Count ? result[i] : (ushort)0;
            plate.NumDecorations = (byte)Math.Min(result.Count, slots.Length);
        }

        if (parts.Position)
            data->InvertPortraitPlacement = design.InvertPortraitPlacement;

        log.Debug("Applied plate design ({skipped} parts skipped)", skipped);
        return skipped;
    }

    /// <summary>Which slot kind a decoration fills (backing, pattern, portrait frame, plate frame, accent).</summary>
    private byte Component(ushort decoration)
        => excel.TryGetRow<CharaCardDecoration>(decoration, out var row) ? row.Component : (byte)0;
}
