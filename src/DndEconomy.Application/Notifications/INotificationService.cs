using DndEconomy.Domain.Enums;

namespace DndEconomy.Application.Notifications;

/// <summary>
/// Уведомления пользователю. Без live-пуша (Фаза 2) — список обновляется при навигации/загрузке
/// страницы, не через SignalR; это осознанно отложено на потом.
/// </summary>
public interface INotificationService
{
  Task<IReadOnlyList<NotificationSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken);

  Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken);

  Task MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken);

  /// <summary>Создаёт уведомление одному пользователю. Используется другими сервисами (например, заявками).</summary>
  Task CreateAsync(
    Guid userId, NotificationType type, string title, string message,
    Guid? relatedItemId, Guid? relatedItemRequestId, CancellationToken cancellationToken);
}
