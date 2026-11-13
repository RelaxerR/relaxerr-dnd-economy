using DndEconomy.Application.Collaboration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DndEconomy.Web.Controllers;

/// <summary>Приватное скачивание: нет статики, MIME пользователя или публичного кэширования.</summary>
[ApiController]
[Authorize]
[Route("chat-files")]
public sealed class ChatFilesController(IChatService chat) : ControllerBase
{
  /// <summary>Повторно проверяет права при каждом скачивании, включая прямые ссылки.</summary>
  [HttpGet("{id:guid}")]
  public async Task<IActionResult> Download(Guid id, CancellationToken ct)
  {
    Response.Headers.CacheControl = "private, no-store";
    Response.Headers["X-Content-Type-Options"] = "nosniff";
    try
    {
      var file = await chat.Download(User, id, ct);
      return File(file.Content, "application/octet-stream", file.Name, enableRangeProcessing: false);
    }
    catch (UnauthorizedAccessException) { return NotFound(); }
  }
}
