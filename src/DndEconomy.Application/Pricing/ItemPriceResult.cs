namespace DndEconomy.Application.Pricing;

/// <summary>
/// Результат расчёта текущей цены предмета — аналог одной строки листа
/// "Текущая стоимость" исходной таблицы, только посчитанный "на лету".
/// </summary>
public sealed class ItemPriceResult
{
  /// <summary>Идентификатор предмета, для которого выполнен расчёт.</summary>
  public required Guid ItemId { get; init; }

  /// <summary>
  /// Цена покупки у лавочника. Null означает "Нет в наличии" — предмет в этом городе/сезоне не продаётся.
  /// </summary>
  public decimal? BuyPrice { get; init; }

  /// <summary>Цена, за которую лавочник скупит предмет у игрока.</summary>
  public required decimal SellPrice { get; init; }

  /// <summary>Признак доступности предмета для покупки (BuyPrice не null).</summary>
  public bool IsAvailable => BuyPrice is not null;

  /// <summary>Название активной сессии, на основе которой выполнен расчёт.</summary>
  public required string ActiveSessionName { get; init; }

  /// <summary>Город, использованный в расчёте.</summary>
  public required string CityName { get; init; }

  /// <summary>Сезон, использованный в расчёте.</summary>
  public required string SeasonName { get; init; }
}
