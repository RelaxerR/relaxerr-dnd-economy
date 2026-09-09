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

  /// <summary>
  /// True для нефизических услуг (например, "Обмен валют") — см. Item.IsService. Комиссия по
  /// городу для такой записи задаётся через уже существующую матрицу CityModifier
  /// (/admin/economy/city-modifiers) для Type+Subtype этого предмета.
  /// </summary>
  public bool IsService { get; init; }
}

/// <summary>Те же поля, что и <see cref="NewItemInput"/> — для редактирования уже существующего предмета.</summary>
public sealed record UpdateItemInput
{
  public required string Category { get; init; }
  public required string Type { get; init; }
  public required string Subtype { get; init; }
  public required string NameRu { get; init; }
  public string? NameEn { get; init; }
  public required decimal BaseCost { get; init; }
  public required decimal Weight { get; init; }
  public bool IsPlayerSuggested { get; init; }
  public bool IsService { get; init; }
}

/// <summary>Параметры поиска для вкладки "Предметы" в админке: строка поиска + пагинация.</summary>
public sealed record ItemAdminQuery
{
  public string? SearchTerm { get; init; }
  public int PageNumber { get; init; } = 1;
  public int PageSize { get; init; } = 20;
}

/// <summary>Одна строка списка предметов в админке — сырые поля Item, без расчёта цены (та зависит от сессии, здесь не нужна).</summary>
public sealed record ItemAdminRow
{
  public required Guid ItemId { get; init; }
  public required string Category { get; init; }
  public required string Type { get; init; }
  public required string Subtype { get; init; }
  public required string NameRu { get; init; }
  public string? NameEn { get; init; }
  public required decimal BaseCost { get; init; }
  public required decimal Weight { get; init; }
  public string? ExternalUuid { get; init; }
  public required bool IsPlayerSuggested { get; init; }
  public required bool IsService { get; init; }
}

/// <summary>Страница результатов поиска предметов для админки.</summary>
public sealed record ItemAdminPage
{
  public required IReadOnlyList<ItemAdminRow> Items { get; init; }
  public required int TotalCount { get; init; }
  public required int PageNumber { get; init; }
  public required int PageSize { get; init; }
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
