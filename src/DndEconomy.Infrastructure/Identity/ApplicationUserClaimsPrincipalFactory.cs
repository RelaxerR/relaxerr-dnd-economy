using System.Security.Claims;
using DndEconomy.Domain.Constants;
using DndEconomy.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DndEconomy.Infrastructure.Identity;

/// <summary>
/// Расширяет стандартный principal claim'ом MustChangePassword, чтобы авторизационный
/// FallbackPolicy мог проверять флаг форс-смены пароля прямо из cookie, без запроса к БД.
/// </summary>
public sealed class ApplicationUserClaimsPrincipalFactory
  : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>
{
  public ApplicationUserClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<IdentityOptions> optionsAccessor)
    : base(userManager, roleManager, optionsAccessor)
  {
  }

  /// <inheritdoc />
  public override async Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
  {
    var principal = await base.CreateAsync(user);
    ((ClaimsIdentity)principal.Identity!).AddClaim(
      new Claim(AppClaimTypes.MustChangePassword, user.MustChangePassword ? "true" : "false"));
    return principal;
  }
}
