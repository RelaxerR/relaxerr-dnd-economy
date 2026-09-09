namespace DndEconomy.Domain.Enums;

/// <summary>
/// Эпоха кампании — соответствует диапазону уровней партии, используется мастером как
/// координата в справочнике оплаты заданий (<see cref="DndEconomy.Domain.Entities.QuestPayRate"/>).
/// I: 1-4 ур., II: 5-10 ур., III: 11-16 ур., IV: 17-20 ур.
/// </summary>
public enum QuestEpoch
{
  I = 0,
  II = 1,
  III = 2,
  IV = 3
}
