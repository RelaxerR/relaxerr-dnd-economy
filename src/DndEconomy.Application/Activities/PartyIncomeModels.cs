namespace DndEconomy.Application.Activities;

/// <summary>Диапазон суммы в зм (нижняя граница ставки → верхняя граница ставки).</summary>
public readonly record struct GoldRange(decimal Min, decimal Max)
{
  public static GoldRange Zero => new(0, 0);

  public static GoldRange operator +(GoldRange a, GoldRange b) => new(a.Min + b.Min, a.Max + b.Max);

  /// <summary>Умножение на неотрицательный множитель — порядок границ сохраняется.</summary>
  public static GoldRange operator *(GoldRange a, decimal factor) => new(a.Min * factor, a.Max * factor);

  public static GoldRange operator /(GoldRange a, decimal divisor) => new(a.Min / divisor, a.Max / divisor);
}

/// <summary>Вход калькулятора: столько-то завершённых контрактов одного задания за период.</summary>
public sealed record QuestContractInput(EconomyActivitySummary Activity, int ContractCount);

/// <summary>Вход калькулятора: занятие в простое — столько-то дней, столько-то персонажей им занимались.</summary>
public sealed record DowntimeInput(EconomyActivitySummary Activity, int Days, int CharacterCount);

/// <summary>Вход калькулятора дохода партии за период.</summary>
public sealed record PartyIncomeInput
{
  /// <summary>Размер партии N (≥ 1).</summary>
  public required int PartySize { get; init; }

  public required IReadOnlyList<QuestContractInput> Quests { get; init; }
  public required IReadOnlyList<DowntimeInput> Downtime { get; init; }

  /// <summary>Таблица коэффициентов размера партии (любой порядок, может быть пустой).</summary>
  public required IReadOnlyList<PartySizeCoefficientSummary> Coefficients { get; init; }
}

/// <summary>Откуда взят коэффициент размера партии.</summary>
public enum PartySizeCoefficientSource
{
  /// <summary>Есть строка ровно для этого размера партии.</summary>
  Exact,

  /// <summary>Строки для размера нет — взята ближайшая меньшая (в т.ч. последняя строка таблицы при N больше максимума).</summary>
  NearestLower,

  /// <summary>Нет ни одной строки с размером ≤ N (например, таблица пуста) — использован 1.0.</summary>
  Default
}

/// <summary>Применённый коэффициент размера партии.</summary>
public sealed record ResolvedPartySizeCoefficient(decimal Coefficient, PartySizeCoefficientSource Source, int? SourcePartySize);

/// <summary>Результат по одной строке калькулятора.</summary>
public sealed record IncomeLineResult(EconomyActivitySummary Activity, GoldRange PerCharacter, GoldRange Party);

/// <summary>Результат калькулятора дохода партии за период.</summary>
public sealed record PartyIncomeResult
{
  public required ResolvedPartySizeCoefficient Coefficient { get; init; }
  public required IReadOnlyList<IncomeLineResult> QuestLines { get; init; }
  public required IReadOnlyList<IncomeLineResult> DowntimeLines { get; init; }

  /// <summary>Итого на партию за период.</summary>
  public required GoldRange PartyTotal { get; init; }

  /// <summary>
  /// Итого на персонажа за период: полная доля от заданий плюс средняя доля от простоя
  /// (доход/расход простоя партии, делённый на размер партии).
  /// </summary>
  public required GoldRange PerCharacterTotal { get; init; }
}
