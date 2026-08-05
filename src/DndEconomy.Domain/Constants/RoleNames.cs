namespace DndEconomy.Domain.Constants;

/// <summary>
/// Имена ролей ASP.NET Core Identity. Централизовано, чтобы не разбрасывать строковые
/// литералы "Admin"/"Player" по атрибутам [Authorize] и коду.
/// </summary>
public static class RoleNames
{
  /// <summary>Администратор: управляет каталогом, коэффициентами, пользователями и заявками.</summary>
  public const string Admin = "Admin";

  /// <summary>Игрок: ищет предметы, сохраняет их в профиль, отправляет заявки.</summary>
  public const string Player = "Player";
}
