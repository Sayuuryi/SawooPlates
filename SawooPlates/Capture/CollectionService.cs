using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using SawooPlates.PortraitEditor.Services;

namespace SawooPlates.Capture;

public enum OwnState
{
    Have,
    Missing,
    Unknown,
}

/// <summary>Whether the player has something, where it is, and how to get it when missing.</summary>
public readonly record struct Ownership(OwnState State, string? Where = null, string? Hint = null, string? Link = null)
{
    public static Ownership Unknown(string? why = null) => new(OwnState.Unknown, Hint: why);
}

/// <summary>Checks portrait and plate unlocks and whether the player owns gear shown in a portrait.</summary>
public sealed unsafe class CollectionService(ExcelService excelService, BannerService bannerService, GearCollectionStore savedGear)
{
    private Dictionary<uint, uint>? _cabinetByItem;

    // --- portrait and plate unlocks (all use BannerCondition)

    public Ownership BannerBg(uint id)
        => id == 0 ? Ownership.Unknown() : Condition(excelService.TryGetRow<BannerBg>(id, out var r) ? r.UnlockCondition.RowId : null);

    public Ownership BannerFrame(uint id)
        => id == 0 ? Ownership.Unknown() : Condition(excelService.TryGetRow<BannerFrame>(id, out var r) ? r.UnlockCondition.RowId : null);

    public Ownership BannerDecoration(uint id)
        => id == 0 ? Ownership.Unknown() : Condition(excelService.TryGetRow<BannerDecoration>(id, out var r) ? r.UnlockCondition.RowId : null);

    public Ownership BannerTimeline(uint id)
        => Condition(excelService.TryGetRow<BannerTimeline>(id, out var r) ? r.UnlockCondition.RowId : null);

    public Ownership Expression(uint id)
        => id == 0 ? new Ownership(OwnState.Have) : Condition(excelService.TryGetRow<BannerFacial>(id, out var r) ? r.UnlockCondition.RowId : null);

    public Ownership PlateBase(uint id)
        => Condition(excelService.TryGetRow<CharaCardBase>(id, out var r) ? r.UnlockCondition.RowId : null);

    public Ownership PlateHeader(uint id)
        => Condition(excelService.TryGetRow<CharaCardHeader>(id, out var r) ? r.UnlockCondition.RowId : null);

    public Ownership PlateDecoration(uint id)
        => Condition(excelService.TryGetRow<CharaCardDecoration>(id, out var r) ? r.UnlockCondition.RowId : null);

    private Ownership Condition(uint? conditionId)
    {
        if (conditionId == null)
            return Ownership.Unknown();

        var (source, link) = GetUnlockSource(conditionId.Value);
        return bannerService.TryGetBannerConditionUnlocked(conditionId.Value) switch
        {
            true => new Ownership(OwnState.Have, Link: link),
            false => new Ownership(OwnState.Missing, Hint: source ?? GetUnlockHint(conditionId.Value), Link: link),
            null => Ownership.Unknown("Still loading. Hover again in a moment.") with { Link = link },
        };
    }

    private Dictionary<uint, ContentFinderCondition>? _dutyByInstance;

    /// <summary>
    /// The exact quest or duty that unlocks a part, read from BannerCondition:
    /// prerequisite type 1 is a quest, types 3 and 4 are an InstanceContent (unlock or clear the duty).
    /// The link points to the same quest or duty on Garland Tools (same ids as the game).
    /// </summary>
    public (string? Text, string? Link) GetUnlockSource(uint conditionId)
    {
        if (!excelService.TryGetRow<BannerCondition>(conditionId, out var condition))
            return (null, null);

        switch (condition.PrerequisiteType)
        {
            case 1 when excelService.TryGetRow<Quest>(condition.Prerequisite.RowId, out var quest) && !quest.Name.IsEmpty:
                return ($"Complete the quest \"{quest.Name.ToString().Trim()}\".", Garland.Quest(quest.RowId));

            case 3 or 4:
                _dutyByInstance ??= excelService.GetSheet<ContentFinderCondition>()
                    .Where(c => c.ContentLinkType == 1 && c.Content.RowId != 0)
                    .GroupBy(c => c.Content.RowId)
                    .ToDictionary(g => g.Key, g => g.First());

                if (_dutyByInstance.TryGetValue(condition.Prerequisite.RowId, out var duty) && !duty.Name.IsEmpty)
                {
                    var name = Capitalize(duty.Name.ToString());
                    return (condition.PrerequisiteType == 3 ? $"Unlock {name}." : $"Clear {name}.", Garland.Instance(duty.RowId));
                }

                break;
        }

        // some conditions only name a quest in the first criteria
        if (condition.UnlockType1 == 1 && excelService.TryGetRow<Quest>(condition.UnlockCriteria1[0].RowId, out var q) && !q.Name.IsEmpty)
            return ($"Complete the quest \"{q.Name.ToString().Trim()}\".", Garland.Quest(q.RowId));

        return (null, null);
    }

    private static string Capitalize(string text)
        => text.Length > 0 && char.IsLower(text[0]) ? char.ToUpperInvariant(text[0]) + text[1..] : text;

    /// <summary>The game's own "how to obtain" text for a BannerCondition.</summary>
    public string? GetUnlockHint(uint conditionId)
    {
        if (!excelService.TryGetRow<BannerCondition>(conditionId, out var condition) || !condition.UnlockHint.IsValid)
            return null;

        var text = condition.UnlockHint.Value.Text.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    // --- gear

    /// <summary>
    /// Looks for an item in your inventory and armoury (always known), and in your glamour dresser and armoire:
    /// live when the game has them loaded, otherwise from the lists saved the last time you opened them.
    /// Retainers and the saddlebag are not checked.
    /// </summary>
    public Ownership Item(uint itemId)
    {
        if (itemId == 0)
            return Ownership.Unknown();

        var inventory = InventoryManager.Instance();
        if (inventory != null &&
            (inventory->GetInventoryItemCount(itemId) > 0 || inventory->GetInventoryItemCount(itemId, true) > 0))
        {
            return new Ownership(OwnState.Have, "Inventory or armoury");
        }

        bool? inDresser = null;
        var mirage = MirageManager.Instance();
        if (mirage != null && mirage->PrismBoxLoaded)
        {
            inDresser = false;
            foreach (var id in mirage->PrismBoxItemIds)
            {
                if (id != 0 && id % 1_000_000 == itemId)
                {
                    inDresser = true;
                    break;
                }
            }
        }

        inDresser ??= savedGear.InDresser(itemId);
        if (inDresser == true)
            return new Ownership(OwnState.Have, "Glamour dresser");

        bool? inArmoire = null;
        var uiState = UIState.Instance();
        if (uiState != null && uiState->Cabinet.IsCabinetLoaded())
            inArmoire = GetCabinetId(itemId) is { } cabinetId && uiState->Cabinet.IsItemInCabinet(cabinetId);

        inArmoire ??= savedGear.InArmoire(itemId);
        if (inArmoire == true)
            return new Ownership(OwnState.Have, "Armoire");

        if (inDresser == null || inArmoire == null)
        {
            var notSeen = inDresser == null && inArmoire == null ? "glamour dresser and armoire" : inDresser == null ? "glamour dresser" : "armoire";
            return Ownership.Unknown($"Not in your inventory or armoury. Open your {notSeen} once so it can be checked too.");
        }

        return new Ownership(OwnState.Missing, Hint: "Not in your inventory, armoury, glamour dresser or armoire. Retainers aren't checked.");
    }

    private uint? GetCabinetId(uint itemId)
    {
        _cabinetByItem ??= excelService.GetSheet<Lumina.Excel.Sheets.Cabinet>()
            .Where(c => c.Item.RowId != 0)
            .GroupBy(c => c.Item.RowId)
            .ToDictionary(g => g.Key, g => g.First().RowId);

        return _cabinetByItem.TryGetValue(itemId, out var id) ? id : null;
    }
}
