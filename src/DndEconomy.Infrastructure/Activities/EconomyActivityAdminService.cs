using DndEconomy.Application.Activities;
using DndEconomy.Domain.Entities;
using DndEconomy.Domain.Enums;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndEconomy.Infrastructure.Activities;

/// <inheritdoc cref="IEconomyActivityAdminService" />
public sealed class EconomyActivityAdminService : IEconomyActivityAdminService
{
  #region Поля и конструктор

  private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

  public EconomyActivityAdminService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
  {
    _dbContextFactory = dbContextFactory;
  }

  #endregion

  #region Публичные методы

  /// <inheritdoc />
  public async Task<IReadOnlyList<EconomyActivitySummary>> GetAllAsync(CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    return await dbContext.EconomyActivities.AsNoTracking()
      .OrderBy(x => x.ActivityType).ThenBy(x => x.MinLevel).ThenBy(x => x.Category).ThenBy(x => x.DangerLevel)
      .Select(x => new EconomyActivitySummary
      {
        Id = x.Id,
        ActivityType = x.ActivityType,
        Category = x.Category,
        DangerLevel = x.DangerLevel,
        MinLevel = x.MinLevel,
        MaxLevel = x.MaxLevel,
        RateMin = x.RateMin,
        RateMax = x.RateMax,
        RecommendedDurationDays = x.RecommendedDurationDays,
        Description = x.Description,
        BalanceNote = x.BalanceNote
      })
      .ToListAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task<Guid> CreateAsync(NewEconomyActivityInput input, CancellationToken cancellationToken)
  {
    EnsureValid(input);
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    var activity = new EconomyActivity();
    Apply(activity, input);

    dbContext.EconomyActivities.Add(activity);
    await dbContext.SaveChangesAsync(cancellationToken);
    return activity.Id;
  }

  /// <inheritdoc />
  public async Task UpdateAsync(Guid id, NewEconomyActivityInput input, CancellationToken cancellationToken)
  {
    EnsureValid(input);
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    var activity = await dbContext.EconomyActivities.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (activity is null)
      return;

    Apply(activity, input);
    activity.UpdatedAtUtc = DateTime.UtcNow;

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    var activity = await dbContext.EconomyActivities.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (activity is null)
      return;

    dbContext.EconomyActivities.Remove(activity);
    await dbContext.SaveChangesAsync(cancellationToken);
  }

  #endregion

  #region Приватные шаги

  private static void EnsureValid(NewEconomyActivityInput input)
  {
    var error = EconomyActivityValidator.Validate(input);
    if (error is not null)
      throw new ArgumentException(error);
  }

  /// <summary>Переносит ввод в сущность; у простоя опасность всегда сбрасывается в null.</summary>
  private static void Apply(EconomyActivity activity, NewEconomyActivityInput input)
  {
    activity.ActivityType = input.ActivityType;
    activity.Category = input.Category.Trim();
    activity.DangerLevel = input.ActivityType == EconomyActivityType.Quest ? input.DangerLevel : null;
    activity.MinLevel = input.MinLevel;
    activity.MaxLevel = input.MaxLevel;
    activity.RateMin = input.RateMin;
    activity.RateMax = input.RateMax;
    activity.RecommendedDurationDays = input.RecommendedDurationDays;
    activity.Description = input.Description;
    activity.BalanceNote = string.IsNullOrWhiteSpace(input.BalanceNote) ? null : input.BalanceNote;
  }

  #endregion
}
