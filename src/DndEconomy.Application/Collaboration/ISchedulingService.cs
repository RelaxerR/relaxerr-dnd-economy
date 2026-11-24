using System.Security.Claims;
using DndEconomy.Domain.Collaboration;

namespace DndEconomy.Application.Collaboration;

/// <summary>Личный календарь и транзакционный процесс согласования игры.</summary>
public interface ISchedulingService
{
  Task<List<AvailabilityRule>> Calendar(ClaimsPrincipal user);
  /// <summary>Возвращает календарь пользователя администратору или null, если пользователь отсутствует.</summary>
  Task<UserCalendar?> ViewUserCalendar(ClaimsPrincipal user, Guid userId);
  Task SaveRule(ClaimsPrincipal user, AvailabilityRule rule);
  Task DeleteRule(ClaimsPrincipal user, Guid rule);
  /// <summary>Атомарно удаляет только явно выбранные собственные окна, сохраняя остальные исключения.</summary>
  Task DeleteRules(ClaimsPrincipal user, Guid[] rules);
  Task<List<SessionWindow>> Preview(ClaimsPrincipal user, string zone, SchedulingSettings settings);
  Task<Guid> Request(ClaimsPrincipal user, Guid room, string zone, SchedulingSettings settings);
  Task Ballot(ClaimsPrincipal user, Guid request, int round, string[] keys);
  Task<Guid> CreateEconomy(ClaimsPrincipal user, Guid game, DndEconomy.Application.Economy.NewEconomySessionInput input);
  Task<(ScheduledGame Game, DateOnly Date)> Game(ClaimsPrincipal user, Guid game);
}

/// <summary>Имя, часовой пояс и окна выбранного пользователя для просмотра календаря.</summary>
public sealed record UserCalendar(string DisplayName, string Zone, List<AvailabilityRule> Rules);
