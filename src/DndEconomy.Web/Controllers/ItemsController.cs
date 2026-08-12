using DndEconomy.Application.Catalog;
using DndEconomy.Application.Items;
using DndEconomy.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace DndEconomy.Web.Controllers;

/// <summary>
/// API для поиска предметов каталога и создания новых предметов админом. Авторизация — те же
/// cookie Identity, что и у всего сайта: поиск не размечен [Authorize], поэтому подпадает под
/// общий FallbackPolicy (нужен только логин, как на любой странице каталога), создание —
/// отдельная политика с ролью Admin, как на странице AdminItemNew. [EnableCors] — доступ с
/// домена Foundry VTT (см. CorsPolicyNames.FoundryVtt) для макросов.
/// </summary>
[ApiController]
[Route("api/items")]
[EnableCors(CorsPolicyNames.FoundryVtt)]
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

  /// <summary>
  /// Ищет предмет по названию той же опечатко-устойчивой word_similarity-логикой, что и каталог
  /// (см. CatalogReadStore.BuildFilterPredicate), и возвращает лучшее совпадение — точное
  /// название и цену. 404, если совпадений выше порога нет или нет активной экономической сессии.
  /// </summary>
  [HttpGet("search")]
  public async Task<ActionResult<ItemSearchResponse>> Search([FromQuery] string name, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(name))
      return BadRequest("Параметр 'name' обязателен.");

    var query = new CatalogQuery
    {
      SearchTerm = name,
      SortOrder = CatalogSortOrder.Relevance,
      PageNumber = 1,
      PageSize = 1
    };

    var page = await _catalogQueryService.GetPageAsync(query, cancellationToken);
    var item = page.Items.FirstOrDefault();
    if (item is null)
      return NotFound();

    return Ok(new ItemSearchResponse
    {
      ItemId = item.ItemId,
      NameRu = item.NameRu,
      NameEn = item.NameEn,
      BuyPrice = item.BuyPrice,
      SellPrice = item.SellPrice,
      IsAvailable = item.IsAvailable
    });
  }

  /// <summary>Создаёт новый предмет каталога. Доступно только администраторам.</summary>
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
