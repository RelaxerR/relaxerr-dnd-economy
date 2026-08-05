using System.Threading.RateLimiting;
using DndEconomy.Infrastructure;
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

builder.Services.AddRateLimiter(ConfigureRateLimiting);

#endregion

var app = builder.Build();

#region Конвейер обработки запросов

if (!app.Environment.IsDevelopment())
{
  app.UseExceptionHandler("/Error", createScopeForErrors: true);
  app.UseHsts();
}

app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
app.UseRateLimiter();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<DndEconomy.Web.Components.App>()
  .AddInteractiveServerRenderMode();

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

  options.AddPolicy("login", httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
      partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
      factory: _ => new FixedWindowRateLimiterOptions
      {
        PermitLimit = 5,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0
      }));
}
