using DndEconomy.Application.Items;
using DndEconomy.Domain.Entities;
using DndEconomy.Infrastructure.Persistence;

namespace DndEconomy.Infrastructure.Items;

/// <inheritdoc cref="IItemAdminService" />
public sealed class ItemAdminService : IItemAdminService
{
  private readonly ApplicationDbContext _dbContext;

  public ItemAdminService(ApplicationDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  /// <inheritdoc />
  public async Task<Guid> CreateItemAsync(NewItemInput input, CancellationToken cancellationToken)
  {
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

    _dbContext.Items.Add(item);
    await _dbContext.SaveChangesAsync(cancellationToken);
    return item.Id;
  }
}
