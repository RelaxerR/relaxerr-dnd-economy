namespace DndEconomy.Application.Profile;

/// <summary>Предмет из избранного игрока вместе с текущей ценой.</summary>
public sealed class FavoriteItemViewModel
{
  public required Guid ItemId { get; init; }
  public required string NameRu { get; init; }
  public string? NameEn { get; init; }
  public required string Category { get; init; }
  public required string Type { get; init; }
  public required string Subtype { get; init; }
  public string? Note { get; init; }
  public decimal? BuyPrice { get; init; }
  public decimal? SellPrice { get; init; }
}
