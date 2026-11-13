using DndEconomy.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DndEconomy.Infrastructure.Identity;

/// <summary>Запрещает выдачу новой сессии временно отключённому пользователю.</summary>
public sealed class ActiveUserSignInManager(UserManager<ApplicationUser> users, IHttpContextAccessor context,
  IUserClaimsPrincipalFactory<ApplicationUser> claims, IOptions<IdentityOptions> options,
  ILogger<SignInManager<ApplicationUser>> logger, IAuthenticationSchemeProvider schemes, IUserConfirmation<ApplicationUser> confirmation)
  : SignInManager<ApplicationUser>(users, context, claims, options, logger, schemes, confirmation)
{
  /// <inheritdoc />
  public override async Task<bool> CanSignInAsync(ApplicationUser user) => user.IsActive && await base.CanSignInAsync(user);
}
