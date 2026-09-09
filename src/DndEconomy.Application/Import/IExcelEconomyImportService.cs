namespace DndEconomy.Application.Import;

/// <summary>
/// Разбирает загруженные .xlsx-файлы с листами таблицы экономики ("Предметы", "Города",
/// "Сезонность", "Настройки", "Приём монет") и наполняет БД. Каждый лист импортируется
/// отдельным методом — админ грузит файл на профильной странице (например, "Приём монет"
/// на /admin/economy/coin-acceptance), и импортируется только соответствующий лист, даже
/// если в книге есть остальные. Повторную загрузку того же файла делает безопасной —
/// существующие записи обновляются по ключу, а не дублируются.
/// </summary>
public interface IExcelEconomyImportService
{
  /// <summary>Импортирует лист "Предметы" — справочник предметов каталога.</summary>
  Task<EconomyImportSummary> ImportItemsAsync(Stream fileStream, CancellationToken cancellationToken);

  /// <summary>
  /// Импортирует лист "Города" — сам список городов (шапка) и матрицу коэффициентов
  /// "Тип+Подтип × Город" (строки 3+), которая на этом же листе.
  /// </summary>
  Task<EconomyImportSummary> ImportCitiesAsync(Stream fileStream, CancellationToken cancellationToken);

  /// <summary>Импортирует лист "Сезонность" — матрицу коэффициентов "Тип+Подтип × Сезон".</summary>
  Task<EconomyImportSummary> ImportSeasonModifiersAsync(Stream fileStream, CancellationToken cancellationToken);

  /// <summary>
  /// Импортирует лист "Настройки" — игровые сессии ("Партии"). Города сопоставляются по
  /// имени с уже существующими в БД (этот лист их не создаёт).
  /// </summary>
  Task<EconomyImportSummary> ImportSessionsAsync(Stream fileStream, CancellationToken cancellationToken);

  /// <summary>
  /// Импортирует лист "Приём монет" — матрицу приёма номиналов "Номинал × Город" и заметки
  /// о нюансах приёма по городам. Города сопоставляются по имени с уже существующими в БД.
  /// </summary>
  Task<EconomyImportSummary> ImportCoinAcceptanceAsync(Stream fileStream, CancellationToken cancellationToken);
}
