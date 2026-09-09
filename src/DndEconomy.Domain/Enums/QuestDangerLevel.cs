namespace DndEconomy.Domain.Enums;

/// <summary>
/// Уровень опасности задания — координата в справочнике оплаты заданий
/// (<see cref="DndEconomy.Domain.Entities.QuestPayRate"/>), не влияет ни на что за пределами
/// этого справочника.
/// </summary>
public enum QuestDangerLevel
{
  Trivial = 0,
  Easy = 1,
  Standard = 2,
  Dangerous = 3,
  Deadly = 4
}
