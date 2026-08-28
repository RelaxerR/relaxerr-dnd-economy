using DndEconomy.Application.Import;
using DndEconomy.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DndEconomy.Web.Controllers;

/// <summary>
/// Отдаёт текущий справочник предметов в виде .xlsx — обычная HTTP-ссылка (не Blazor-компонент),
/// потому что скачивание файла из Blazor Server иначе потребовало бы JS interop; браузер и так
/// умеет сохранять файл по прямому GET, а cookie-авторизация Identity действует и здесь.
/// </summary>
[ApiController]
[Route("api/admin/items")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class ItemsExportController : ControllerBase
{
  #region Поля и конструктор

  private readonly IExcelItemExportService _exportService;

  public ItemsExportController(IExcelItemExportService exportService)
  {
    _exportService = exportService;
  }

  #endregion

  #region Публичные методы

  /// <summary>Строит и отдаёт .xlsx с текущим списком предметов (лист "Предметы", формат — как у шаблона импорта).</summary>
  [HttpGet("export")]
  public async Task<IActionResult> Export(CancellationToken cancellationToken)
  {
    var content = await _exportService.ExportItemsAsync(cancellationToken);
    var fileName = $"predmety-{DateTime.UtcNow:yyyy-MM-dd}.xlsx";

    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
  }

  #endregion
}
