using DndEconomy.Application.Import;
using DndEconomy.Application.Pricing;
using DndEconomy.Domain.Entities;
using DndEconomy.Infrastructure.Import;
using DndEconomy.Infrastructure.Persistence;
using DndEconomy.Infrastructure.Pricing;
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
  /// <summary>Регистрирует DbContext, Identity и реализации портов Application-слоя.</summary>
  public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
  {
    var connectionString = configuration.GetConnectionString("Default")
      ?? throw new InvalidOperationException("Не задана строка подключения 'ConnectionStrings:Default'.");

    services.AddDbContext<ApplicationDbContext>(options =>
      options.UseNpgsql(connectionString));

    services
      .AddIdentityCore<ApplicationUser>(options =>
      {
        // Пароли выдаются админом при приглашении — держим базовые требования, без избыточных наворотов.
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.User.RequireUniqueEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
      })
      .AddRoles<IdentityRole<Guid>>()
      .AddEntityFrameworkStores<ApplicationDbContext>()
      .AddSignInManager();

    services.AddScoped<IEconomyPricingReadStore, EconomyPricingReadStore>();
    services.AddScoped<IPriceCalculationService, PriceCalculationService>();
    services.AddScoped<IExcelEconomyImportService, ExcelEconomyImportService>();
    services.AddSingleton(TimeProvider.System);

    return services;
  }
}
