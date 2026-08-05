using DndEconomy.Domain.Enums;

namespace DndEconomy.Application.Pricing;

/// <summary>
/// Узкий read-only порт для чтения данных, необходимых PriceCalculationService.
/// Реализация живёт в Infrastructure (прямые запросы к EF Core DbContext) — здесь только контракт,
/// чтобы Application не зависел от EF Core и деталей хранения.
/// </summary>
public interface IEconomyPricingReadStore
{
  /// <summary>Возвращает минимальный набор полей предмета для расчёта цены.</summary>
  Task<ItemPricingSource?> GetItemPricingSourceAsync(Guid itemId, CancellationToken cancellationToken);

  /// <summary>
  /// Возвращает контекст мира из сессии, ближайшей по дате к <paramref name="asOfDate"/>,
  /// но не позже неё (аналог XLOOKUP с режимом поиска "точное или предыдущее меньшее значение").
  /// </summary>
  Task<ActiveSessionContext?> GetActiveSessionContextAsync(DateOnly asOfDate, CancellationToken cancellationToken);

  /// <summary>Возвращает коэффициент города для пары Тип+Подтип. 1, если явного модификатора нет.</summary>
  Task<decimal> GetCityCoefficientAsync(string type, string subtype, Guid cityId, CancellationToken cancellationToken);

  /// <summary>Возвращает коэффициент сезона для пары Тип+Подтип. 1, если явного модификатора нет.</summary>
  Task<decimal> GetSeasonCoefficientAsync(string type, string subtype, Season season, CancellationToken cancellationToken);
}
