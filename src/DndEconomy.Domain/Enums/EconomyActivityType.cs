namespace DndEconomy.Domain.Enums;

/// <summary>
/// Вид экономической активности (<see cref="DndEconomy.Domain.Entities.EconomyActivity"/>) —
/// определяет группировку в интерфейсе и то, как активность участвует в расчёте дохода партии:
/// партийное задание делится с учётом коэффициента размера партии
/// (<c>PartySizeCoefficient</c>), занятие в простое
/// считается для каждого персонажа независимо.
/// </summary>
public enum EconomyActivityType
{
  /// <summary>Партийное задание (контракт) — Дипломатия, Курьерские, Эскорт и т.п.</summary>
  Quest = 0,

  /// <summary>Персональное занятие в простое — Практика профессии, Кутёж, Азартные игры и т.п.</summary>
  Downtime = 1
}
