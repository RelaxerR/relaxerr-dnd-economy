using DndEconomy.Application.QuestPay;
using DndEconomy.Domain.Entities;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndEconomy.Infrastructure.QuestPay;

/// <inheritdoc cref="IQuestPayRateAdminService" />
public sealed class QuestPayRateAdminService : IQuestPayRateAdminService
{
  private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

  public QuestPayRateAdminService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
  {
    _dbContextFactory = dbContextFactory;
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<QuestPayRateSummary>> GetAllAsync(CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    return await dbContext.QuestPayRates.AsNoTracking()
      .OrderBy(x => x.Epoch).ThenBy(x => x.Category).ThenBy(x => x.DangerLevel)
      .Select(x => new QuestPayRateSummary
      {
        Id = x.Id,
        Epoch = x.Epoch,
        Category = x.Category,
        DangerLevel = x.DangerLevel,
        Description = x.Description,
        Duration = x.Duration,
        PartyPayment = x.PartyPayment,
        BalanceNote = x.BalanceNote
      })
      .ToListAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task<Guid> CreateAsync(NewQuestPayRateInput input, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    var rate = new QuestPayRate
    {
      Epoch = input.Epoch,
      Category = input.Category,
      DangerLevel = input.DangerLevel,
      Description = input.Description,
      Duration = input.Duration,
      PartyPayment = input.PartyPayment,
      BalanceNote = input.BalanceNote ?? string.Empty
    };

    dbContext.QuestPayRates.Add(rate);
    await dbContext.SaveChangesAsync(cancellationToken);
    return rate.Id;
  }

  /// <inheritdoc />
  public async Task UpdateAsync(Guid id, NewQuestPayRateInput input, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    var rate = await dbContext.QuestPayRates.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (rate is null)
      return;

    rate.Epoch = input.Epoch;
    rate.Category = input.Category;
    rate.DangerLevel = input.DangerLevel;
    rate.Description = input.Description;
    rate.Duration = input.Duration;
    rate.PartyPayment = input.PartyPayment;
    rate.BalanceNote = input.BalanceNote ?? string.Empty;
    rate.UpdatedAtUtc = DateTime.UtcNow;

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    var rate = await dbContext.QuestPayRates.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (rate is null)
      return;

    dbContext.QuestPayRates.Remove(rate);
    await dbContext.SaveChangesAsync(cancellationToken);
  }
}
