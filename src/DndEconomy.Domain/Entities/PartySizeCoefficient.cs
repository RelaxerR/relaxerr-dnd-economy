using DndEconomy.Domain.Common;

namespace DndEconomy.Domain.Entities;

/// <summary>
/// Коэффициент убывающей полезности для партийных заданий
/// (<see cref="DndEconomy.Domain.Enums.EconomyActivityType.Quest"/>): множитель к ставке на
/// игрока в зависимости от размера партии. Итоговая выплата партии за задание —
/// <c>N × Ставка × Coefficient(N)</c>, где для N больше наибольшего заведённого
/// <see cref="PartySize"/> берётся коэффициент последней (наибольшей) строки, без экстраполяции.
/// Занятия в простое этот коэффициент не используют — там каждый персонаж считается независимо.
/// </summary>
public class PartySizeCoefficient : AuditableEntity
{
  /// <summary>Наименьший допустимый размер партии.</summary>
  public const int MinPartySize = 1;

  /// <summary>Наибольший размер партии, для которого можно завести строку.</summary>
  public const int MaxPartySize = 8;

  /// <summary>Размер партии (<see cref="MinPartySize"/>..<see cref="MaxPartySize"/>), уникален.</summary>
  public int PartySize { get; set; }

  /// <summary>
  /// Множитель к ставке на одного игрока — 1.0 при <see cref="PartySize"/> = 1, дальше
  /// убывает. Строго больше нуля.
  /// </summary>
  public decimal Coefficient { get; set; }
}
