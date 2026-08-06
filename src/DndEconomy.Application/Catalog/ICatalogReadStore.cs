using DndEconomy.Application.Pricing;

namespace DndEconomy.Application.Catalog;

/// <summary>
/// Порт для эффективного (без N+1) чтения каталога предметов вместе с ценой,
/// посчитанной прямо в SQL для контекста активной экономической сессии.
/// </summary>
public interface ICatalogReadStore
{
  /// <summary>
  /// Одна страница каталога. Цена (CalculatedCost) считается через LEFT JOIN на
  /// CityModifiers/SeasonModifiers активной сессии — поэтому фильтр "в наличии" и сортировка
  /// по цене работают на всей выборке, а не только на уже выбранной странице.
  /// </summary>
  Task<(IReadOnlyList<CatalogPricedRow> Rows, int TotalCount)> GetPageAsync(
    CatalogQuery query, ActiveSessionContext session, CancellationToken cancellationToken);

  /// <summary>Один предмет — для карточки товара.</summary>
  Task<CatalogPricedRow?> GetItemAsync(Guid itemId, ActiveSessionContext session, CancellationToken cancellationToken);

  /// <summary>Уникальные значения Category — для дропдауна фильтра.</summary>
  Task<IReadOnlyList<string>> GetDistinctCategoriesAsync(CancellationToken cancellationToken);

  /// <summary>Уникальные значения Type, опционально ограниченные выбранной Category.</summary>
  Task<IReadOnlyList<string>> GetDistinctTypesAsync(string? category, CancellationToken cancellationToken);

  /// <summary>Уникальные значения Subtype, опционально ограниченные выбранным Type.</summary>
  Task<IReadOnlyList<string>> GetDistinctSubtypesAsync(string? type, CancellationToken cancellationToken);
}
