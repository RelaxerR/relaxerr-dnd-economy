using DndEconomy.Application.Pricing;
using DndEconomy.Domain.Enums;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndEconomy.Infrastructure.Pricing;

/// <inheritdoc cref="ICityCoinAcceptanceReadStore" />
public sealed class CityCoinAcceptanceReadStore : ICityCoinAcceptanceReadStore
{
  private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

  public CityCoinAcceptanceReadStore(IDbContextFactory<ApplicationDbContext> dbContextFactory)
  {
    _dbContextFactory = dbContextFactory;
  }

  /// <inheritdoc />
  public async Task<CityCoinAcceptanceInfo> GetForCityAsync(Guid cityId, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    var note = await dbContext.Cities.AsNoTracking()
      .Where(x => x.Id == cityId)
      .Select(x => x.CoinAcceptanceNote)
      .SingleOrDefaultAsync(cancellationToken);

    var ratesByDenomination = await dbContext.CityCoinAcceptances.AsNoTracking()
      .Where(x => x.CityId == cityId)
      .ToDictionaryAsync(x => x.Denomination, x => x.AcceptanceRate, cancellationToken);

    // Все 5 номиналов присутствуют всегда — отсутствие строки означает "принимается по
    // номиналу без скидки" (тот же приём умолчания, что и у CityModifier для товаров).
    var denominations = Enum.GetValues<CoinDenomination>()
      .Select(denomination => new CoinAcceptanceRate
      {
        Denomination = denomination,
        AcceptanceRate = ratesByDenomination.GetValueOrDefault(denomination, 1m)
      })
      .ToList();

    return new CityCoinAcceptanceInfo { Denominations = denominations, Note = note };
  }
}
