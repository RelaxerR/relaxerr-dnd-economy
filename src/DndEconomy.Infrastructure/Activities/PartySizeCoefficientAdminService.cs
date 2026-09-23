using DndEconomy.Application.Activities;
using DndEconomy.Domain.Entities;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndEconomy.Infrastructure.Activities;

/// <inheritdoc cref="IPartySizeCoefficientAdminService" />
public sealed class PartySizeCoefficientAdminService : IPartySizeCoefficientAdminService
{
  #region Поля и конструктор

  private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

  public PartySizeCoefficientAdminService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
  {
    _dbContextFactory = dbContextFactory;
  }

  #endregion

  #region Публичные методы

  /// <inheritdoc />
  public async Task<IReadOnlyList<PartySizeCoefficientSummary>> GetAllAsync(CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    return await dbContext.PartySizeCoefficients.AsNoTracking()
      .OrderBy(x => x.PartySize)
      .Select(x => new PartySizeCoefficientSummary { PartySize = x.PartySize, Coefficient = x.Coefficient })
      .ToListAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task SetAsync(int partySize, decimal coefficient, CancellationToken cancellationToken)
  {
    var error = PartySizeCoefficientValidator.Validate(partySize, coefficient);
    if (error is not null)
      throw new ArgumentException(error);

    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    var existing = await dbContext.PartySizeCoefficients.SingleOrDefaultAsync(x => x.PartySize == partySize, cancellationToken);
    if (existing is null)
    {
      dbContext.PartySizeCoefficients.Add(new PartySizeCoefficient { PartySize = partySize, Coefficient = coefficient });
    }
    else
    {
      existing.Coefficient = coefficient;
      existing.UpdatedAtUtc = DateTime.UtcNow;
    }

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task DeleteAsync(int partySize, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    await dbContext.PartySizeCoefficients.Where(x => x.PartySize == partySize).ExecuteDeleteAsync(cancellationToken);
  }

  #endregion
}
