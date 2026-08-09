using DndEconomy.Application.Economy;
using DndEconomy.Domain.Entities;
using DndEconomy.Domain.Enums;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndEconomy.Infrastructure.Economy;

/// <inheritdoc cref="IEconomyAdminService" />
public sealed class EconomyAdminService : IEconomyAdminService
{
  private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

  public EconomyAdminService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
  {
    _dbContextFactory = dbContextFactory;
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<CitySummary>> GetCitiesAsync(CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    return await dbContext.Cities.AsNoTracking()
      .OrderBy(x => x.Name)
      .Select(x => new CitySummary { Id = x.Id, Name = x.Name, Size = x.Size })
      .ToListAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task<Guid> CreateCityAsync(string name, CitySize size, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    var city = new City { Name = name, Size = size };
    dbContext.Cities.Add(city);
    await dbContext.SaveChangesAsync(cancellationToken);
    return city.Id;
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<EconomySessionSummary>> GetSessionsAsync(CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    return await dbContext.EconomySessions.AsNoTracking()
      .Include(x => x.City)
      .OrderByDescending(x => x.RealDate)
      .Select(x => new EconomySessionSummary
      {
        Id = x.Id,
        Name = x.Name,
        RealDate = x.RealDate,
        GameDateLabel = x.GameDateLabel,
        CityName = x.City != null ? x.City.Name : null,
        Season = x.Season,
        BaseCoefficient = x.BaseCoefficient,
        SellCoefficient = x.SellCoefficient
      })
      .ToListAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task CreateSessionAsync(NewEconomySessionInput input, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    dbContext.EconomySessions.Add(new EconomySession
    {
      Name = input.Name,
      Description = input.Description,
      RealDate = input.RealDate,
      GameDateLabel = input.GameDateLabel,
      CityId = input.CityId,
      Season = input.Season,
      BaseCoefficient = input.BaseCoefficient,
      SellCoefficient = input.SellCoefficient
    });

    await dbContext.SaveChangesAsync(cancellationToken);
  }
}
