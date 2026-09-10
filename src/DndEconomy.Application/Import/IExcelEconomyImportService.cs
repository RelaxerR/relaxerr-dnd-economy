namespace DndEconomy.Application.Import;

/// <summary>
/// Разбирает загруженные .xlsx-файлы с листами таблицы экономики ("Предметы", "Города",
/// "Сезонность", "Настройки", "Приём монет", "Оплата заданий") и наполняет БД. Каждый лист
/// импортируется отдельным методом — админ грузит файл на профильной странице (например,
/// "Приём монет" на /admin/economy/coin-acceptance), и импортируется только соответствующий
/// лист, даже если в книге есть остальные.
///
/// По умолчанию (<c>replaceExisting: false</c>) повторную загрузку делает безопасной —
/// существующие записи обновляются по ключу, а то, чего нет в файле, остаётся в БД нетронутым.
/// С <c>replaceExisting: true</c> лист полностью синхронизируется с файлом — записи, которых
/// в файле нет, удаляются (см. <see cref="EconomyImportSummary.TotalRemoved"/>); листу "Оплата
/// заданий" эта опция не нужна, он не имеет естественного ключа и всегда заменяется полностью.
///
/// <paramref name="persist"/> = false выполняет тот же разбор и подсчёт (включая то, что было
/// бы удалено), но не пишет изменения в БД — используется для предпросмотра перед подтверждением
/// полной замены (см. <c>ExcelSheetPanel.razor</c>), сам файл при этом безопасно перечитывается
/// повторно с <c>persist: true</c> после подтверждения.
/// </summary>
public interface IExcelEconomyImportService
{
  /// <summary>Импортирует лист "Предметы" — справочник предметов каталога.</summary>
  Task<EconomyImportSummary> ImportItemsAsync(Stream fileStream, bool replaceExisting, bool persist, CancellationToken cancellationToken);

  /// <summary>
  /// Импортирует лист "Города" — сам список городов (шапка) и матрицу коэффициентов
  /// "Тип+Подтип × Город" (строки 3+), которая на этом же листе.
  /// </summary>
  Task<EconomyImportSummary> ImportCitiesAsync(Stream fileStream, bool replaceExisting, bool persist, CancellationToken cancellationToken);

  /// <summary>Импортирует лист "Сезонность" — матрицу коэффициентов "Тип+Подтип × Сезон".</summary>
  Task<EconomyImportSummary> ImportSeasonModifiersAsync(Stream fileStream, bool replaceExisting, bool persist, CancellationToken cancellationToken);

  /// <summary>
  /// Импортирует лист "Настройки" — игровые сессии ("Партии"). Города сопоставляются по
  /// имени с уже существующими в БД (этот лист их не создаёт).
  /// </summary>
  Task<EconomyImportSummary> ImportSessionsAsync(Stream fileStream, bool replaceExisting, bool persist, CancellationToken cancellationToken);

  /// <summary>
  /// Импортирует лист "Приём монет" — матрицу приёма номиналов "Номинал × Город". Города
  /// сопоставляются по имени с уже существующими в БД. Заметки о нюансах приёма по городам
  /// (<c>City.CoinAcceptanceNote</c>) полная замена не трогает — это поле города, а не строка
  /// этой таблицы.
  /// </summary>
  Task<EconomyImportSummary> ImportCoinAcceptanceAsync(Stream fileStream, bool replaceExisting, bool persist, CancellationToken cancellationToken);

  /// <summary>
  /// Импортирует лист "Оплата заданий" — справочник оплаты заданий (Категория × Опасность ×
  /// Эпоха). В отличие от остальных листов здесь нет естественного ключа записи (несколько
  /// заданий одной Категории+Опасности+Эпохи — обычное дело), поэтому загрузка всегда полностью
  /// заменяет текущее содержимое таблицы содержимым листа — параметр <paramref name="replaceExisting"/>
  /// присутствует только для единообразия сигнатуры с остальными методами (используется
  /// <c>ExcelSheetPanel</c>) и игнорируется.
  /// </summary>
  Task<EconomyImportSummary> ImportQuestPayRatesAsync(Stream fileStream, bool replaceExisting, bool persist, CancellationToken cancellationToken);
}
