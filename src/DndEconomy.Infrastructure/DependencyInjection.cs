using System.Security.Claims;
using DndEconomy.Application.Catalog;
using DndEconomy.Application.Economy;
using DndEconomy.Application.Import;
using DndEconomy.Application.Items;
using DndEconomy.Application.Notifications;
using DndEconomy.Application.Pricing;
using DndEconomy.Application.Profile;
using DndEconomy.Application.Requests;
using DndEconomy.Application.Users;
using DndEconomy.Domain.Constants;
using DndEconomy.Domain.Entities;
using DndEconomy.Infrastructure.Catalog;
using DndEconomy.Infrastructure.Economy;
using DndEconomy.Infrastructure.Identity;
using DndEconomy.Infrastructure.Import;
using DndEconomy.Infrastructure.Items;
using DndEconomy.Infrastructure.Notifications;
using DndEconomy.Infrastructure.Persistence;
using DndEconomy.Infrastructure.Pricing;
using DndEconomy.Infrastructure.Profile;
using DndEconomy.Infrastructure.Requests;
using DndEconomy.Infrastructure.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DndEconomy.Infrastructure;

/// <summary>
/// Точка входа для регистрации всех сервисов слоя Infrastructure: БД, Identity, read-store для цен.
/// Вызывается один раз из Program.cs хост-проекта.
/// </summary>
public static class DependencyInjection
{
  #region Публичный метод

  /// <summary>Регистрирует DbContext, Identity (cookie-аутентификация + авторизация) и реализации портов Application-слоя.</summary>
  public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
  {
    AddPersistence(services, configuration);
    AddIdentityWithCookies(services);
    AddSeeding(services, configuration);
    AddApplicationServices(services);

    return services;
  }

  #endregion

  #region Приватные шаги

  private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
  {
    var connectionString = configuration.GetConnectionString("Default")
      ?? throw new InvalidOperationException("Не задана строка подключения 'ConnectionStrings:Default'.");

    // AddDbContextFactory вместо AddDbContext: помимо обычного Scoped-инжекта ApplicationDbContext
    // (как раньше, весь остальной код не меняется) даёт IDbContextFactory — нужен NotificationService,
    // чтобы создавать отдельный короткоживущий контекст для счётчика уведомлений в MainLayout
    // (он есть на каждой странице и иначе конкурирует за общий на circuit DbContext с самой страницей —
    // "A second operation was started on this context instance...", см. CLAUDE.md).
    services.AddDbContextFactory<ApplicationDbContext>(options =>
      options.UseNpgsql(connectionString));
  }

  /// <summary>
  /// AddIdentityCore (а не полный AddIdentity) + явное подключение cookie-схемы через
  /// AddIdentityCookies и secure-by-default авторизация: без [AllowAnonymous] доступ
  /// требует и логина, и завершённой смены временного пароля (MustChangePassword=false).
  /// </summary>
  private static void AddIdentityWithCookies(IServiceCollection services)
  {
    services
      .AddIdentityCore<ApplicationUser>(options =>
      {
        // Пароли выдаются админом при приглашении — держим базовые требования, без избыточных наворотов.
        // RequireLowercase/RequireUppercase у Identity проверяют только ASCII a-z/A-Z, а не Unicode —
        // отключаем оба, иначе пароль на кириллице (например, "ВременныйПароль123!") не проходит.
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.User.RequireUniqueEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
      })
      .AddRoles<IdentityRole<Guid>>()
      .AddEntityFrameworkStores<ApplicationDbContext>()
      .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>()
      .AddSignInManager();

    services
      .AddAuthentication(options =>
      {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
      })
      .AddIdentityCookies();

    services.AddAuthorizationCore(options =>
    {
      // Служебные эндпоинты самого Blazor Server (/_blazor/..., /_framework/...) не размечены
      // [AllowAnonymous] — под FallbackPolicy без исключения они редиректили бы на Login на
      // каждой анонимной странице (браузер сам инициализирует их через blazor.web.js),
      // забивая rate-limit политику "login" и ломая интерактивность/стили шапки.
      options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAssertion(ctx =>
        {
          if (ctx.Resource is HttpContext { Request.Path: var path } &&
              (path.StartsWithSegments("/_blazor") || path.StartsWithSegments("/_framework")))
            return true;

          return ctx.User.Identity?.IsAuthenticated == true
            && ctx.User.FindFirstValue(AppClaimTypes.MustChangePassword) != "true";
        })
        .Build();

      // Пускает залогиненного пользователя, даже если у него MustChangePassword=true —
      // нужна странице смены пароля, иначе FallbackPolicy заблокировал бы её саму.
      options.AddPolicy(AuthorizationPolicyNames.AuthenticatedOnly, policy => policy.RequireAuthenticatedUser());
    });
  }

  /// <summary>Опции сидинга ролей/первого администратора (см. <see cref="IdentitySeeder"/>).</summary>
  private static void AddSeeding(IServiceCollection services, IConfiguration configuration)
  {
    services.Configure<AdminSeedOptions>(configuration.GetSection(AdminSeedOptions.SectionName));
  }

  private static void AddApplicationServices(IServiceCollection services)
  {
    services.AddScoped<IEconomyPricingReadStore, EconomyPricingReadStore>();
    services.AddScoped<IPriceCalculationService, PriceCalculationService>();
    services.AddScoped<IExcelEconomyImportService, ExcelEconomyImportService>();
    services.AddScoped<ICatalogReadStore, CatalogReadStore>();
    services.AddScoped<ICatalogQueryService, CatalogQueryService>();
    services.AddScoped<IPlayerProfileService, PlayerProfileService>();
    services.AddScoped<INotificationService, NotificationService>();
    services.AddScoped<IItemRequestService, ItemRequestService>();
    services.AddScoped<IItemAdminService, ItemAdminService>();
    services.AddScoped<IAdminUserService, AdminUserService>();
    services.AddScoped<IEconomyAdminService, EconomyAdminService>();
    services.AddSingleton(TimeProvider.System);
  }

  #endregion
}
