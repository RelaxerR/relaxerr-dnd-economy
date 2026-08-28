using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using DndEconomy.Domain.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DndEconomy.Infrastructure.Identity;

/// <summary>
/// Схема "MacroApiKey" — аутентификация по статичному ключу в заголовке <see cref="HeaderName"/>,
/// без cookie. Раньше макросы Foundry VTT логинились через SignInManager и получали ту же
/// auth-cookie Identity, что и обычный вход на сайте — поскольку cookie принадлежит браузеру и
/// домену целиком (а не конкретной вкладке), каждый вызов макроса из вкладки Foundry перезаписывал
/// cookie во ВСЕХ вкладках того же браузера, включая ту, где пользователь был залогинен в сайт под
/// собой — выглядело как "постоянно перелогинивает". Ключ полностью развязывает эти два сценария.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
  /// <summary>Заголовок, в котором макрос передаёт ключ.</summary>
  public const string HeaderName = "X-Api-Key";

  public ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : base(options, logger, encoder)
  {
  }

  protected override Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    if (!Request.Headers.TryGetValue(HeaderName, out var values) || values.Count == 0)
      return Task.FromResult(AuthenticateResult.NoResult());

    var providedKey = values.ToString();

    string role;
    if (KeyMatches(providedKey, Options.AdminKey))
      role = RoleNames.Admin;
    else if (KeyMatches(providedKey, Options.PlayerKey))
      role = RoleNames.Player;
    else
      return Task.FromResult(AuthenticateResult.Fail("Неверный API-ключ."));

    var identity = new ClaimsIdentity(
      [new Claim(ClaimTypes.Name, "foundry-macro"), new Claim(ClaimTypes.Role, role)], Scheme.Name);
    var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
    return Task.FromResult(AuthenticateResult.Success(ticket));
  }

  /// <summary>
  /// Сравнение через хэш постоянной длины (а не строк напрямую) — защита от timing-атаки по
  /// длине ключа; заодно безопасно, если <paramref name="configuredKey"/> не задан в конфиге.
  /// </summary>
  private static bool KeyMatches(string providedKey, string configuredKey)
  {
    if (string.IsNullOrEmpty(configuredKey))
      return false;

    var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey));
    var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
    return CryptographicOperations.FixedTimeEquals(providedHash, configuredHash);
  }
}
