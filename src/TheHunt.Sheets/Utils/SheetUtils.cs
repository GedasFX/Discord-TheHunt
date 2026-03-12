using Google.Apis.Sheets.v4.Data;

namespace TheHunt.Sheets.Utils;

public static class SheetUtils
{
    public static CellData NumberCell(double value)
    {
        return new CellData
        {
            UserEnteredValue = new ExtendedValue() { NumberValue = value }
        };
    }

    public static CellData StringCell(string? value)
    {
        return new CellData
        {
            UserEnteredValue = value != null ? new ExtendedValue() { StringValue = value } : null,
        };
    }
    
    public static CellData FormulaCell(string? value)
    {
        return new CellData
        {
            UserEnteredValue = value != null ? new ExtendedValue() { FormulaValue = value } : null,
        };
    }

    public static CellData StringDropdownCell(string? value, IEnumerable<string> choices)
    {
        return new CellData
        {
            UserEnteredValue = value != null ? new ExtendedValue() { StringValue = value } : null,
            DataValidation = new DataValidationRule
            {
                Condition = new BooleanCondition
                {
                    Type = "ONE_OF_LIST",
                    Values = choices.Select(c => new ConditionValue { UserEnteredValue = c, }).ToList()
                }
            }
        };
    }

    public static IList<RowData> SingleRow(params CellData[] cells)
    {
        return [new RowData { Values = cells }];
    }

    public static Request AppendRow(int sheetId, IList<CellData> cells)
    {
        return new Request
        {
            AppendCells = new AppendCellsRequest
            {
                SheetId = sheetId, Fields = "*",
                Rows = [new RowData { Values = cells }],
            }
        };
    }

    public static Request UpdateCells(int sheetId, int rowIdx, int colIdx, IList<RowData> rows)
    {
        return new Request
        {
            UpdateCells = new UpdateCellsRequest()
            {
                Rows = rows, Fields = "*",
                Start = new GridCoordinate { SheetId = sheetId, RowIndex = rowIdx, ColumnIndex = colIdx, }
            }
        };
    }
}