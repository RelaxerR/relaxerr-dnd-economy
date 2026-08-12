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
        Description = x.Description,
        RealDate = x.RealDate,
        GameDateLabel = x.GameDateLabel,
        CityId = x.CityId,
        CityName = x.City != null ? x.City.Name : null,
        Season = x.Season,
        BaseCoefficient = x.BaseCoefficient,
        SellCoefficient = x.SellCoefficient,
        IsPinnedForDisplay = x.IsPinnedForDisplay
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

  /// <inheritdoc />
  public async Task UpdateSessionAsync(Guid sessionId, NewEconomySessionInput input, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    var session = await dbContext.EconomySessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
    if (session is null)
      return;

    session.Name = input.Name;
    session.Description = input.Description;
    session.RealDate = input.RealDate;
    session.GameDateLabel = input.GameDateLabel;
    session.CityId = input.CityId;
    session.Season = input.Season;
    session.BaseCoefficient = input.BaseCoefficient;
    session.SellCoefficient = input.SellCoefficient;

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    var session = await dbContext.EconomySessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
    if (session is null)
      return;

    dbContext.EconomySessions.Remove(session);
    await dbContext.SaveChangesAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task SetDisplaySessionOverrideAsync(Guid? sessionId, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    // Снимаем закрепление со всех сессий разом (частичный уникальный индекс допускает
    // только одну закреплённую запись), затем закрепляем выбранную, если она указана.
    await dbContext.EconomySessions
      .Where(x => x.IsPinnedForDisplay)
      .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsPinnedForDisplay, false), cancellationToken);

    if (sessionId is null)
      return;

    await dbContext.EconomySessions
      .Where(x => x.Id == sessionId.Value)
      .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsPinnedForDisplay, true), cancellationToken);
  }
}
