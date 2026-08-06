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

  public ItemRequestService(
    ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, INotificationService notificationService)
  {
    _dbContext = dbContext;
    _userManager = userManager;
    _notificationService = notificationService;
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
      .Select(x => new ItemRequestSummary
      {
        Id = x.Id,
        ProposedName = x.ProposedName,
        Description = x.Description,
        Status = x.Status,
        CreatedAtUtc = x.CreatedAtUtc,
        ReviewComment = x.ReviewComment
      })
      .ToListAsync(cancellationToken);
}
