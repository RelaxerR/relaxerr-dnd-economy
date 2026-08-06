namespace DndEconomy.Application.Catalog;

/// <summary>Параметры страницы каталога: фильтры, поиск, сортировка, пагинация.</summary>
public sealed record CatalogQuery
{
  public string? SearchTerm { get; init; }
  public string? Category { get; init; }
  public string? Type { get; init; }
  public string? Subtype { get; init; }
  public bool? OnlyAvailable { get; init; }
  public CatalogSortOrder SortOrder { get; init; } = CatalogSortOrder.NameAsc;
  public int PageNumber { get; init; } = 1;
  public int PageSize { get; init; } = 24;
}

/// <summary>Порядок сортировки каталога.</summary>
public enum CatalogSortOrder
{
  NameAsc,
  NameDesc,
  PriceAsc,
  PriceDesc,
  Relevance
}

/// <summary>
/// Строка результата запроса к БД: поля отображения плюс уже посчитанная в SQL "сырая"
/// стоимость (BaseCost × коэффициенты активной сессии/города/сезона) — считается один раз
/// в CatalogReadStore через LEFT JOIN, а не через отдельный запрос на каждый предмет.
/// </summary>
public sealed class CatalogPricedRow
{
  public required Guid ItemId { get; init; }
  public required string NameRu { get; init; }
  public string? NameEn { get; init; }
  public required string Category { get; init; }
  public required string Type { get; init; }
  public required string Subtype { get; init; }
  public required decimal Weight { get; init; }
  public required decimal BaseCost { get; init; }
  public required bool IsPlayerSuggested { get; init; }
  public required decimal CalculatedCost { get; init; }
}

/// <summary>Предмет каталога с уже посчитанными ценами покупки/продажи для UI.</summary>
public sealed class CatalogItemViewModel
{
  public required Guid ItemId { get; init; }
  public required string NameRu { get; init; }
  public string? NameEn { get; init; }
  public required string Category { get; init; }
  public required string Type { get; init; }
  public required string Subtype { get; init; }
  public required decimal Weight { get; init; }
  public required bool IsPlayerSuggested { get; init; }
  public decimal? BuyPrice { get; init; }
  public required decimal SellPrice { get; init; }

  public bool IsAvailable => BuyPrice is not null;
}

/// <summary>Страница результатов каталога вместе с контекстом активной сессии (для отображения "цены на дату").</summary>
public sealed class CatalogPage
{
  public required IReadOnlyList<CatalogItemViewModel> Items { get; init; }
  public required int TotalCount { get; init; }
  public required int PageNumber { get; init; }
  public required int PageSize { get; init; }
  public required string ActiveSessionName { get; init; }
  public required string CityName { get; init; }

  public static CatalogPage Empty(int pageNumber, int pageSize) => new()
  {
    Items = [],
    TotalCount = 0,
    PageNumber = pageNumber,
    PageSize = pageSize,
    ActiveSessionName = string.Empty,
    CityName = string.Empty
  };
}
