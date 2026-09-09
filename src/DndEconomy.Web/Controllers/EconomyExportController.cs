using DndEconomy.Application.Import;
using DndEconomy.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DndEconomy.Web.Controllers;

/// <summary>
/// Отдаёт текущее состояние городов/сессий/коэффициентов/приёма монет в виде .xlsx — по
/// одному листу на эндпоинт, тот же паттерн, что <see cref="ItemsExportController"/>
/// (обычная HTTP-ссылка, не Blazor-компонент — браузер сам сохраняет файл по GET, cookie-
/// авторизация Identity действует и здесь).
/// </summary>
[ApiController]
[Route("api/admin/economy")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class EconomyExportController : ControllerBase
{
  #region Поля и конструктор

  private readonly IExcelEconomyExportService _exportService;

  public EconomyExportController(IExcelEconomyExportService exportService)
  {
    _exportService = exportService;
  }

  #endregion

  #region Публичные методы

  /// <summary>Лист "Города" — список городов и матрица коэффициентов "Тип+Подтип × Город".</summary>
  [HttpGet("cities/export")]
  public async Task<IActionResult> ExportCities(CancellationToken cancellationToken)
  {
    var content = await _exportService.ExportCitiesAsync(cancellationToken);
    return AsXlsx(content, "goroda");
  }

  /// <summary>Лист "Сезонность" — матрица коэффициентов "Тип+Подтип × Сезон".</summary>
  [HttpGet("season-modifiers/export")]
  public async Task<IActionResult> ExportSeasonModifiers(CancellationToken cancellationToken)
  {
    var content = await _exportService.ExportSeasonModifiersAsync(cancellationToken);
    return AsXlsx(content, "sezonnost");
  }

  /// <summary>Лист "Настройки" — список игровых сессий ("Партий").</summary>
  [HttpGet("sessions/export")]
  public async Task<IActionResult> ExportSessions(CancellationToken cancellationToken)
  {
    var content = await _exportService.ExportSessionsAsync(cancellationToken);
    return AsXlsx(content, "sessii");
  }

  /// <summary>Лист "Приём монет" — матрица приёма номиналов и заметки по городам.</summary>
  [HttpGet("coin-acceptance/export")]
  public async Task<IActionResult> ExportCoinAcceptance(CancellationToken cancellationToken)
  {
    var content = await _exportService.ExportCoinAcceptanceAsync(cancellationToken);
    return AsXlsx(content, "priem-monet");
  }

  /// <summary>Лист "Оплата заданий" — справочник оплаты заданий мастера.</summary>
  [HttpGet("quest-pay/export")]
  public async Task<IActionResult> ExportQuestPayRates(CancellationToken cancellationToken)
  {
    var content = await _exportService.ExportQuestPayRatesAsync(cancellationToken);
    return AsXlsx(content, "oplata-zadaniy");
  }

  #endregion

  #region Приватные шаги

  private FileContentResult AsXlsx(byte[] content, string fileNamePrefix)
  {
    var fileName = $"{fileNamePrefix}-{DateTime.UtcNow:yyyy-MM-dd}.xlsx";
    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
  }

  #endregion
}
