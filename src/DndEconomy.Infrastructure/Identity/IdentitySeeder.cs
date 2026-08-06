using DndEconomy.Domain.Constants;
using DndEconomy.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DndEconomy.Infrastructure.Identity;

/// <summary>
/// Идемпотентный сидинг ролей Admin/Player и первого администратора при старте приложения.
/// Экран приглашений — это Фаза 3 (админ-панель); до него первый админ и тестовые игроки
/// заводятся только так, через конфигурацию/секреты.
/// </summary>
public static class IdentitySeeder
{
  #region Публичная точка входа

  /// <summary>
  /// Открывает scope (UserManager/RoleManager — Scoped-сервисы) и выполняет сидинг.
  /// Вызывается из Program.cs один раз при старте, до app.Run().
  /// </summary>
  public static async Task SeedIdentityDataAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
  {
    using var scope = services.CreateScope();
    var provider = scope.ServiceProvider;

    var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
    var options = provider.GetRequiredService<IOptions<AdminSeedOptions>>().Value;
    var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("DndEconomy.Infrastructure.Identity.IdentitySeeder");

    await EnsureRoleAsync(roleManager, RoleNames.Admin);
    await EnsureRoleAsync(roleManager, RoleNames.Player);
    await EnsureFirstAdminAsync(userManager, options, logger, cancellationToken);
  }

  #endregion

  #region Приватные шаги

  private static async Task EnsureRoleAsync(RoleManager<IdentityRole<Guid>> roleManager, string roleName)
  {
    if (!await roleManager.RoleExistsAsync(roleName))
      await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
  }

  private static async Task EnsureFirstAdminAsync(
    UserManager<ApplicationUser> userManager,
    AdminSeedOptions options,
    ILogger logger,
    CancellationToken cancellationToken)
  {
    var existingAdmins = await userManager.GetUsersInRoleAsync(RoleNames.Admin);
    if (existingAdmins.Count > 0)
      return; // идемпотентность — не пересоздаём при повторном старте

    if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
    {
      logger.LogWarning(
        "Нет ни одного администратора, а секция {Section} не заполнена — задайте {Section}:Email/Password " +
        "(dotnet user-secrets локально или переменные окружения AdminSeed__Email/AdminSeed__Password на сервере).",
        AdminSeedOptions.SectionName, AdminSeedOptions.SectionName);
      return;
    }

    // UserName == Email — соглашение проекта: PasswordSignInAsync ищет по UserName, не по Email.
    var admin = new ApplicationUser
    {
      UserName = options.Email,
      Email = options.Email,
      EmailConfirmed = true,
      DisplayName = options.DisplayName,
      MustChangePassword = true
    };

    var createResult = await userManager.CreateAsync(admin, options.Password);
    if (!createResult.Succeeded)
    {
      logger.LogError(
        "Не удалось создать первого администратора {Email}: {Errors}",
        options.Email, string.Join("; ", createResult.Errors.Select(e => e.Description)));
      return;
    }

    await userManager.AddToRoleAsync(admin, RoleNames.Admin);
    logger.LogWarning(
      "Создан первый администратор {Email} с временным паролем — MustChangePassword=true.",
      options.Email);
  }

  #endregion
}
