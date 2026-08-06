using DndEconomy.Application.Items;
using DndEconomy.Application.Notifications;
using DndEconomy.Application.Requests;
using DndEconomy.Domain.Constants;
using DndEconomy.Domain.Entities;
using DndEconomy.Domain.Enums;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DndEconomy.Infrastructure.Requests;

/// <inheritdoc cref="IItemRequestService" />
public sealed class ItemRequestService : IItemRequestService
{
  private readonly ApplicationDbContext _dbContext;
  private readonly UserManager<ApplicationUser> _userManager;
  private readonly INotificationService _notificationService;
  private readonly IItemAdminService _itemAdminService;

  public ItemRequestService(
    ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager,
    INotificationService notificationService, IItemAdminService itemAdminService)
  {
    _dbContext = dbContext;
    _userManager = userManager;
    _notificationService = notificationService;
    _itemAdminService = itemAdminService;
  }

  /// <inheritdoc />
  public async Task SubmitAsync(Guid userId, string proposedName, string? description, CancellationToken cancellationToken)
  {
    var request = new ItemRequest
    {
      RequestedByUserId = userId,
      ProposedName = proposedName,
      Description = description
    };

    _dbContext.ItemRequests.Add(request);
    await _dbContext.SaveChangesAsync(cancellationToken);

    var admins = await _userManager.GetUsersInRoleAsync(RoleNames.Admin);
    foreach (var admin in admins)
    {
      await _notificationService.CreateAsync(
        admin.Id, NotificationType.ItemRequestSubmitted,
        "Новая заявка на предмет",
        $"Игрок предложил добавить «{proposedName}».",
        relatedItemId: null, relatedItemRequestId: request.Id, cancellationToken);
    }
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<ItemRequestSummary>> GetMyRequestsAsync(Guid userId, CancellationToken cancellationToken)
    => await _dbContext.ItemRequests.AsNoTracking()
      .Where(x => x.RequestedByUserId == userId)
      .OrderByDescending(x => x.CreatedAtUtc)
      .Select(ToSummary)
      .ToListAsync(cancellationToken);

  /// <inheritdoc />
  public async Task<IReadOnlyList<ItemRequestSummary>> GetPendingAsync(CancellationToken cancellationToken)
    => await _dbContext.ItemRequests.AsNoTracking()
      .Where(x => x.Status == ItemRequestStatus.Pending)
      .OrderBy(x => x.CreatedAtUtc)
      .Select(ToSummary)
      .ToListAsync(cancellationToken);

  /// <inheritdoc />
  public async Task ApproveAsync(Guid requestId, Guid adminUserId, NewItemInput itemInput, CancellationToken cancellationToken)
  {
    var request = await _dbContext.ItemRequests.SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
    if (request is null || request.Status != ItemRequestStatus.Pending)
      return;

    var itemId = await _itemAdminService.CreateItemAsync(itemInput, cancellationToken);

    request.Status = ItemRequestStatus.Approved;
    request.ResultingItemId = itemId;
    request.ReviewedByUserId = adminUserId;
    request.ReviewedAtUtc = DateTime.UtcNow;
    await _dbContext.SaveChangesAsync(cancellationToken);

    await _notificationService.CreateAsync(
      request.RequestedByUserId, NotificationType.ItemRequestApproved,
      "Заявка одобрена",
      $"Ваша заявка «{request.ProposedName}» одобрена и добавлена в каталог.",
      relatedItemId: itemId, relatedItemRequestId: request.Id, cancellationToken);
  }

  /// <inheritdoc />
  public async Task RejectAsync(Guid requestId, Guid adminUserId, string comment, CancellationToken cancellationToken)
  {
    var request = await _dbContext.ItemRequests.SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
    if (request is null || request.Status != ItemRequestStatus.Pending)
      return;

    request.Status = ItemRequestStatus.Rejected;
    request.ReviewComment = comment;
    request.ReviewedByUserId = adminUserId;
    request.ReviewedAtUtc = DateTime.UtcNow;
    await _dbContext.SaveChangesAsync(cancellationToken);

    await _notificationService.CreateAsync(
      request.RequestedByUserId, NotificationType.ItemRequestRejected,
      "Заявка отклонена",
      $"Ваша заявка «{request.ProposedName}» отклонена. {comment}",
      relatedItemId: null, relatedItemRequestId: request.Id, cancellationToken);
  }

  private static readonly System.Linq.Expressions.Expression<Func<ItemRequest, ItemRequestSummary>> ToSummary = x => new ItemRequestSummary
  {
    Id = x.Id,
    ProposedName = x.ProposedName,
    Description = x.Description,
    Status = x.Status,
    CreatedAtUtc = x.CreatedAtUtc,
    ReviewComment = x.ReviewComment
  };
}
