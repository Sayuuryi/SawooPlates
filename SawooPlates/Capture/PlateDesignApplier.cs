using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SawooPlates.Capture;

/// <summary>
/// Puts a saved plate design into the game's "Edit Design" window for your own Adventurer Plate.
/// Only parts you have unlocked are used; the game's own Save button keeps the result, Cancel restores your old design.
/// </summary>
public sealed unsafe class PlateDesignApplier(IPluginLog log, CollectionService collection)
{
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

    /// <summary>Writes the design into the editor. Returns how many parts were left out because they aren't unlocked.</summary>
    public int Apply(PlateDesignData design)
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

        var decorations = new List<ushort>();
        foreach (var id in design.Decorations.Where(d => d != 0))
        {
            if (collection.PlateDecoration(id).State == OwnState.Have)
                decorations.Add(id);
            else
                skipped++;
        }

        var slots = plate.Decorations;
        for (var i = 0; i < slots.Length; i++)
            slots[i] = i < decorations.Count ? decorations[i] : (ushort)0;
        plate.NumDecorations = (byte)Math.Min(decorations.Count, slots.Length);

        data->InvertPortraitPlacement = design.InvertPortraitPlacement;

        log.Debug("Applied plate design ({skipped} parts skipped)", skipped);
        return skipped;
    }
}
