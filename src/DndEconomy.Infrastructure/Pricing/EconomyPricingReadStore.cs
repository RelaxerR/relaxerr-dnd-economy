using DndEconomy.Application.Pricing;
using DndEconomy.Domain.Enums;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DndEconomy.Infrastructure.Pricing;

/// <summary>
/// EF Core-реализация <see cref="IEconomyPricingReadStore"/>. Каждый метод — отдельный
/// узкий запрос, чтобы PriceCalculationService мог их переиспользовать и кэшировать по отдельности.
/// </summary>
public sealed class EconomyPricingReadStore : IEconomyPricingReadStore
{
  #region Поля и конструктор

  private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
  private readonly ILogger<EconomyPricingReadStore> _logger;

  public EconomyPricingReadStore(IDbContextFactory<ApplicationDbContext> dbContextFactory, ILogger<EconomyPricingReadStore> logger)
  {
    _dbContextFactory = dbContextFactory;
    _logger = logger;
  }

  #endregion

  #region Чтение предмета

  /// <inheritdoc />
  public async Task<ItemPricingSource?> GetItemPricingSourceAsync(Guid itemId, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    return await dbContext.Items
      .Where(x => x.Id == itemId)
      .Select(x => new ItemPricingSource
      {
        ItemId = x.Id,
        Type = x.Type,
        Subtype = x.Subtype,
        BaseCost = x.BaseCost
      })
      .SingleOrDefaultAsync(cancellationToken);
  }

  #endregion

  #region Чтение активной сессии

  /// <inheritdoc />
  public async Task<ActiveSessionContext?> GetActiveSessionContextAsync(DateOnly asOfDate, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    var session = await dbContext.EconomySessions
      .Include(x => x.City)
      .Where(x => x.RealDate <= asOfDate)
      .OrderByDescending(x => x.RealDate)
      .FirstOrDefaultAsync(cancellationToken);

    if (session is null || session.City is null)
    {
      _logger.LogWarning("Не найдена активная сессия с городом на дату {Date}", asOfDate);
      return null;
    }

    return new ActiveSessionContext
    {
      SessionName = session.Name,
      CityId = session.City.Id,
      CityName = session.City.Name,
      Season = session.Season,
      BaseCoefficient = session.BaseCoefficient,
      SellCoefficient = session.SellCoefficient
    };
  }

  #endregion

  #region Чтение коэффициентов

  /// <inheritdoc />
  public async Task<decimal> GetCityCoefficientAsync(string type, string subtype, Guid cityId, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    var coefficient = await dbContext.CityModifiers
      .Where(x => x.Type == type && x.Subtype == subtype && x.CityId == cityId)
      .Select(x => (decimal?)x.Coefficient)
      .SingleOrDefaultAsync(cancellationToken);

    return coefficient ?? 1m;
  }

  /// <inheritdoc />
  public async Task<decimal> GetSeasonCoefficientAsync(string type, string subtype, Season season, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    var coefficient = await dbContext.SeasonModifiers
      .Where(x => x.Type == type && x.Subtype == subtype && x.Season == season)
      .Select(x => (decimal?)x.Coefficient)
      .SingleOrDefaultAsync(cancellationToken);

    return coefficient ?? 1m;
  }

  #endregion
}
