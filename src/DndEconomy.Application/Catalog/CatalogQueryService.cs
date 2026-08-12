using DndEconomy.Application.Pricing;
using Microsoft.Extensions.Logging;

namespace DndEconomy.Application.Catalog;

/// <summary>
/// Реализация <see cref="ICatalogQueryService"/>: получает контекст активной сессии один раз
/// (переиспользуя <see cref="IEconomyPricingReadStore"/>), затем через <see cref="ICatalogReadStore"/>
/// одним запросом на страницу читает предметы с уже посчитанной в SQL "сырой" стоимостью
/// и домапливает цены покупки/продажи через общие формулы <see cref="PriceFormulas"/>.
/// </summary>
public sealed class CatalogQueryService : ICatalogQueryService
{
  #region Поля и конструктор

  private readonly ICatalogReadStore _readStore;
  private readonly IEconomyPricingReadStore _pricingReadStore;
  private readonly TimeProvider _timeProvider;
  private readonly ILogger<CatalogQueryService> _logger;

  public CatalogQueryService(
    ICatalogReadStore readStore,
    IEconomyPricingReadStore pricingReadStore,
    TimeProvider timeProvider,
    ILogger<CatalogQueryService> logger)
  {
    _readStore = readStore;
    _pricingReadStore = pricingReadStore;
    _timeProvider = timeProvider;
    _logger = logger;
  }

  #endregion

  #region Публичные методы

  /// <inheritdoc />
  public async Task<CatalogPage> GetPageAsync(CatalogQuery query, CancellationToken cancellationToken)
  {
    var session = await GetActiveSessionOrNullAsync(cancellationToken);
    if (session is null)
      return CatalogPage.Empty(query.PageNumber, query.PageSize);

    // Релевантность без поискового запроса не имеет смысла — тихо переключаемся на сортировку по имени.
    var normalizedQuery = query.SortOrder == CatalogSortOrder.Relevance && string.IsNullOrWhiteSpace(query.SearchTerm)
      ? query with { SortOrder = CatalogSortOrder.NameAsc }
      : query;

    var (rows, totalCount) = await _readStore.GetPageAsync(normalizedQuery, session, cancellationToken);

    return new CatalogPage
    {
      Items = rows.Select(row => ToViewModel(row, session)).ToList(),
      TotalCount = totalCount,
      PageNumber = normalizedQuery.PageNumber,
      PageSize = normalizedQuery.PageSize,
      ActiveSessionName = session.SessionName,
      CityName = session.CityName,
      GameDateLabel = session.GameDateLabel
    };
  }

  /// <inheritdoc />
  public async Task<CatalogItemViewModel?> GetItemAsync(Guid itemId, CancellationToken cancellationToken)
  {
    var session = await GetActiveSessionOrNullAsync(cancellationToken);
    if (session is null)
      return null;

    var row = await _readStore.GetItemAsync(itemId, session, cancellationToken);
    return row is null ? null : ToViewModel(row, session);
  }

  #endregion

  #region Приватные шаги

  private async Task<ActiveSessionContext?> GetActiveSessionOrNullAsync(CancellationToken cancellationToken)
  {
    var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
    var session = await _pricingReadStore.GetActiveSessionContextAsync(today, cancellationToken);
    if (session is null)
      _logger.LogWarning("Каталог: нет активной экономической сессии на дату {Date}", today);

    return session;
  }

  private static CatalogItemViewModel ToViewModel(CatalogPricedRow row, ActiveSessionContext session) => new()
  {
    ItemId = row.ItemId,
    NameRu = row.NameRu,
    NameEn = row.NameEn,
    Category = row.Category,
    Type = row.Type,
    Subtype = row.Subtype,
    Weight = row.Weight,
    IsPlayerSuggested = row.IsPlayerSuggested,
    BuyPrice = PriceFormulas.ResolveBuyPrice(row.CalculatedCost),
    SellPrice = PriceFormulas.ResolveSellPrice(row.CalculatedCost, row.BaseCost, session.SellCoefficient)
  };

  #endregion
}
