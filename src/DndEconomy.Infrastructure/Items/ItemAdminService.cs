using DndEconomy.Application.Items;
using DndEconomy.Application.Pricing;
using DndEconomy.Domain.Entities;
using DndEconomy.Domain.Enums;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DndEconomy.Infrastructure.Items;

/// <inheritdoc cref="IItemAdminService" />
public sealed class ItemAdminService : IItemAdminService
{
  #region Поля и конструктор

  private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
  private readonly IEconomyPricingReadStore _pricingReadStore;
  private readonly TimeProvider _timeProvider;
  private readonly ILogger<ItemAdminService> _logger;

  public ItemAdminService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IEconomyPricingReadStore pricingReadStore,
    TimeProvider timeProvider,
    ILogger<ItemAdminService> logger)
  {
    _dbContextFactory = dbContextFactory;
    _pricingReadStore = pricingReadStore;
    _timeProvider = timeProvider;
    _logger = logger;
  }

  #endregion

  #region Публичные методы

  /// <inheritdoc />
  public async Task<Guid> CreateItemAsync(NewItemInput input, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    var item = new Item
    {
      Category = input.Category,
      Type = input.Type,
      Subtype = input.Subtype,
      NameRu = input.NameRu,
      NameEn = input.NameEn,
      BaseCost = input.BaseCost,
      Weight = input.Weight,
      IsPlayerSuggested = input.IsPlayerSuggested
    };

    dbContext.Items.Add(item);
    await dbContext.SaveChangesAsync(cancellationToken);
    return item.Id;
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<BulkPriceUpdatePreviewRow>> PreviewBulkPriceUpdateAsync(
    BulkPriceUpdateInput input, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
    var session = await _pricingReadStore.GetActiveSessionContextAsync(today, cancellationToken);

    var rows = await BuildItemsWithModifiersQuery(dbContext, input.Filter, session)
      .OrderBy(x => x.NameRu)
      .ToListAsync(cancellationToken);

    var preview = rows
      .Select(row => BuildPreviewRow(row, input.Operation, input.Value, session))
      .ToList();

    _logger.LogInformation(
      "Превью массового изменения стоимости: диапазон [{Min};{Max}], операция {Operation} {Value}, затронуто {Count} предметов",
      input.Filter.MinCost, input.Filter.MaxCost, input.Operation, input.Value, preview.Count);

    return preview;
  }

  /// <inheritdoc />
  public async Task<int> ApplyBulkPriceUpdateAsync(BulkPriceUpdateInput input, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    var items = await ApplyFilter(dbContext.Items, input.Filter).ToListAsync(cancellationToken);
    var now = DateTime.UtcNow;

    foreach (var item in items)
    {
      item.BaseCost = CalculateNewCost(item.BaseCost, input.Operation, input.Value);
      item.UpdatedAtUtc = now;
    }

    await dbContext.SaveChangesAsync(cancellationToken);

    _logger.LogInformation(
      "Массовое изменение стоимости применено: диапазон [{Min};{Max}], операция {Operation} {Value}, изменено {Count} предметов",
      input.Filter.MinCost, input.Filter.MaxCost, input.Operation, input.Value, items.Count);

    return items.Count;
  }

  #endregion

  #region Приватные шаги

  private static IQueryable<Item> ApplyFilter(IQueryable<Item> query, BulkPriceUpdateFilter filter)
  {
    if (filter.MinCost.HasValue)
      query = query.Where(i => i.BaseCost >= filter.MinCost.Value);

    if (filter.MaxCost.HasValue)
      query = query.Where(i => i.BaseCost <= filter.MaxCost.Value);

    return query;
  }

  /// <summary>
  /// Отобранные по фильтру предметы вместе с коэффициентами города/сезона активной сессии
  /// (1, если модификатора для пары Тип+Подтип нет) — одним запросом с LEFT JOIN, тем же
  /// приёмом, что и <c>CatalogReadStore.BuildPricedQuery</c>, чтобы не дёргать
  /// IEconomyPricingReadStore по коэффициенту на каждый предмет отдельно (N+1). Если активной
  /// сессии нет вообще, коэффициенты не имеют смысла — возвращаются как 1, а итоговую
  /// цену покупки/продажи <see cref="BuildPreviewRow"/> в этом случае не считает совсем.
  /// </summary>
  private static IQueryable<ItemWithModifiers> BuildItemsWithModifiersQuery(
    ApplicationDbContext dbContext, BulkPriceUpdateFilter filter, ActiveSessionContext? session)
  {
    var items = ApplyFilter(dbContext.Items.AsNoTracking(), filter);

    if (session is null)
    {
      return items.Select(item => new ItemWithModifiers
      {
        ItemId = item.Id,
        NameRu = item.NameRu,
        Type = item.Type,
        Subtype = item.Subtype,
        BaseCost = item.BaseCost,
        CityCoefficient = 1m,
        SeasonCoefficient = 1m
      });
    }

    var cityModifiers = dbContext.CityModifiers.Where(cm => cm.CityId == session.CityId);
    var seasonModifiers = dbContext.SeasonModifiers.Where(sm => sm.Season == session.Season);

    return
      from item in items
      join cityMod in cityModifiers
        on new { item.Type, item.Subtype } equals new { cityMod.Type, cityMod.Subtype } into cityModsJoin
      from cityMod in cityModsJoin.DefaultIfEmpty()
      join seasonMod in seasonModifiers
        on new { item.Type, item.Subtype } equals new { seasonMod.Type, seasonMod.Subtype } into seasonModsJoin
      from seasonMod in seasonModsJoin.DefaultIfEmpty()
      select new ItemWithModifiers
      {
        ItemId = item.Id,
        NameRu = item.NameRu,
        Type = item.Type,
        Subtype = item.Subtype,
        BaseCost = item.BaseCost,
        CityCoefficient = cityMod != null ? cityMod.Coefficient : 1m,
        SeasonCoefficient = seasonMod != null ? seasonMod.Coefficient : 1m
      };
  }

  /// <summary>
  /// Строит строку предпросмотра: базовая стоимость до/после операции плюс, если активная
  /// сессия есть, итоговые цены покупки/продажи до/после — те же формулы
  /// (<see cref="PriceFormulas"/>), что использует каталог, применённые к старой и новой
  /// базовой стоимости при одних и тех же коэффициентах сессии/города/сезона.
  /// </summary>
  private static BulkPriceUpdatePreviewRow BuildPreviewRow(
    ItemWithModifiers row, BulkPriceOperation operation, decimal value, ActiveSessionContext? session)
  {
    var newCost = CalculateNewCost(row.BaseCost, operation, value);

    decimal? oldBuyPrice = null;
    decimal? newBuyPrice = null;
    decimal? oldSellPrice = null;
    decimal? newSellPrice = null;

    if (session is not null)
    {
      var oldCalculatedCost = PriceFormulas.CalculateRawCost(row.BaseCost, session.BaseCoefficient, row.CityCoefficient, row.SeasonCoefficient);
      var newCalculatedCost = PriceFormulas.CalculateRawCost(newCost, session.BaseCoefficient, row.CityCoefficient, row.SeasonCoefficient);

      oldBuyPrice = PriceFormulas.ResolveBuyPrice(oldCalculatedCost);
      newBuyPrice = PriceFormulas.ResolveBuyPrice(newCalculatedCost);
      oldSellPrice = PriceFormulas.ResolveSellPrice(oldCalculatedCost, row.BaseCost, session.SellCoefficient);
      newSellPrice = PriceFormulas.ResolveSellPrice(newCalculatedCost, newCost, session.SellCoefficient);
    }

    return new BulkPriceUpdatePreviewRow
    {
      ItemId = row.ItemId,
      NameRu = row.NameRu,
      Type = row.Type,
      Subtype = row.Subtype,
      OldCost = row.BaseCost,
      NewCost = newCost,
      OldBuyPrice = oldBuyPrice,
      NewBuyPrice = newBuyPrice,
      OldSellPrice = oldSellPrice,
      NewSellPrice = newSellPrice
    };
  }

  /// <summary>Промежуточная проекция для <see cref="BuildItemsWithModifiersQuery"/>.</summary>
  private sealed class ItemWithModifiers
  {
    public required Guid ItemId { get; init; }
    public required string NameRu { get; init; }
    public required string Type { get; init; }
    public required string Subtype { get; init; }
    public required decimal BaseCost { get; init; }
    public required decimal CityCoefficient { get; init; }
    public required decimal SeasonCoefficient { get; init; }
  }

  /// <summary>
  /// Применяет операцию к базовой стоимости и округляет результат до целого числа монет,
  /// не позволяя ему уйти в отрицательные значения (например, после "прибавить -100" к дешёвому предмету).
  /// </summary>
  private static decimal CalculateNewCost(decimal oldCost, BulkPriceOperation operation, decimal value)
  {
    var rawNewCost = operation switch
    {
      BulkPriceOperation.Add => oldCost + value,
      BulkPriceOperation.Multiply => oldCost * value,
      BulkPriceOperation.SetTo => value,
      _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
    };

    var roundedCost = Math.Round(rawNewCost, 0, MidpointRounding.AwayFromZero);
    return Math.Max(0m, roundedCost);
  }

  #endregion
}
