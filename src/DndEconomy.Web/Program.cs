using System.Net;
using System.Threading.RateLimiting;
using DndEconomy.Infrastructure;
using DndEconomy.Infrastructure.Identity;
using DndEconomy.Infrastructure.Persistence;
using DndEconomy.Web;
using DndEconomy.Web.Components.Account;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
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

// API-контроллеры (DndEconomy.Web/Controllers) — поиск предмета и создание предмета админом,
// используют ту же cookie-авторизацию Identity, что и Blazor-страницы.
builder.Services.AddControllers();

// Foundry VTT (relaxerr-dnd.ru) — отдельный сайт от relaxerr-dnd-economy.ru (хоть и на одном
// сервере), макросы делают кросс-сайтовые fetch с credentials к /api/*. AllowCredentials требует
// явного списка origin — AllowAnyOrigin с ним несовместим (спека CORS). Второй origin —
// http://77.110.104.211:30000 — прямой адрес по IP:порту в обход nginx/домена: часть игроков
// заходит в Foundry так же часто, как и по домену, оба варианта нужны одновременно. Он httр
// (не https) — cookie авторизации сайта всё равно долетит обратно (Secure относится к
// защищённости запроса К сайту экономики, а не к схеме страницы-источника), но сам этот origin
// по определению не шифрован — трафик до Foundry по IP отдельная история, вне контроля этого приложения.
builder.Services.AddCors(options => options.AddPolicy(CorsPolicyNames.FoundryVtt, policy => policy
  .WithOrigins("https://relaxerr-dnd.ru", "http://77.110.104.211:30000")
  .AllowAnyHeader()
  .WithMethods("GET", "POST")
  .AllowCredentials()));

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddScoped<DndEconomy.Web.Services.ToastService>();

builder.Services.AddRateLimiter(ConfigureRateLimiting);

// Кросс-сайтовый fetch(credentials:'include') с relaxerr-dnd.ru отправит auth-cookie только если
// она SameSite=None (обязательно вместе с Secure — таково требование браузеров). В Development
// оставляем стандартный Lax, иначе локальный вход по обычному HTTP (без TLS) сломается: браузер
// отбрасывает SameSite=None-cookie без Secure, а Secure-cookie не запишется поверх HTTP.
builder.Services.ConfigureApplicationCookie(options =>
{
  if (!builder.Environment.IsDevelopment())
  {
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
  }
});

#endregion

var app = builder.Build();

// В Docker-деплое отдельного шага "выполнить миграции" нет — применяем при каждом старте,
// это идемпотентно (EF пропускает уже применённые). До сидинга: без схемы сидить некуда.
await app.Services.MigrateDatabaseAsync();

// Идемпотентный сидинг ролей Admin/Player и первого администратора из секции AdminSeed
// (см. IdentitySeeder) — до запуска хоста, чтобы им можно было залогиниться сразу.
await app.Services.SeedIdentityDataAsync();

#region Конвейер обработки запросов

if (!app.Environment.IsDevelopment())
{
  app.UseExceptionHandler("/Error", createScopeForErrors: true);
  app.UseHsts();
}

// Приложение стоит за nginx в отдельном Docker-контейнере: без этого Request.Scheme/
// RemoteIpAddress всегда были бы "http" и IP шлюза docker-сети (один и тот же для всех
// клиентов) — UseHttpsRedirection зациклился бы, а партиционирование rate limiter'а по
// RemoteIpAddress (см. ConfigureRateLimiting ниже) схлопнулось бы в общий счётчик на всех
// посетителей сразу. KnownIPNetworks — весь диапазон дефолтных docker-бриджей (172.16.0.0/12)
// плюс loopback, т.к. только nginx на хосте достаёт до этого порта (он опубликован только
// на 127.0.0.1 — см. docker-compose.prod.yml).
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
  ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
forwardedHeadersOptions.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Loopback, 8));
app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
app.UseRateLimiter();
app.UseAuthentication();
// Только endpoint'ы с [EnableCors(CorsPolicyNames.FoundryVtt)] (ItemsController, AuthController)
// получают CORS-заголовки — остальной сайт (Blazor, Account/*) в кросс-сайтовом доступе не нуждается.
app.UseCors();
app.UseAuthorization();
app.UseAntiforgery();

// FallbackPolicy требует авторизации на ЛЮБОМ endpoint без [AllowAnonymous] — без этой
// правки статика (css/framework-скрипты Blazor) тоже блокировалась и редиректила на Login,
// из-за чего страница входа рендерилась без стилей/JS и выглядела "пустой".
app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<DndEconomy.Web.Components.App>()
  .AddInteractiveServerRenderMode();
app.MapAccountEndpoints();
app.MapControllers();

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
