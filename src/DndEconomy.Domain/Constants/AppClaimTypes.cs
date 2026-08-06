namespace DndEconomy.Domain.Constants;

/// <summary>Кастомные claim-типы, добавляемые в ClaimsPrincipal при логине.</summary>
public static class AppClaimTypes
{
  /// <summary>
  /// "true"/"false" — зеркалирует <see cref="DndEconomy.Domain.Entities.ApplicationUser.MustChangePassword"/> в cookie,
  /// чтобы FallbackPolicy мог проверять флаг без похода в БД на каждой навигации.
  /// Обновляется вызовом SignInManager.RefreshSignInAsync после смены пароля.
  /// </summary>
  public const string MustChangePassword = "dnd:must_change_password";
}
