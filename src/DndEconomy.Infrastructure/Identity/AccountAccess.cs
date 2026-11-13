using System.Security.Claims;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DndEconomy.Infrastructure.Identity;

/// <summary>Результат проверки текущей учётной записи и отзыва сессии.</summary>
public enum AccountSessionState { Valid, Inactive, Invalid }

/// <summary>Проверка сессии по свежим данным, общая для HTTP и входящих действий Blazor.</summary>
public sealed class AccountAccess(IDbContextFactory<ApplicationDbContext> factory, IOptions<IdentityOptions> options)
{
  /// <summary>Проверяет активность и security stamp, включая cookies, выданные до отключения.</summary>
  public async Task<AccountSessionState> Check(ClaimsPrincipal? principal)
  {
    if (principal?.Identity?.IsAuthenticated != true || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
      return AccountSessionState.Invalid;
    await using var db = await factory.CreateDbContextAsync();
    var user = await db.Users.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.IsActive, x.SecurityStamp }).SingleOrDefaultAsync();
    if (user is null) return AccountSessionState.Invalid;
    if (!user.IsActive) return AccountSessionState.Inactive;
    var stamp = principal.FindFirstValue(options.Value.ClaimsIdentity.SecurityStampClaimType);
    return stamp is not null && stamp == user.SecurityStamp
      ? AccountSessionState.Valid : AccountSessionState.Invalid;
  }
}
