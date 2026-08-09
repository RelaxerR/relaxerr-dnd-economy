using DndEconomy.Application.Items;
using DndEconomy.Application.Notifications;
using DndEconomy.Application.Profile;
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
  private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
  private readonly UserManager<ApplicationUser> _userManager;
  private readonly INotificationService _notificationService;
  private readonly IItemAdminService _itemAdminService;
  private readonly IPlayerProfileService _playerProfileService;

  public ItemRequestService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory, UserManager<ApplicationUser> userManager,
    INotificationService notificationService, IItemAdminService itemAdminService,
    IPlayerProfileService playerProfileService)
  {
    _dbContextFactory = dbContextFactory;
    _userManager = userManager;
    _notificationService = notificationService;
    _itemAdminService = itemAdminService;
    _playerProfileService = playerProfileService;
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

    await using (var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
    {
      dbContext.ItemRequests.Add(request);
      await dbContext.SaveChangesAsync(cancellationToken);
    }

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
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    return await dbContext.ItemRequests.AsNoTracking()
      .Where(x => x.RequestedByUserId == userId)
      .OrderByDescending(x => x.CreatedAtUtc)
      .Select(ToSummary)
      .ToListAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<ItemRequestSummary>> GetPendingAsync(CancellationToken cancellationToken)
  {
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    return await dbContext.ItemRequests.AsNoTracking()
      .Where(x => x.Status == ItemRequestStatus.Pending)
      .OrderBy(x => x.CreatedAtUtc)
      .Select(ToSummary)
      .ToListAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task ApproveAsync(Guid requestId, Guid adminUserId, NewItemInput itemInput, CancellationToken cancellationToken)
  {
    // CreateItemAsync берёт свой отдельный короткоживущий DbContext (через IItemAdminService),
    // поэтому safe вызывать его между чтением и сохранением request — конфликта с dbContext ниже нет.
    var itemId = await _itemAdminService.CreateItemAsync(itemInput, cancellationToken);

    Guid requestedByUserId;
    string proposedName;

    await using (var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
    {
      var request = await dbContext.ItemRequests.SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
      if (request is null || request.Status != ItemRequestStatus.Pending)
        return;

      request.Status = ItemRequestStatus.Approved;
      request.ResultingItemId = itemId;
      request.ReviewedByUserId = adminUserId;
      request.ReviewedAtUtc = DateTime.UtcNow;
      await dbContext.SaveChangesAsync(cancellationToken);

      requestedByUserId = request.RequestedByUserId;
      proposedName = request.ProposedName;
    }

    // Игрок предложил предмет — логично, что он же хочет его отслеживать, поэтому сразу
    // кладём созданный предмет в избранное автору заявки, а не заставляем искать его в каталоге.
    await _playerProfileService.AddFavoriteAsync(requestedByUserId, itemId, note: null, cancellationToken);

    await _notificationService.CreateAsync(
      requestedByUserId, NotificationType.ItemRequestApproved,
      "Заявка одобрена",
      $"Ваша заявка «{proposedName}» одобрена и добавлена в каталог.",
      relatedItemId: itemId, relatedItemRequestId: requestId, cancellationToken);
  }

  /// <inheritdoc />
  public async Task RejectAsync(Guid requestId, Guid adminUserId, string comment, CancellationToken cancellationToken)
  {
    Guid requestedByUserId;
    string proposedName;

    await using (var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
    {
      var request = await dbContext.ItemRequests.SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
      if (request is null || request.Status != ItemRequestStatus.Pending)
        return;

      request.Status = ItemRequestStatus.Rejected;
      request.ReviewComment = comment;
      request.ReviewedByUserId = adminUserId;
      request.ReviewedAtUtc = DateTime.UtcNow;
      await dbContext.SaveChangesAsync(cancellationToken);

      requestedByUserId = request.RequestedByUserId;
      proposedName = request.ProposedName;
    }

    await _notificationService.CreateAsync(
      requestedByUserId, NotificationType.ItemRequestRejected,
      "Заявка отклонена",
      $"Ваша заявка «{proposedName}» отклонена. {comment}",
      relatedItemId: null, relatedItemRequestId: requestId, cancellationToken);
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
