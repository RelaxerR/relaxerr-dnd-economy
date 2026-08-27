using DndEconomy.Application.Pricing;
using DndEconomy.Domain.Enums;

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

/// <summary>
/// Условие отбора предметов для глобального изменения стоимости — диапазон BaseCost.
/// Обе границы включительны; null означает отсутствие границы с этой стороны.
/// </summary>
public sealed record BulkPriceUpdateFilter
{
  public decimal? MinCost { get; init; }
  public decimal? MaxCost { get; init; }
}

/// <summary>Параметры массового изменения базовой стоимости: условие отбора + операция + значение.</summary>
public sealed record BulkPriceUpdateInput
{
  public required BulkPriceUpdateFilter Filter { get; init; }
  public required BulkPriceOperation Operation { get; init; }
  public required decimal Value { get; init; }
}

/// <summary>
/// Одна строка предпросмотра массового изменения стоимости — предмет, его текущая и
/// пересчитанная (но ещё не сохранённая) базовая стоимость, а также итоговые цены
/// покупки/продажи для активной экономической сессии до и после изменения (та же цена, что
/// увидит игрок в каталоге). Цены покупки/продажи — null, если активной сессии нет вообще
/// (расчёт невозможен); цена покупки дополнительно null, если товара нет в наличии
/// (см. <see cref="PriceFormulas.ResolveBuyPrice"/>).
/// </summary>
public sealed record BulkPriceUpdatePreviewRow
{
  public required Guid ItemId { get; init; }
  public required string NameRu { get; init; }
  public required string Type { get; init; }
  public required string Subtype { get; init; }
  public required decimal OldCost { get; init; }
  public required decimal NewCost { get; init; }
  public decimal? OldBuyPrice { get; init; }
  public decimal? NewBuyPrice { get; init; }
  public decimal? OldSellPrice { get; init; }
  public decimal? NewSellPrice { get; init; }
}
