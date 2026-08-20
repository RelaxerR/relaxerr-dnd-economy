namespace DndEconomy.Domain.Enums;

/// <summary>
/// Способ массового изменения базовой стоимости предметов в админ-инструменте
/// "глобальное управление стоимостью" (см. IItemAdminService.ApplyBulkPriceUpdateAsync).
/// </summary>
public enum BulkPriceOperation
{
  /// <summary>Прибавить значение к текущей базовой стоимости (значение может быть отрицательным).</summary>
  Add = 0,

  /// <summary>Умножить текущую базовую стоимость на значение (дробное значение — для деления).</summary>
  Multiply = 1,

  /// <summary>Заменить базовую стоимость на указанное значение вне зависимости от текущей.</summary>
  SetTo = 2
}
