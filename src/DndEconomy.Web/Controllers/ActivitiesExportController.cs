using DndEconomy.Application.Import;
using DndEconomy.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DndEconomy.Web.Controllers;

/// <summary>
/// Отдаёт справочник экономической активности в виде .xlsx — своя точка входа (не
/// <see cref="EconomyExportController"/>), потому что страница `/admin/activities` — отдельная
/// вкладка рядом с каталогом, а не часть раздела «Города и сессии». Тот же паттерн, что
/// <see cref="ItemsExportController"/>: обычная HTTP-ссылка, cookie-авторизация Identity
/// действует и здесь.
/// </summary>
[ApiController]
[Route("api/admin/activities")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class ActivitiesExportController : ControllerBase
{
  #region Поля и конструктор

  private readonly IExcelEconomyExportService _exportService;

  public ActivitiesExportController(IExcelEconomyExportService exportService)
  {
    _exportService = exportService;
  }

  #endregion

  #region Публичные методы

  /// <summary>Лист "Экономическая активность" — ставки заданий и занятий в простое.</summary>
  [HttpGet("export")]
  public async Task<IActionResult> Export(CancellationToken cancellationToken)
  {
    var content = await _exportService.ExportEconomyActivitiesAsync(cancellationToken);
    var fileName = $"ekonomicheskaya-aktivnost-{DateTime.UtcNow:yyyy-MM-dd}.xlsx";

    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
  }

  #endregion
}
