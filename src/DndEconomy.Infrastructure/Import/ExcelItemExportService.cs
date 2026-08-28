using ClosedXML.Excel;
using DndEconomy.Application.Import;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndEconomy.Infrastructure.Import;

/// <inheritdoc cref="IExcelItemExportService" />
public sealed class ExcelItemExportService : IExcelItemExportService
{
  #region Поля, конструктор, названия колонок

  private const string ItemsSheetName = "Предметы";

  private static readonly string[] Headers =
  [
    "Категория", "Тип", "Подтип", "Название", "Базовая стоимость", "Вес", "UUID (Foundry)"
  ];

  private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

  public ExcelItemExportService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
  {
    _dbContextFactory = dbContextFactory;
  }

  #endregion

  #region Публичные методы

  /// <inheritdoc />
  public async Task<byte[]> ExportItemsAsync(CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    var items = await dbContext.Items.AsNoTracking()
      .OrderBy(x => x.Category)
      .ThenBy(x => x.Type)
      .ThenBy(x => x.Subtype)
      .ThenBy(x => x.NameRu)
      .ToListAsync(cancellationToken);

    using var workbook = new XLWorkbook();
    var sheet = workbook.Worksheets.Add(ItemsSheetName);

    for (var column = 0; column < Headers.Length; column++)
    {
      sheet.Cell(1, column + 1).Value = Headers[column];
    }

    // Тот же формат колонок (Категория/Тип/Подтип/Название/Стоимость/Вес/UUID), что читает
    // ExcelEconomyImportService.ImportItemsAsync — выгруженный файл можно править и загружать
    // обратно через /admin/import без дублирования записей.
    var row = 2;
    foreach (var item in items)
    {
      sheet.Cell(row, 1).Value = item.Category;
      sheet.Cell(row, 2).Value = item.Type;
      sheet.Cell(row, 3).Value = item.Subtype;
      sheet.Cell(row, 4).Value = FormatName(item.NameRu, item.NameEn);
      sheet.Cell(row, 5).Value = item.BaseCost;
      sheet.Cell(row, 6).Value = item.Weight;
      sheet.Cell(row, 7).Value = item.ExternalUuid;
      row++;
    }

    sheet.Columns(1, Headers.Length).AdjustToContents();

    using var stream = new MemoryStream();
    workbook.SaveAs(stream);
    return stream.ToArray();
  }

  #endregion

  #region Приватные шаги

  /// <summary>Собирает колонку "Название" в формате "Русское [English]", как в исходной таблице.</summary>
  private static string FormatName(string nameRu, string? nameEn)
    => string.IsNullOrWhiteSpace(nameEn) ? nameRu : $"{nameRu} [{nameEn}]";

  #endregion
}
