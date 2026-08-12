using System.ComponentModel.DataAnnotations;
using DndEconomy.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DndEconomy.Web.Controllers;

/// <summary>Учётные данные для JSON-логина.</summary>
public sealed class LoginRequest
{
  [Required, EmailAddress] public string Email { get; init; } = string.Empty;
  [Required] public string Password { get; init; } = string.Empty;
}

/// <summary>
/// JSON-эндпоинт логина для внешних клиентов (например, макросов Foundry VTT), которые не могут
/// пройти обычную SSR-форму /Account/Login (антифорджери-токен и скрытое поле _handler привязаны
/// к разметке конкретной страницы и неудобны для скрипта с другого домена). Использует тот же
/// SignInManager и ту же cookie-схему Identity, что и обычный вход — после успешного логина
/// браузер сохраняет ту же auth-cookie, что и при входе через сайт.
/// </summary>
[ApiController]
[Route("api/auth")]
[EnableCors(CorsPolicyNames.FoundryVtt)]
public sealed class AuthController : ControllerBase
{
  private readonly SignInManager<ApplicationUser> _signInManager;
  private readonly ILogger<AuthController> _logger;

  public AuthController(SignInManager<ApplicationUser> signInManager, ILogger<AuthController> logger)
  {
    _signInManager = signInManager;
    _logger = logger;
  }

  /// <summary>Логинит пользователя по email/паролю и выставляет cookie Identity. 30 попыток/мин на IP — та же политика, что и у /Account/Login.</summary>
  [HttpPost("login")]
  [AllowAnonymous]
  [EnableRateLimiting("login")]
  public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
  {
    var result = await _signInManager.PasswordSignInAsync(request.Email, request.Password, isPersistent: true, lockoutOnFailure: true);

    if (result.Succeeded)
    {
      _logger.LogInformation("API-логин: пользователь {Email} вошёл в систему.", request.Email);
      return Ok();
    }

    if (result.IsLockedOut)
    {
      _logger.LogWarning("API-логин: учётная запись {Email} временно заблокирована после серии неверных попыток входа.", request.Email);
      return StatusCode(StatusCodes.Status423Locked, "Учётная запись временно заблокирована после серии неверных попыток входа.");
    }

    return Unauthorized();
  }
}
