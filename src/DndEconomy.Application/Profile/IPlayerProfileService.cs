namespace DndEconomy.Application.Profile;

/// <summary>Избранные предметы игрока.</summary>
public interface IPlayerProfileService
{
  Task<IReadOnlyList<FavoriteItemViewModel>> GetFavoritesAsync(Guid userId, CancellationToken cancellationToken);

  Task<bool> IsFavoriteAsync(Guid userId, Guid itemId, CancellationToken cancellationToken);

  Task AddFavoriteAsync(Guid userId, Guid itemId, string? note, CancellationToken cancellationToken);

  Task RemoveFavoriteAsync(Guid userId, Guid itemId, CancellationToken cancellationToken);
}
