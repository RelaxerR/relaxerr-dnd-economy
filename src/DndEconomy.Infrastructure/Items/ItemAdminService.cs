using DndEconomy.Application.Items;
using DndEconomy.Domain.Entities;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndEconomy.Infrastructure.Items;

/// <inheritdoc cref="IItemAdminService" />
public sealed class ItemAdminService : IItemAdminService
{
  private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

  public ItemAdminService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
  {
    _dbContextFactory = dbContextFactory;
  }

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
}
