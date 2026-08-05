namespace DndEconomy.Application.Import;

/// <summary>
/// Сводка результата импорта исходной Excel-таблицы: сколько записей создано по каждому листу.
/// Показывается администратору после загрузки файла, чтобы он видел, что импорт прошёл полностью.
/// </summary>
public sealed class EconomyImportSummary
{
  public int ItemsImported { get; set; }
  public int CitiesImported { get; set; }
  public int CityModifiersImported { get; set; }
  public int SeasonModifiersImported { get; set; }
  public int SessionsImported { get; set; }

  /// <summary>Предупреждения, не прервавшие импорт (например, пропущенная пустая строка).</summary>
  public List<string> Warnings { get; } = [];
}
