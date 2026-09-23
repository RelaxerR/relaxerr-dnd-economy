namespace DndEconomy.Domain.Enums;

/// <summary>
/// Уровень опасности партийного задания — задаётся только у активностей
/// <see cref="EconomyActivityType.Quest"/> в справочнике экономической активности
/// (<see cref="DndEconomy.Domain.Entities.EconomyActivity"/>), у занятий в простое его нет.
/// Не влияет ни на что за пределами этого справочника.
/// </summary>
public enum QuestDangerLevel
{
  Trivial = 0,
  Easy = 1,
  Standard = 2,
  Dangerous = 3,
  Deadly = 4
}
