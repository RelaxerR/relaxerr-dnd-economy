namespace DndEconomy.Domain.Constants;

/// <summary>Имена политик авторизации, регистрируемых в AddAuthorizationCore.</summary>
public static class AuthorizationPolicyNames
{
  /// <summary>
  /// Требует только аутентификации, без проверки MustChangePassword — нужна странице смены
  /// временного пароля, чтобы её саму не заблокировал общий FallbackPolicy.
  /// </summary>
  public const string AuthenticatedOnly = "AuthenticatedOnly";
}
