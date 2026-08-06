using DndEconomy.Domain.Enums;

namespace DndEconomy.Application.Notifications;

/// <summary>Уведомление для отображения в UI.</summary>
public sealed class NotificationSummary
{
  public required Guid Id { get; init; }
  public required NotificationType Type { get; init; }
  public required string Title { get; init; }
  public required string Message { get; init; }
  public required bool IsRead { get; init; }
  public required DateTime CreatedAtUtc { get; init; }
  public Guid? RelatedItemId { get; init; }
  public Guid? RelatedItemRequestId { get; init; }
}
