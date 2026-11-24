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
public readonly record struct Ownership(OwnState State, string? Where = null, string? Hint = null)
{
    public static Ownership Unknown(string? why = null) => new(OwnState.Unknown, Hint: why);
}

/// <summary>Checks portrait and plate unlocks and whether the player owns gear shown in a portrait.</summary>
public sealed unsafe class CollectionService(ExcelService excelService, BannerService bannerService)
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

        if (bannerService.IsBannerConditionUnlocked(conditionId.Value))
            return new Ownership(OwnState.Have);

        return new Ownership(OwnState.Missing, Hint: GetUnlockHint(conditionId.Value));
    }

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
    /// Looks for an item in the places the client knows about: equipped, inventory, armoury chest,
    /// glamour dresser and armoire. Retainers and the saddlebag are not checked.
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

        var dresserLoaded = false;
        var mirage = MirageManager.Instance();
        if (mirage != null)
        {
            foreach (var id in mirage->PrismBoxItemIds)
            {
                if (id == 0)
                    continue;

                dresserLoaded = true;
                if (id % 1_000_000 == itemId)
                    return new Ownership(OwnState.Have, "Glamour dresser");
            }
        }

        var armoireLoaded = false;
        var uiState = UIState.Instance();
        if (uiState != null && uiState->Cabinet.IsCabinetLoaded())
        {
            armoireLoaded = true;
            if (GetCabinetId(itemId) is { } cabinetId && uiState->Cabinet.IsItemInCabinet(cabinetId))
                return new Ownership(OwnState.Have, "Armoire");
        }

        if (!dresserLoaded || !armoireLoaded)
        {
            var notLoaded = !dresserLoaded && !armoireLoaded ? "glamour dresser and armoire" : !dresserLoaded ? "glamour dresser" : "armoire";
            return Ownership.Unknown($"Not in your inventory or armoury. Open your {notLoaded} once this session so it can be checked too.");
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
