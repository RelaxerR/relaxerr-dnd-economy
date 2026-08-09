using DndEconomy.Application.Pricing;
using DndEconomy.Application.Profile;
using DndEconomy.Domain.Entities;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndEconomy.Infrastructure.Profile;

/// <inheritdoc cref="IPlayerProfileService" />
public sealed class PlayerProfileService : IPlayerProfileService
{
  private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
  private readonly IPriceCalculationService _priceCalculationService;

  public PlayerProfileService(IDbContextFactory<ApplicationDbContext> dbContextFactory, IPriceCalculationService priceCalculationService)
  {
    _dbContextFactory = dbContextFactory;
    _priceCalculationService = priceCalculationService;
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<FavoriteItemViewModel>> GetFavoritesAsync(Guid userId, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    var saved = await dbContext.UserSavedItems.AsNoTracking()
      .Where(x => x.UserId == userId)
      .Include(x => x.Item)
      .OrderByDescending(x => x.CreatedAtUtc)
      .ToListAsync(cancellationToken);

    var result = new List<FavoriteItemViewModel>(saved.Count);
    foreach (var savedItem in saved)
    {
      if (savedItem.Item is null)
        continue;

      var price = await _priceCalculationService.CalculateCurrentPriceAsync(savedItem.ItemId, cancellationToken);
      result.Add(new FavoriteItemViewModel
      {
        ItemId = savedItem.ItemId,
        NameRu = savedItem.Item.NameRu,
        NameEn = savedItem.Item.NameEn,
        Category = savedItem.Item.Category,
        Type = savedItem.Item.Type,
        Subtype = savedItem.Item.Subtype,
        Note = savedItem.Note,
        BuyPrice = price?.BuyPrice,
        SellPrice = price?.SellPrice
      });
    }

    return result;
  }

  /// <inheritdoc />
  public async Task<bool> IsFavoriteAsync(Guid userId, Guid itemId, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    return await dbContext.UserSavedItems.AsNoTracking().AnyAsync(x => x.UserId == userId && x.ItemId == itemId, cancellationToken);
  }

  /// <inheritdoc />
  public async Task AddFavoriteAsync(Guid userId, Guid itemId, string? note, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    var exists = await dbContext.UserSavedItems.AnyAsync(x => x.UserId == userId && x.ItemId == itemId, cancellationToken);
    if (exists)
      return;

    dbContext.UserSavedItems.Add(new UserSavedItem { UserId = userId, ItemId = itemId, Note = note });
    await dbContext.SaveChangesAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task RemoveFavoriteAsync(Guid userId, Guid itemId, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    var saved = await dbContext.UserSavedItems.SingleOrDefaultAsync(x => x.UserId == userId && x.ItemId == itemId, cancellationToken);
    if (saved is null)
      return;

    dbContext.UserSavedItems.Remove(saved);
    await dbContext.SaveChangesAsync(cancellationToken);
  }
}
