using DndEconomy.Application.Catalog;
using DndEconomy.Application.Pricing;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndEconomy.Infrastructure.Catalog;

/// <summary>
/// EF Core-реализация <see cref="ICatalogReadStore"/>. Цена считается прямо в SQL через
/// LEFT JOIN на модификаторы активной сессии — на всю страницу каталога уходит один запрос
/// на подсчёт (Count) и один на выборку строк, независимо от числа предметов на странице.
/// </summary>
public sealed class CatalogReadStore : ICatalogReadStore
{
  #region Поля и конструктор

  private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

  public CatalogReadStore(IDbContextFactory<ApplicationDbContext> dbContextFactory)
  {
    _dbContextFactory = dbContextFactory;
  }

  #endregion

  #region Публичные методы

  /// <inheritdoc />
  public async Task<(IReadOnlyList<CatalogPricedRow> Rows, int TotalCount)> GetPageAsync(
    CatalogQuery query, ActiveSessionContext session, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    var priced = BuildPricedQuery(dbContext, session)
      .Where(BuildFilterPredicate(query));

    if (query.OnlyAvailable == true)
      priced = priced.Where(x => x.CalculatedCost > 0);

    var totalCount = await priced.CountAsync(cancellationToken);

    var term = query.SearchTerm?.Trim();
    var hasSearch = !string.IsNullOrEmpty(term);

    priced = (query.SortOrder, hasSearch) switch
    {
      (CatalogSortOrder.PriceAsc, _) => priced.OrderBy(x => x.CalculatedCost),
      (CatalogSortOrder.PriceDesc, _) => priced.OrderByDescending(x => x.CalculatedCost),
      (CatalogSortOrder.NameDesc, _) => priced.OrderByDescending(x => x.NameRu),
      (CatalogSortOrder.Relevance, true) => priced.OrderByDescending(x => EF.Functions.TrigramsWordSimilarity(term!, x.NameRu)),
      _ => priced.OrderBy(x => x.NameRu)
    };

    var rows = await priced
      .Skip((query.PageNumber - 1) * query.PageSize)
      .Take(query.PageSize)
      .ToListAsync(cancellationToken);

    return (rows, totalCount);
  }

  /// <inheritdoc />
  public async Task<CatalogPricedRow?> GetItemAsync(Guid itemId, ActiveSessionContext session, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    return await BuildPricedQuery(dbContext, session)
      .Where(x => x.ItemId == itemId)
      .SingleOrDefaultAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<string>> GetDistinctCategoriesAsync(CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    return await dbContext.Items.AsNoTracking()
      .Select(x => x.Category).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<string>> GetDistinctTypesAsync(string? category, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    var items = dbContext.Items.AsNoTracking();
    if (!string.IsNullOrWhiteSpace(category))
      items = items.Where(x => x.Category == category);

    return await items.Select(x => x.Type).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<string>> GetDistinctSubtypesAsync(string? type, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    var items = dbContext.Items.AsNoTracking();
    if (!string.IsNullOrWhiteSpace(type))
      items = items.Where(x => x.Type == type);

    return await items.Select(x => x.Subtype).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);
  }

  #endregion

  #region Приватные шаги

  /// <summary>
  /// Порог word_similarity для поиска — задан явно в коде, а не через GUC
  /// pg_trgm.word_similarity_threshold (умалчиваемое значение Postgres — 0.6). Причина: 0.6
  /// не проходит частые однобуквенные опечатки в коротких словах (например, "кольчюга" →
  /// "Кольчуга" даёт 0.556), а GUC — это настройка уровня БД/сессии, легко забыть выставить
  /// на сервере при деплое. Побочный эффект: сравнение через функцию word_similarity() в WHERE
  /// (а не оператор `&lt;%`) не использует GIN-индекс gin_trgm_ops — на масштабе каталога одной
  /// кампании (сотни предметов) это не проблема, полный скан word_similarity по всем строкам
  /// занимает доли миллисекунды.
  /// </summary>
  private const double WordSimilarityThreshold = 0.4;

  /// <summary>
  /// LEFT JOIN на CityModifiers (по городу сессии) и SeasonModifiers (по сезону сессии) —
  /// коэффициент 1m, если для (Type, Subtype) модификатора нет, как и в EconomyPricingReadStore.
  /// </summary>
  private static IQueryable<CatalogPricedRow> BuildPricedQuery(ApplicationDbContext dbContext, ActiveSessionContext session)
  {
    var cityModifiers = dbContext.CityModifiers.Where(cm => cm.CityId == session.CityId);
    var seasonModifiers = dbContext.SeasonModifiers.Where(sm => sm.Season == session.Season);

    return
      from item in dbContext.Items.AsNoTracking()
      join cityMod in cityModifiers
        on new { item.Type, item.Subtype } equals new { cityMod.Type, cityMod.Subtype } into cityModsJoin
      from cityMod in cityModsJoin.DefaultIfEmpty()
      join seasonMod in seasonModifiers
        on new { item.Type, item.Subtype } equals new { seasonMod.Type, seasonMod.Subtype } into seasonModsJoin
      from seasonMod in seasonModsJoin.DefaultIfEmpty()
      select new CatalogPricedRow
      {
        ItemId = item.Id,
        NameRu = item.NameRu,
        NameEn = item.NameEn,
        Category = item.Category,
        Type = item.Type,
        Subtype = item.Subtype,
        Weight = item.Weight,
        BaseCost = item.BaseCost,
        IsPlayerSuggested = item.IsPlayerSuggested,
        CalculatedCost = item.BaseCost * session.BaseCoefficient
          * (cityMod != null ? cityMod.Coefficient : 1m)
          * (seasonMod != null ? seasonMod.Coefficient : 1m)
      };
  }

  /// <summary>
  /// Поиск через word_similarity, а не обычный similarity: имена предметов почти всегда
  /// многословные ("Длинный меч"), и обычная триграммная схожесть по ВСЕЙ строке резко падает
  /// для короткого запроса ("мечь" даёт similarity 0.21 — ниже порога 0.3). word_similarity
  /// ищет схожесть с лучшим непрерывным фрагментом строки, поэтому "мечь" находит "Длинный меч"
  /// (word_similarity 0.6). Порог сравнения — <see cref="WordSimilarityThreshold"/>.
  /// </summary>
  private static System.Linq.Expressions.Expression<Func<CatalogPricedRow, bool>> BuildFilterPredicate(CatalogQuery query)
  {
    var term = query.SearchTerm?.Trim();
    var hasSearch = !string.IsNullOrEmpty(term);

    return x =>
      (string.IsNullOrWhiteSpace(query.Category) || x.Category == query.Category) &&
      (string.IsNullOrWhiteSpace(query.Type) || x.Type == query.Type) &&
      (string.IsNullOrWhiteSpace(query.Subtype) || x.Subtype == query.Subtype) &&
      (!hasSearch ||
        EF.Functions.TrigramsWordSimilarity(term!, x.NameRu) >= WordSimilarityThreshold ||
        (x.NameEn != null && EF.Functions.TrigramsWordSimilarity(term!, x.NameEn) >= WordSimilarityThreshold));
  }

  #endregion
}
