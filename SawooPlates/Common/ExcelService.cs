using Lumina.Excel;

namespace SawooPlates.Common;

public sealed class ExcelService(IDataManager dataManager)
{
    public ExcelSheet<T> GetSheet<T>() where T : struct, IExcelRow<T>
        => dataManager.GetExcelSheet<T>();

    public bool TryGetRow<T>(uint rowId, out T row) where T : struct, IExcelRow<T>
        => dataManager.GetExcelSheet<T>().TryGetRow(rowId, out row);
}
