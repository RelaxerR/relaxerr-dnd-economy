namespace DndEconomy.Application.Import;

/// <summary>
/// Экспортирует текущее состояние городов/сессий/коэффициентов/приёма монет в .xlsx —
/// по одному листу за раз, в том же формате, что читает <see cref="IExcelEconomyImportService"/>,
/// чтобы выгруженный файл можно было отредактировать и загрузить обратно на той же
/// странице админки без дублирования записей. Матрицы коэффициентов экспортируются с теми
/// же строками, что видит админ в интерфейсе (включая автоматически подставленные категории
/// предметов) — см. <c>IEconomyAdminService.GetCityModifierMatrixAsync</c> и
/// <c>GetSeasonModifierMatrixAsync</c>.
/// </summary>
public interface IExcelEconomyExportService
{
  /// <summary>Строит книгу .xlsx с листом "Города" — список городов и матрица коэффициентов "Тип+Подтип × Город".</summary>
  Task<byte[]> ExportCitiesAsync(CancellationToken cancellationToken);

  /// <summary>Строит книгу .xlsx с листом "Сезонность" — матрица коэффициентов "Тип+Подтип × Сезон".</summary>
  Task<byte[]> ExportSeasonModifiersAsync(CancellationToken cancellationToken);

  /// <summary>Строит книгу .xlsx с листом "Настройки" — список игровых сессий ("Партий").</summary>
  Task<byte[]> ExportSessionsAsync(CancellationToken cancellationToken);

  /// <summary>Строит книгу .xlsx с листом "Приём монет" — матрица приёма номиналов и заметки по городам.</summary>
  Task<byte[]> ExportCoinAcceptanceAsync(CancellationToken cancellationToken);

  /// <summary>Строит книгу .xlsx с листом "Оплата заданий" — справочник оплаты заданий мастера.</summary>
  Task<byte[]> ExportQuestPayRatesAsync(CancellationToken cancellationToken);
}
