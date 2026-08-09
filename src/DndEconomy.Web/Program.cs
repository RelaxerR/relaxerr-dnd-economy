using System.Threading.RateLimiting;
using DndEconomy.Infrastructure;
using DndEconomy.Infrastructure.Identity;
using DndEconomy.Web.Components.Account;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;

// Serilog поднимаем до создания WebApplicationBuilder, чтобы логировать даже ошибки конфигурации хоста.
Log.Logger = new LoggerConfiguration()
  .WriteTo.Console()
  .WriteTo.File("logs/dnd-economy-.log", rollingInterval: RollingInterval.Day)
  .Enrich.FromLogContext()
  .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
  .ReadFrom.Configuration(context.Configuration)
  .ReadFrom.Services(services)
  .Enrich.FromLogContext());

#region Регистрация сервисов

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddRazorComponents()
  .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddScoped<DndEconomy.Web.Services.ToastService>();

builder.Services.AddRateLimiter(ConfigureRateLimiting);

#endregion

var app = builder.Build();

// Идемпотентный сидинг ролей Admin/Player и первого администратора из секции AdminSeed
// (см. IdentitySeeder) — до запуска хоста, чтобы им можно было залогиниться сразу.
await app.Services.SeedIdentityDataAsync();

#region Конвейер обработки запросов

if (!app.Environment.IsDevelopment())
{
  app.UseExceptionHandler("/Error", createScopeForErrors: true);
  app.UseHsts();
}

app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// FallbackPolicy требует авторизации на ЛЮБОМ endpoint без [AllowAnonymous] — без этой
// правки статика (css/framework-скрипты Blazor) тоже блокировалась и редиректила на Login,
// из-за чего страница входа рендерилась без стилей/JS и выглядела "пустой".
app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<DndEconomy.Web.Components.App>()
  .AddInteractiveServerRenderMode();
app.MapAccountEndpoints();

#endregion

app.Run();

/// <summary>
/// Настраивает простую, но действенную защиту от перебора/DDoS на уровне приложения:
/// общий лимит запросов на IP плюс отдельная, более строгая политика для входа в систему.
/// Это первый рубеж — второй, более надёжный, обеспечивается Cloudflare перед доменом.
/// </summary>
static void ConfigureRateLimiting(RateLimiterOptions options)
{
  options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

  options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    RateLimitPartition.GetSlidingWindowLimiter(
      partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
      factory: _ => new SlidingWindowRateLimiterOptions
      {
        PermitLimit = 100,
        Window = TimeSpan.FromMinutes(1),
        SegmentsPerWindow = 4,
        QueueLimit = 0
      }));

  // Поднято с 5/мин — этот лимит считает КАЖДЫЙ GET/POST на /Account/Login, включая
  // повторные загрузки страницы при ручном тестировании, а не только попытки входа.
  options.AddPolicy("login", httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
      partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
      factory: _ => new FixedWindowRateLimiterOptions
      {
        PermitLimit = 30,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
      }));
}
