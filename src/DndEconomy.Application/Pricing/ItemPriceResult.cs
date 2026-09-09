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
  /// Не используется для услуг (<see cref="IsService"/>) — там доступность и ставка выражаются
  /// через <see cref="CommissionRate"/>.
  /// </summary>
  public decimal? BuyPrice { get; init; }

  /// <summary>Цена, за которую лавочник скупит предмет у игрока. Не используется для услуг.</summary>
  public required decimal SellPrice { get; init; }

  /// <summary>Признак того, что это услуга (Item.IsService), а не физический товар.</summary>
  public bool IsService { get; init; }

  /// <summary>
  /// Только для услуг: доля суммы, которую вернут игроку после комиссии (1 = без комиссии,
  /// 0.93 = комиссия 7%). Null означает "услуга в этом городе не оказывается" — в отличие от
  /// обычных товаров, здесь отсутствие явно заведённого коэффициента НЕ равно 1
  /// (см. <see cref="IEconomyPricingReadStore.GetServiceCityCoefficientAsync"/>).
  /// </summary>
  public decimal? CommissionRate { get; init; }

  /// <summary>Признак доступности: для услуг — CommissionRate положителен, для товаров — BuyPrice не null.</summary>
  public bool IsAvailable => IsService ? CommissionRate is > 0 : BuyPrice is not null;

  /// <summary>Название активной сессии, на основе которой выполнен расчёт.</summary>
  public required string ActiveSessionName { get; init; }

  /// <summary>Город, использованный в расчёте.</summary>
  public required string CityName { get; init; }

  /// <summary>Сезон, использованный в расчёте.</summary>
  public required string SeasonName { get; init; }
}
