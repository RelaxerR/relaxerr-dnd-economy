using System.Security.Claims;
using DndEconomy.Domain.Collaboration;

namespace DndEconomy.Application.Collaboration;

/// <summary>Личный календарь и транзакционный процесс согласования игры.</summary>
public interface ISchedulingService
{
  Task<List<AvailabilityRule>> Calendar(ClaimsPrincipal user);
  Task SaveRule(ClaimsPrincipal user, AvailabilityRule rule);
  Task DeleteRule(ClaimsPrincipal user, Guid rule);
  Task<List<SessionWindow>> Preview(ClaimsPrincipal user, string zone, SchedulingSettings settings);
  Task<Guid> Request(ClaimsPrincipal user, Guid room, string zone, SchedulingSettings settings);
  Task Ballot(ClaimsPrincipal user, Guid request, int round, string[] keys);
  Task<Guid> CreateEconomy(ClaimsPrincipal user, Guid game, DndEconomy.Application.Economy.NewEconomySessionInput input);
  Task<(ScheduledGame Game, DateOnly Date)> Game(ClaimsPrincipal user, Guid game);
}
