using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace DndEconomy.Infrastructure.Identity;

/// <summary>Отзывает доступ отключённой учётки на каждом HTTP-запросе, сохраняя стандартную проверку Identity.</summary>
public sealed class ActiveUserCookieEvents(AccountAccess access) : CookieAuthenticationEvents
{
  private const string InactiveKey = "DndEconomy.InactiveAccount";

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
    if (!context.HttpContext.Items.ContainsKey(InactiveKey)) return base.RedirectToLogin(context);
    context.Response.Redirect("/Account/Inactive");
    return Task.CompletedTask;
  }
}
