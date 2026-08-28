using DndEconomy.Application.Catalog;
using DndEconomy.Application.Items;
using DndEconomy.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace DndEconomy.Web.Controllers;

/// <summary>
/// API для поиска предметов каталога и создания новых предметов — оба эндпоинта только для
/// макросов Foundry VTT, авторизация схемой MacroApiKey (см. ApiKeyAuthenticationHandler), НЕ
/// cookie Identity: macros получают статичный ключ, а не логинятся, поэтому не трогают auth-cookie
/// браузера и не выбивают пользователя из уже открытой вкладки сайта (см. CLAUDE.md). Поиск
/// принимает ключ игрока или админа, создание — только ключ админа (роль Admin на принципале
/// от ApiKeyAuthenticationHandler). [EnableCors] — доступ с домена Foundry VTT
/// (см. CorsPolicyNames.FoundryVtt).
/// </summary>
[ApiController]
[Route("api/items")]
[EnableCors(CorsPolicyNames.FoundryVtt)]
[Authorize(AuthenticationSchemes = AuthenticationSchemeNames.MacroApiKey)]
public sealed class ItemsController : ControllerBase
{
  #region Поля и конструктор

  private readonly ICatalogQueryService _catalogQueryService;
  private readonly IItemAdminService _itemAdminService;

  public ItemsController(ICatalogQueryService catalogQueryService, IItemAdminService itemAdminService)
  {
    _catalogQueryService = catalogQueryService;
    _itemAdminService = itemAdminService;
  }

  #endregion

  #region Публичные методы

  /// <summary>Верхняя граница для <c>take</c> — не даёт запросить сразу весь каталог одним поиском.</summary>
  private const int MaxTake = 20;

  /// <summary>
  /// Ищет предмет по названию той же опечатко-устойчивой word_similarity-логикой, что и каталог
  /// (см. CatalogReadStore.BuildFilterPredicate), и возвращает до <paramref name="take"/>
  /// лучших совпадений (по умолчанию 5), отсортированных по релевантности. Пустой список,
  /// если совпадений выше порога нет или нет активной экономической сессии.
  /// </summary>
  [HttpGet("search")]
  public async Task<ActionResult<IReadOnlyList<ItemSearchResponse>>> Search(
    [FromQuery] string name, [FromQuery] int take = 5, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(name))
      return BadRequest("Параметр 'name' обязателен.");

    var query = new CatalogQuery
    {
      SearchTerm = name,
      SortOrder = CatalogSortOrder.Relevance,
      PageNumber = 1,
      PageSize = Math.Clamp(take, 1, MaxTake)
    };

    var page = await _catalogQueryService.GetPageAsync(query, cancellationToken);

    var results = page.Items.Select(item => new ItemSearchResponse
    {
      ItemId = item.ItemId,
      NameRu = item.NameRu,
      NameEn = item.NameEn,
      BuyPrice = item.BuyPrice,
      SellPrice = item.SellPrice,
      IsAvailable = item.IsAvailable
    }).ToList();

    return Ok(results);
  }

  /// <summary>Создаёт новый предмет каталога. Требует ключ админа (AdminKey).</summary>
  [HttpPost]
  [Authorize(Roles = RoleNames.Admin)]
  public async Task<ActionResult<ItemCreatedResponse>> Create([FromBody] CreateItemRequest request, CancellationToken cancellationToken)
  {
    var itemId = await _itemAdminService.CreateItemAsync(new NewItemInput
    {
      Category = request.Category,
      Type = request.Type,
      Subtype = request.Subtype,
      NameRu = request.NameRu,
      NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn,
      BaseCost = request.BaseCost,
      Weight = request.Weight,
      IsPlayerSuggested = false
    }, cancellationToken);

    return CreatedAtAction(nameof(Search), new { name = request.NameRu }, new ItemCreatedResponse { ItemId = itemId });
  }

  #endregion
}
