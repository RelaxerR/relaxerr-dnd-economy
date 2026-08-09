using DndEconomy.Application.Users;
using DndEconomy.Domain.Constants;
using DndEconomy.Domain.Entities;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DndEconomy.Infrastructure.Users;

/// <inheritdoc cref="IAdminUserService" />
public sealed class AdminUserService : IAdminUserService
{
  private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
  private readonly UserManager<ApplicationUser> _userManager;

  public AdminUserService(IDbContextFactory<ApplicationDbContext> dbContextFactory, UserManager<ApplicationUser> userManager)
  {
    _dbContextFactory = dbContextFactory;
    _userManager = userManager;
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<UserSummary>> GetUsersAsync(CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    // Одним запросом (без await внутри цикла на каждого пользователя) — на несколько await'ов
    // с одним и тем же DbContext в Blazor Server легко наткнуться на "second operation was
    // started..." при параллельном рендере (например, счётчик уведомлений в MainLayout).
    var users = await dbContext.Users.AsNoTracking().ToListAsync(cancellationToken);
    var userRoles = await (
      from ur in dbContext.UserRoles
      join r in dbContext.Roles on ur.RoleId equals r.Id
      select new { ur.UserId, RoleName = r.Name })
      .ToListAsync(cancellationToken);

    var rolesByUser = userRoles
      .GroupBy(x => x.UserId)
      .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName ?? "").ToList());

    return users.Select(user => new UserSummary
    {
      Id = user.Id,
      Email = user.Email ?? "",
      DisplayName = user.DisplayName,
      MustChangePassword = user.MustChangePassword,
      Roles = rolesByUser.TryGetValue(user.Id, out var roles) ? roles : []
    }).ToList();
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<string>> InviteUserAsync(
    Guid invitedByUserId, string email, string displayName, string temporaryPassword, CancellationToken cancellationToken)
  {
    // UserName == Email — соглашение проекта, см. CLAUDE.md (PasswordSignInAsync ищет по UserName).
    var user = new ApplicationUser
    {
      UserName = email,
      Email = email,
      EmailConfirmed = true,
      DisplayName = displayName,
      MustChangePassword = true,
      InvitedByUserId = invitedByUserId
    };

    var createResult = await _userManager.CreateAsync(user, temporaryPassword);
    if (!createResult.Succeeded)
      return createResult.Errors.Select(e => e.Description).ToList();

    await _userManager.AddToRoleAsync(user, RoleNames.Player);
    return [];
  }
}
