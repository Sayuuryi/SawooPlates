using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GameAgentId = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentId;

namespace SawooPlates.Capture;

/// <summary>
/// Puts a saved plate design into the game's "Edit Design" window for your own Adventurer Plate.
/// Only parts you have unlocked are used; the game's own Save button keeps the result, Cancel restores your old design.
/// </summary>
public sealed unsafe class PlateDesignApplier(IPluginLog log, IFramework framework, CollectionService collection, WindowHandoff handoff)
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
    /// Applies the design, first opening your own Adventurer Plate and its design editor when they're closed.
    /// <paramref name="done"/> gets the number of skipped parts, or -1 when the editor couldn't be opened.
    /// </summary>
    public void ApplyOrOpen(PlateDesignData design, Action<int> done)
    {
        if (CanApply)
        {
            done(Apply(design));
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
            OpenDesignWhenReady(design, done, DateTime.UtcNow.AddSeconds(10), designShown: false);
        });
    }

    private void OpenDesignWhenReady(PlateDesignData design, Action<int> done, DateTime giveUpAt, bool designShown)
    {
        if (CanApply)
        {
            // give the window a moment to fill in your current design before changing it
            _ = framework.RunOnTick(() => done(CanApply ? Apply(design) : -1), TimeSpan.FromMilliseconds(500));
            return;
        }

        if (DateTime.UtcNow > giveUpAt)
        {
            done(-1);
            return;
        }

        if (!designShown && OwnPlateOpen)
        {
            var designAgent = AgentModule.Instance()->GetAgentByInternalId(GameAgentId.CharaCardDesignSetting);
            if (designAgent != null && !designAgent->IsAgentActive())
                designAgent->Show();
            designShown = true;
        }

        _ = framework.RunOnTick(() => OpenDesignWhenReady(design, done, giveUpAt, designShown), TimeSpan.FromMilliseconds(200));
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
