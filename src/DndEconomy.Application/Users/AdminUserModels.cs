namespace DndEconomy.Application.Users;

/// <summary>Пользователь для списка в админ-панели.</summary>
public sealed record UserSummary
{
  public required Guid Id { get; init; }
  public required string Email { get; init; }
  public required string DisplayName { get; init; }
  /// <summary>Разрешены вход и участие в работе сайта.</summary>
  public bool IsActive { get; init; } = true;
  public required bool MustChangePassword { get; init; }
  public required IReadOnlyList<string> Roles { get; init; }
}
