namespace DndEconomy.Application.Import;

/// <summary>
/// Экспортирует справочник предметов каталога в .xlsx с той же структурой листа "Предметы"
/// (см. <see cref="IExcelEconomyImportService"/> и /templates/economy-items.xlsx), чтобы
/// выгруженный файл можно было отредактировать и загрузить обратно через /admin/import.
/// </summary>
public interface IExcelItemExportService
{
  /// <summary>Строит книгу .xlsx с текущим справочником предметов и возвращает её байты.</summary>
  Task<byte[]> ExportItemsAsync(CancellationToken cancellationToken);
}
