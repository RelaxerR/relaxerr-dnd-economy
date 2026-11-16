using System.Security.Claims;
namespace DndEconomy.Application.Collaboration;

/// <summary>Настройка подписки текущего устройства без выдачи endpoint других устройств.</summary>
public interface IPushService
{
  string? PublicKey { get; }
  /// <summary>Безопасная диагностика конфигурации без значений ключей; null означает готовность.</summary>
  string? ConfigurationError { get; }
  Task Subscribe(ClaimsPrincipal user, string endpoint, string p256dh, string auth);
  Task Unsubscribe(ClaimsPrincipal user, string endpoint);
}
