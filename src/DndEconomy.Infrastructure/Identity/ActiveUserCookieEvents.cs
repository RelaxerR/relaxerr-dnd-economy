using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;

namespace DndEconomy.Infrastructure.Identity;

/// <summary>Отзывает доступ отключённой учётки на каждом HTTP-запросе, сохраняя стандартную проверку Identity.</summary>
public sealed class ActiveUserCookieEvents(AccountAccess access) : CookieAuthenticationEvents
{
  private const string InactiveKey = "DndEconomy.InactiveAccount";

  /// <summary>Показывает причину отзыва и при прямом переходе на анонимную страницу входа.</summary>
  public static bool WasInactive(HttpContext context) => context.Items.ContainsKey(InactiveKey);

  /// <inheritdoc />
  public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
  {
    var state = await access.Check(context.Principal);
    if (state == AccountSessionState.Valid)
    {
      await SecurityStampValidator.ValidatePrincipalAsync(context);
      return;
    }
    if (state == AccountSessionState.Inactive) context.HttpContext.Items[InactiveKey] = true;
    context.RejectPrincipal();
    await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
  }

  /// <inheritdoc />
  public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
  {
    if (!WasInactive(context.HttpContext)) return base.RedirectToLogin(context);
    context.Response.Redirect("/Account/Inactive");
    return Task.CompletedTask;
  }
}
