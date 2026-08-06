namespace DndEconomy.Infrastructure.Identity;

/// <summary>
/// Данные для создания первого администратора при старте, если в БД ещё нет ни одного.
/// Email/Password задаются через user-secrets локально или переменные окружения на сервере
/// (AdminSeed__Email/AdminSeed__Password) — в appsettings.json хранится только пустой каркас.
/// </summary>
public sealed class AdminSeedOptions
{
  /// <summary>Имя секции конфигурации.</summary>
  public const string SectionName = "AdminSeed";

  /// <summary>Email первого администратора. Также используется как UserName (см. соглашение проекта).</summary>
  public string? Email { get; set; }

  /// <summary>Временный пароль — при первом входе администратор обязан его сменить.</summary>
  public string? Password { get; set; }

  /// <summary>Отображаемое имя.</summary>
  public string DisplayName { get; set; } = "Админ";
}
