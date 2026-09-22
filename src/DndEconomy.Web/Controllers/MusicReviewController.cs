using DndEconomy.Domain.Constants;
using DndEconomy.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DndEconomy.Web.Controllers;

/// <summary>Авторизованная выдача аудио для музыкального редактора.</summary>
[ApiController]
[Route("api/admin/music")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class MusicReviewController(MusicReviewService music) : ControllerBase
{
  [HttpGet("preview")]
  public async Task<IActionResult> Preview([FromQuery] string path, [FromQuery] bool reviewed, CancellationToken cancellationToken)
  {
    try
    {
      var file = await music.PreparePreviewAsync(new MusicTrack(path, Path.GetFileName(path), reviewed), cancellationToken);
      return PhysicalFile(file, "audio/ogg", enableRangeProcessing: true);
    }
    catch (FileNotFoundException) { return NotFound(); }
    catch (InvalidOperationException exception) { return BadRequest(exception.Message); }
  }
}
