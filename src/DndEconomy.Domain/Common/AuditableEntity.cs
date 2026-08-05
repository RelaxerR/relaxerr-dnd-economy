namespace DndEconomy.Domain.Common;

/// <summary>
/// Базовый класс для сущностей, которым нужно отслеживать дату создания и последнего изменения.
/// Наследники получают Id, CreatedAtUtc и UpdatedAtUtc без дублирования кода.
/// </summary>
public abstract class AuditableEntity
{
  #region Идентификация

  /// <summary>
  /// Уникальный идентификатор сущности.
  /// </summary>
  public Guid Id { get; set; } = Guid.NewGuid();

  #endregion

  #region Аудит

  /// <summary>
  /// Дата и время создания записи (UTC).
  /// </summary>
  public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

  /// <summary>
  /// Дата и время последнего изменения записи (UTC). Null, если запись ни разу не изменялась.
  /// </summary>
  public DateTime? UpdatedAtUtc { get; set; }

  #endregion
}
