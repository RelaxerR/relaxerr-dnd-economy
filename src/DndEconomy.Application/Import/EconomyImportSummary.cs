namespace DndEconomy.Application.Import;

/// <summary>
/// Сводка результата импорта исходной Excel-таблицы: сколько записей создано/обновлено (и,
/// при полной замене — см. <c>replaceExisting</c> у методов <see cref="IExcelEconomyImportService"/>,
/// сколько удалено) по каждому листу. Показывается администратору после загрузки файла, чтобы
/// он видел, что импорт прошёл полностью.
/// </summary>
public sealed class EconomyImportSummary
{
  public int ItemsImported { get; set; }
  public int CitiesImported { get; set; }
  public int CityModifiersImported { get; set; }
  public int SeasonModifiersImported { get; set; }
  public int SessionsImported { get; set; }
  public int CoinAcceptancesImported { get; set; }
  public int QuestPayRatesImported { get; set; }

  /// <summary>
  /// Сколько записей удалено из-за полной замены (<c>replaceExisting: true</c>) — то, что было
  /// в БД, но отсутствовало в загруженном файле. Ноль при обычном импорте (обновление по ключу).
  /// </summary>
  public int ItemsRemoved { get; set; }
  public int CitiesRemoved { get; set; }
  public int CityModifiersRemoved { get; set; }
  public int SeasonModifiersRemoved { get; set; }
  public int SessionsRemoved { get; set; }
  public int CoinAcceptancesRemoved { get; set; }
  public int QuestPayRatesRemoved { get; set; }

  /// <summary>Суммарно удалено по всем листам этого вызова — используется для confirm-диалога перед полной заменой.</summary>
  public int TotalRemoved =>
    ItemsRemoved + CitiesRemoved + CityModifiersRemoved + SeasonModifiersRemoved + SessionsRemoved + CoinAcceptancesRemoved + QuestPayRatesRemoved;

  /// <summary>Предупреждения, не прервавшие импорт (например, пропущенная пустая строка).</summary>
  public List<string> Warnings { get; } = [];
}
