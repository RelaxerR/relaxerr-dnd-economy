namespace DndEconomy.Application.Activities;

/// <summary>
/// Управление таблицей коэффициентов размера партии (убывающая полезность партийных заданий).
/// Строки адресуются естественным ключом — размером партии, а не Id: в таблице не больше
/// одной строки на размер. Редактируется прямо на странице калькулятора дохода.
/// </summary>
public interface IPartySizeCoefficientAdminService
{
  /// <summary>Все заведённые строки по возрастанию размера партии.</summary>
  Task<IReadOnlyList<PartySizeCoefficientSummary>> GetAllAsync(CancellationToken cancellationToken);

  /// <summary>
  /// Создаёт строку для размера партии или обновляет её коэффициент, если строка уже есть.
  /// Бросает <see cref="ArgumentException"/> при размере вне 1..8 или коэффициенте ≤ 0.
  /// </summary>
  Task SetAsync(int partySize, decimal coefficient, CancellationToken cancellationToken);

  /// <summary>Удаляет строку для размера партии (ничего не делает, если её нет).</summary>
  Task DeleteAsync(int partySize, CancellationToken cancellationToken);
}

public sealed record PartySizeCoefficientSummary
{
  public required int PartySize { get; init; }
  public required decimal Coefficient { get; init; }
}
