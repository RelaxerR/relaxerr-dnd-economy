using System.ComponentModel.DataAnnotations;

namespace DndEconomy.Web.Controllers;

/// <summary>Результат поиска предмета по названию — точное имя лучшего совпадения и его цена.</summary>
public sealed class ItemSearchResponse
{
  public required Guid ItemId { get; init; }
  public required string NameRu { get; init; }
  public string? NameEn { get; init; }

  /// <summary>Цена покупки; null означает "Нет в наличии" (см. PriceFormulas.ResolveBuyPrice).</summary>
  public decimal? BuyPrice { get; init; }

  public required decimal SellPrice { get; init; }
  public required bool IsAvailable { get; init; }
}

/// <summary>Поля для создания предмета через API — те же, что и в форме админки (AdminItemNew).</summary>
public sealed class CreateItemRequest
{
  [Required] public string Category { get; init; } = string.Empty;
  [Required] public string Type { get; init; } = string.Empty;
  [Required] public string Subtype { get; init; } = string.Empty;
  [Required] public string NameRu { get; init; } = string.Empty;
  public string? NameEn { get; init; }
  public decimal BaseCost { get; init; }
  public decimal Weight { get; init; }
}

/// <summary>Ответ на успешное создание предмета.</summary>
public sealed class ItemCreatedResponse
{
  public required Guid ItemId { get; init; }
}
