using DndEconomy.Application.Items;
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
  private readonly ILogger<ItemAdminService> _logger;

  public ItemAdminService(IDbContextFactory<ApplicationDbContext> dbContextFactory, ILogger<ItemAdminService> logger)
  {
    _dbContextFactory = dbContextFactory;
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

    var items = await ApplyFilter(dbContext.Items.AsNoTracking(), input.Filter)
      .OrderBy(i => i.NameRu)
      .ToListAsync(cancellationToken);

    var preview = items
      .Select(item => new BulkPriceUpdatePreviewRow
      {
        ItemId = item.Id,
        NameRu = item.NameRu,
        Type = item.Type,
        Subtype = item.Subtype,
        OldCost = item.BaseCost,
        NewCost = CalculateNewCost(item.BaseCost, input.Operation, input.Value)
      })
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
