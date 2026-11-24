using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace SawooPlates.Capture;

/// <summary>Reads the gear a portrait shows (glamour when set) from its CharaView.</summary>
public sealed unsafe class GearReader(ExcelService excelService)
{
    public List<GearPiece> Read(CharaViewPortrait* charaView)
    {
        var gear = new List<GearPiece>();
        if (charaView == null)
            return gear;

        var items = charaView->Items;
        for (var i = 0; i < items.Length; i++)
        {
            ref var item = ref items[i];

            var glamoured = item.GlamourItemId != 0;
            var itemId = glamoured ? item.GlamourItemId : item.ItemId;
            if (itemId == 0)
                continue;

            // soul crystals are not shown on portraits
            if (excelService.TryGetRow<Item>(itemId % 1_000_000, out var row)
                && row.EquipSlotCategory.IsValid
                && row.EquipSlotCategory.Value.SoulCrystal != 0)
            {
                continue;
            }

            gear.Add(new GearPiece
            {
                Slot = (byte)i,
                ItemId = itemId % 1_000_000,
                Stain0 = glamoured ? item.GlamourStain0Id : item.Stain0Id,
                Stain1 = glamoured ? item.GlamourStain1Id : item.Stain1Id,
            });
        }

        return gear;
    }
}
