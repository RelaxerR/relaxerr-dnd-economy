using DndEconomy.Application.Notifications;
using DndEconomy.Domain.Entities;
using DndEconomy.Domain.Enums;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DndEconomy.Infrastructure.Notifications;

/// <summary>
/// Реализация <see cref="INotificationService"/>. Использует собственный короткоживущий
/// DbContext через <see cref="IDbContextFactory{TContext}"/> вместо общего на весь circuit —
/// этот сервис вызывается из MainLayout на КАЖДОЙ странице (счётчик непрочитанных), и с общим
/// Scoped-контекстом это регулярно конкурировало с DbContext-запросами самой страницы
/// ("A second operation was started on this context instance...").
/// </summary>
public sealed class NotificationService : INotificationService
{
  private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

  public NotificationService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
  {
    _dbContextFactory = dbContextFactory;
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<NotificationSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    return await dbContext.Notifications.AsNoTracking()
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
  }

  /// <inheritdoc />
  public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    return await dbContext.Notifications.AsNoTracking().CountAsync(x => x.UserId == userId && !x.IsRead, cancellationToken);
  }

  /// <inheritdoc />
  public async Task MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    var notification = await dbContext.Notifications
      .SingleOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId, cancellationToken);

    if (notification is null || notification.IsRead)
      return;

    notification.IsRead = true;
    notification.ReadAtUtc = DateTime.UtcNow;
    await dbContext.SaveChangesAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task CreateAsync(
    Guid userId, NotificationType type, string title, string message,
    Guid? relatedItemId, Guid? relatedItemRequestId, CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    dbContext.Notifications.Add(new Notification
    {
      UserId = userId,
      Type = type,
      Title = title,
      Message = message,
      RelatedItemId = relatedItemId,
      RelatedItemRequestId = relatedItemRequestId
    });

    await dbContext.SaveChangesAsync(cancellationToken);
  }
}
