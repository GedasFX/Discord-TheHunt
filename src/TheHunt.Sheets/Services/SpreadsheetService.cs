using System.Net;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using TheHunt.Core.Exceptions;
using TheHunt.Core.Extensions;
using TheHunt.Data.Models;
using TheHunt.Sheets.Models;
using TheHunt.Sheets.Utils;

namespace TheHunt.Sheets.Services;

public class SpreadsheetService(string googleCredentialsFile)
{
    private readonly SheetsService _service = new(new BaseClientService.Initializer
    {
        HttpClientInitializer = GoogleCredential.FromFile(googleCredentialsFile)
    });

    #region General

    public async Task<SheetsRef> CreateCompetition(string spreadsheetId, string sheetName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var createBatch = await _service.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest()
            {
                Requests =
                [
                    new Request { AddSheet = CreateSheet(sheetName, "overview", frozenRowCount: 0) },
                    new Request { AddSheet = CreateSheet(sheetName, "members", 3) },
                    new Request { AddSheet = CreateSheet(sheetName, "items", 2) },
                    new Request { AddSheet = CreateSheet(sheetName, "submissions", 12) }
                ]
            }, spreadsheetId).ExecuteAsync(cancellationToken);

            await _service.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest()
            {
                Requests =
                [
                    new Request { UpdateCells = AddHeaderRow(createBatch, 1, "Id", "Name", "Team") },
                    new Request { UpdateCells = AddHeaderRow(createBatch, 2, "Item Name", "Points Value") },
                    new Request
                    {
                        UpdateCells = AddHeaderRow(createBatch, 3,
                            "Id", "Image", "Date", "Item", "Team", "Submitter Id", "Submitter", "Verifier Id",
                            "Verifier", "Points Item", "Points Bonus", "Points Total")
                    }
                ]
            }, spreadsheetId).ExecuteAsync(cancellationToken);

            return new SheetsRef
            {
                SpreadsheetId = spreadsheetId, SheetName = sheetName,
                Sheets = new SheetsRef.Sheet()
                {
                    Overview = (int)createBatch.Replies[0].AddSheet.Properties.SheetId!,
                    Members = (int)createBatch.Replies[1].AddSheet.Properties.SheetId!,
                    Items = (int)createBatch.Replies[2].AddSheet.Properties.SheetId!,
                    Submissions = (int)createBatch.Replies[3].AddSheet.Properties.SheetId!,
                },
            };
        }
        catch (GoogleApiException e) when (e is
                                           {
                                               HttpStatusCode: HttpStatusCode.Forbidden,
                                               Error.Message: "The caller does not have permission"
                                           })
        {
            throw new EntityValidationException(
                "The bot does not have permission to edit this spreadsheet.\n" +
                "Send an invitation to `the-hunt@the-hunt-373015.iam.gserviceaccount.com` with `Editor` permissions.",
                e);
        }
        catch (GoogleApiException e) when (e is { HttpStatusCode: HttpStatusCode.BadRequest })
        {
            throw new EntityValidationException(
                "Error while calling Google Sheets API:\n" +
                e.Error.Message, e);
        }
    }

    #endregion
    
    #region Members

    public async Task<IReadOnlyList<CompetitionUser>> GetMembers(SheetsRef sheet,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var data = await _service.Spreadsheets.Values.BatchGetByDataFilter(new BatchGetValuesByDataFilterRequest()
            {
                DataFilters =
                [
                    new DataFilter
                    {
                        GridRange = new GridRange
                        {
                            SheetId = sheet.Sheets.Members, StartRowIndex = 1, StartColumnIndex = 0, EndColumnIndex = 1
                        }
                    }
                ]
            }, sheet.SpreadsheetId).ExecuteAsync(cancellationToken);

            IReadOnlyList<CompetitionUser> result =
            [
                ..data.ValueRanges[0].ValueRange.Values?
                      .Select((t, i) => (List: t, Idx: i)).Where(t => t.List?.Count > 0)
                      .Select(t => new CompetitionUser
                      {
                          UserId = ulong.Parse((string)t.List[0]), RowIdx = t.Idx,
                      })
                  ?? []
            ];
            
            return result;
        }
        catch (Exception e)
        {
            throw new EntityValidationException(
                "Members sheet is malformed. Make sure first column consists of only user ids, and you do not have any duplicate entries.",
                e);
        }
    }

    public async Task AddMember(SheetsRef sheet, ulong userId, string displayName, string? team)
    {
        await _service.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest()
        {
            Requests =
            [
                SheetUtils.AppendRow(sheet.Sheets.Members, [
                    SheetUtils.StringCell(userId.ToString()),
                    SheetUtils.StringCell(displayName),
                    SheetUtils.StringCell(team)
                ])
            ]
        }, sheet.SpreadsheetId).ExecuteAsync();
    }

    public async Task RemoveMember(SheetsRef sheet, int rowNumber, CancellationToken cancellationToken = default)
    {
        await RemoveRow(sheet.SpreadsheetId, sheet.Sheets.Members, rowNumber, cancellationToken);
    }

    #endregion

    #region Items

    public async Task<IReadOnlyList<CompetitionItem>> GetItems(SheetsRef sheet,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var data = await _service.Spreadsheets.Values.BatchGetByDataFilter(new BatchGetValuesByDataFilterRequest()
            {
                DataFilters =
                [
                    new DataFilter
                    {
                        GridRange = new GridRange
                        {
                            SheetId = sheet.Sheets.Items, StartRowIndex = 1, StartColumnIndex = 0, EndColumnIndex = 1
                        }
                    }
                ]
            }, sheet.SpreadsheetId).ExecuteAsync(cancellationToken);

            IReadOnlyList<CompetitionItem> result =
            [
                ..data.ValueRanges[0].ValueRange.Values?
                      .Select((t, i) => (List: t, Idx: i))
                      .Where(t => t.List?.Count > 0)
                      .Select(t => (Name: t.List[0] as string, t.Idx))
                      .Where(t => !string.IsNullOrEmpty(t.Name))
                      .Select(t => new CompetitionItem
                      {
                          Name = t.Name!, RowIdx = t.Idx,
                      })
                  ?? []
            ];

            return result;
        }
        catch (Exception e)
        {
            throw new EntityValidationException(
                "Items sheet is malformed. Make sure first column consists of only item names, and you do not have any duplicate entries.",
                e);
        }
    }

    public async Task AddItem(SheetsRef sheetsRef, string name, int pointsValue = 0,
        CancellationToken cancellationToken = default)
    {
        await _service.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest()
        {
            Requests =
            [
                SheetUtils.AppendRow(sheetsRef.Sheets.Items, [
                    SheetUtils.StringCell(name),
                    SheetUtils.NumberCell(pointsValue)
                ])
            ]
        }, sheetsRef.SpreadsheetId).ExecuteAsync(cancellationToken);
    }
    
    public async Task RemoveItem(SheetsRef sheet, int rowNumber, CancellationToken cancellationToken = default)
    {
        await RemoveRow(sheet.SpreadsheetId, sheet.Sheets.Items, rowNumber, cancellationToken);
    }

    #endregion

    #region Submissions

    public async Task AddSubmission(SheetsRef sheetsRef, ulong submissionId, string submissionUrl, ulong submitterId,
        ulong verifierId, string? imageUrl, DateTime date, string? item, int bonus, CancellationToken cancellationToken = default)
    {
        await _service.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest()
        {
            Requests =
            [
                SheetUtils.AppendRow(sheetsRef.Sheets.Submissions, [
                    SheetUtils.FormulaCell($"=HYPERLINK(\"{submissionUrl}\", \"{submissionId}\")"),
                    SheetUtils.FormulaCell(imageUrl != null ? $"=HYPERLINK(\"{imageUrl}\")" : null),
                    SheetUtils.FormulaCell(date.ToString("=DATE(yyyy,MM,dd) + TI\\ME(HH,mm,ss)")),
                    SheetUtils.StringCell(item),
                    SheetUtils.FormulaCell($"=IF(ISBLANK(VLOOKUP(INDIRECT(\"R[0]C6\", FALSE), '__{sheetsRef.SheetName}_members'!A$2:D, 3, FALSE)), INDIRECT(\"R[0]C7\", FALSE), VLOOKUP(INDIRECT(\"R[0]C6\", FALSE), '__{sheetsRef.SheetName}_members'!A$2:D, 3, FALSE))"),
                    SheetUtils.StringCell(submitterId.ToString()),
                    SheetUtils.FormulaCell($"=VLOOKUP(INDIRECT(\"R[0]C6\", FALSE), '__{sheetsRef.SheetName}_members'!A$2:B, 2, FALSE)"),
                    SheetUtils.StringCell(verifierId.ToString()),
                    SheetUtils.FormulaCell($"=VLOOKUP(INDIRECT(\"R[0]C8\", FALSE), '__{sheetsRef.SheetName}_members'!A$2:B, 2, FALSE)"),
                    SheetUtils.FormulaCell($"=VLOOKUP(INDIRECT(\"R[0]C4\", FALSE), '__{sheetsRef.SheetName}_items'!A$2:C, 2, FALSE)"),
                    SheetUtils.NumberCell(bonus),
                    SheetUtils.FormulaCell("=IFNA(INDIRECT(\"R[0]C[-2]\", FALSE)) + INDIRECT(\"R[0]C[-1]\", FALSE)")
                ])
            ]
        }, sheetsRef.SpreadsheetId).ExecuteAsync(cancellationToken);
    }

    #endregion

    #region === Utils ===

    public async Task RemoveRow(string spreadsheetId, int sheetId, int rowNumber,
        CancellationToken cancellationToken = default)
    {
        await _service.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest()
        {
            Requests =
            [
                new Request()
                {
                    DeleteDimension = new DeleteDimensionRequest()
                    {
                        Range = new DimensionRange()
                        {
                            Dimension = "ROWS", SheetId = sheetId,
                            StartIndex = rowNumber + 1, EndIndex = rowNumber + 2,
                        }
                    }
                }
            ]
        }, spreadsheetId).ExecuteAsync(cancellationToken);
    }

    #endregion

   
    private static UpdateCellsRequest AddHeaderRow(BatchUpdateSpreadsheetResponse createBatch, int index,
        params string[] header)
    {
        return new UpdateCellsRequest()
        {
            Start = new GridCoordinate()
            {
                ColumnIndex = 0, RowIndex = 0, SheetId = createBatch.Replies[index].AddSheet.Properties.SheetId
            },
            Fields = "*", Rows =
            [
                new RowData { Values = GetHeaderCells(header) }
            ]
        };
    }

    private static AddSheetRequest CreateSheet(string sheetName, string name, int? columnCount = null,
        int? frozenRowCount = 1)
    {
        return new AddSheetRequest()
        {
            Properties = new SheetProperties()
            {
                Title = $"__{sheetName}_{name}",
                GridProperties = new GridProperties() { FrozenRowCount = frozenRowCount, ColumnCount = columnCount },
            }
        };
    }

    private static IList<CellData> GetHeaderCells(params string[] values)
    {
        return
        [
            ..values.Select(value => new CellData()
            {
                UserEnteredValue = new ExtendedValue() { StringValue = value },
                UserEnteredFormat = new CellFormat() { TextFormat = new TextFormat() { Bold = true } }
            })
        ];
    }
}