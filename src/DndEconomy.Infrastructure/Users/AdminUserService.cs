using DndEconomy.Application.Users;
using DndEconomy.Domain.Constants;
using DndEconomy.Domain.Entities;
using DndEconomy.Infrastructure.Persistence;
using DndEconomy.Infrastructure.Collaboration;
using DndEconomy.Domain.Collaboration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DndEconomy.Infrastructure.Users;

/// <inheritdoc cref="IAdminUserService" />
public sealed class AdminUserService : IAdminUserService
{
  private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
  private readonly UserManager<ApplicationUser> _userManager;
  private readonly ILogger<AdminUserService> _logger;

  public AdminUserService(IDbContextFactory<ApplicationDbContext> dbContextFactory, UserManager<ApplicationUser> userManager, ILogger<AdminUserService> logger)
  {
    _dbContextFactory = dbContextFactory;
    _userManager = userManager;
    _logger = logger;
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
      IsActive = user.IsActive,
      Roles = rolesByUser.TryGetValue(user.Id, out var roles) ? roles : []
    }).ToList();
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<string>> InviteUserAsync(
    Guid invitedByUserId, string email, string displayName, string temporaryPassword, string role, CancellationToken cancellationToken)
  {
    if (role != RoleNames.Admin && role != RoleNames.Player)
      return [$"Неизвестная роль «{role}»."];

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

    await _userManager.AddToRoleAsync(user, role);
    return [];
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<string>> SetActiveAsync(Guid requestingAdminId, Guid userId, bool isActive, CancellationToken cancellationToken)
  {
    await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
    await ChatAccess.Lock(db);
    var adminIds = await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id
      where role.Name == RoleNames.Admin select ur.UserId).ToArrayAsync(cancellationToken);
    if (!adminIds.Contains(requestingAdminId) || !await db.Users.AnyAsync(x => x.Id == requestingAdminId && x.IsActive, cancellationToken))
      return ["Нет доступа к управлению пользователями."];
    if (!isActive && requestingAdminId == userId) return ["Нельзя отключить самого себя."];
    var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
    if (user is null) return ["Пользователь не найден."];
    if (user.IsActive == isActive) return [];
    if (!isActive && adminIds.Contains(userId) && await db.Users.CountAsync(x => x.IsActive && adminIds.Contains(x.Id), cancellationToken) <= 1)
      return ["Нельзя отключить последнего активного администратора."];
    user.IsActive = isActive;
    user.SecurityStamp = Guid.NewGuid().ToString();
    user.ConcurrencyStamp = Guid.NewGuid().ToString();
    await db.Set<ChatPresence>().Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
    await db.SaveChangesAsync(cancellationToken);
    await SchedulingService.Recalculate(db, DateTime.UtcNow);
    await db.SaveChangesAsync(cancellationToken);
    await tx.CommitAsync(cancellationToken);
    _logger.LogInformation("Администратор {AdminId} изменил активность пользователя {UserId}: {IsActive}", requestingAdminId, userId, isActive);
    return [];
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<string>> DeleteUserAsync(Guid requestingAdminId, Guid userId, CancellationToken cancellationToken)
  {
    if (requestingAdminId == userId)
      return ["Нельзя удалить самого себя."];

    var user = await _userManager.FindByIdAsync(userId.ToString());
    if (user is null)
      return [];

    if (await _userManager.IsInRoleAsync(user, RoleNames.Admin))
    {
      var admins = await _userManager.GetUsersInRoleAsync(RoleNames.Admin);
      if (user.IsActive && admins.Count(x => x.IsActive) <= 1)
        return ["Нельзя удалить последнего администратора."];
    }

    var deleteResult = await _userManager.DeleteAsync(user);
    return deleteResult.Succeeded ? [] : deleteResult.Errors.Select(e => e.Description).ToList();
  }
}
