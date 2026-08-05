namespace DndEconomy.Application.Import;

/// <summary>
/// Сервис первичной загрузки "Модели" предметов — той самой исходной таблицы — в пустую БД.
/// Доступен администратору через отдельный экран загрузки файла.
/// </summary>
public interface IExcelEconomyImportService
{
  /// <summary>
  /// Разбирает загруженный .xlsx (листы "Исходник ДнД", "Города", "Сезонность", "Настройки")
  /// и создаёт соответствующие записи в БД. Существующие записи с тем же ключом обновляются,
  /// а не дублируются, поэтому файл можно перезаливать при правках.
  /// </summary>
  /// <param name="fileStream">Поток содержимого .xlsx файла.</param>
  /// <param name="cancellationToken">Токен отмены.</param>
  Task<EconomyImportSummary> ImportAsync(Stream fileStream, CancellationToken cancellationToken);
}
