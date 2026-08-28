namespace DndEconomy.Domain.Constants;

/// <summary>Имена схем аутентификации, регистрируемых в AddAuthentication.</summary>
public static class AuthenticationSchemeNames
{
  /// <summary>
  /// Статичный API-ключ в заголовке (см. ApiKeyAuthenticationHandler) — для макросов Foundry VTT.
  /// Не использует cookie Identity: макрос не пишет и не читает cookie браузера, поэтому не
  /// затирает сессию пользователя, открывшего сайт экономики в соседней вкладке того же браузера.
  /// </summary>
  public const string MacroApiKey = "MacroApiKey";
}
