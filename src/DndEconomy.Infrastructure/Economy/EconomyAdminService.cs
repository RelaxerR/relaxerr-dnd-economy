using DndEconomy.Application.Economy;
using DndEconomy.Domain.Entities;
using DndEconomy.Domain.Enums;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndEconomy.Infrastructure.Economy;

/// <inheritdoc cref="IEconomyAdminService" />
public sealed class EconomyAdminService : IEconomyAdminService
{
  private readonly ApplicationDbContext _dbContext;

  public EconomyAdminService(ApplicationDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<CitySummary>> GetCitiesAsync(CancellationToken cancellationToken)
    => await _dbContext.Cities.AsNoTracking()
      .OrderBy(x => x.Name)
      .Select(x => new CitySummary { Id = x.Id, Name = x.Name, Size = x.Size })
      .ToListAsync(cancellationToken);

  /// <inheritdoc />
  public async Task<Guid> CreateCityAsync(string name, CitySize size, CancellationToken cancellationToken)
  {
    var city = new City { Name = name, Size = size };
    _dbContext.Cities.Add(city);
    await _dbContext.SaveChangesAsync(cancellationToken);
    return city.Id;
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<EconomySessionSummary>> GetSessionsAsync(CancellationToken cancellationToken)
    => await _dbContext.EconomySessions.AsNoTracking()
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

  /// <inheritdoc />
  public async Task CreateSessionAsync(NewEconomySessionInput input, CancellationToken cancellationToken)
  {
    _dbContext.EconomySessions.Add(new EconomySession
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

    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
