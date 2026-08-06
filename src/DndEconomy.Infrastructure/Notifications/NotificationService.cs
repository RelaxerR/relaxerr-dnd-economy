using DndEconomy.Application.Notifications;
using DndEconomy.Domain.Entities;
using DndEconomy.Domain.Enums;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndEconomy.Infrastructure.Notifications;

/// <inheritdoc cref="INotificationService" />
public sealed class NotificationService : INotificationService
{
  private readonly ApplicationDbContext _dbContext;

  public NotificationService(ApplicationDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<NotificationSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
    => await _dbContext.Notifications.AsNoTracking()
      .Where(x => x.UserId == userId)
      .OrderByDescending(x => x.CreatedAtUtc)
      .Select(x => new NotificationSummary
      {
        Id = x.Id,
        Type = x.Type,
        Title = x.Title,
        Message = x.Message,
        IsRead = x.IsRead,
        CreatedAtUtc = x.CreatedAtUtc,
        RelatedItemId = x.RelatedItemId,
        RelatedItemRequestId = x.RelatedItemRequestId
      })
      .ToListAsync(cancellationToken);

  /// <inheritdoc />
  public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken)
    => _dbContext.Notifications.AsNoTracking().CountAsync(x => x.UserId == userId && !x.IsRead, cancellationToken);

  /// <inheritdoc />
  public async Task MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken)
  {
    var notification = await _dbContext.Notifications
      .SingleOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId, cancellationToken);

    if (notification is null || notification.IsRead)
      return;

    notification.IsRead = true;
    notification.ReadAtUtc = DateTime.UtcNow;
    await _dbContext.SaveChangesAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task CreateAsync(
    Guid userId, NotificationType type, string title, string message,
    Guid? relatedItemId, Guid? relatedItemRequestId, CancellationToken cancellationToken)
  {
    _dbContext.Notifications.Add(new Notification
    {
      UserId = userId,
      Type = type,
      Title = title,
      Message = message,
      RelatedItemId = relatedItemId,
      RelatedItemRequestId = relatedItemRequestId
    });

    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
