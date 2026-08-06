namespace DndEconomy.Application.Items;

/// <summary>Поля для создания предмета — вручную админом или при одобрении заявки игрока.</summary>
public sealed record NewItemInput
{
  public required string Category { get; init; }
  public required string Type { get; init; }
  public required string Subtype { get; init; }
  public required string NameRu { get; init; }
  public string? NameEn { get; init; }
  public required decimal BaseCost { get; init; }
  public required decimal Weight { get; init; }
  public bool IsPlayerSuggested { get; init; }
}
