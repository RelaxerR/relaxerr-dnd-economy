using DndEconomy.Domain.Enums;

namespace DndEconomy.Application.Activities;

/// <summary>
/// Калькулятор дохода партии за период — чистая функция над справочником экономической
/// активности и таблицей коэффициентов размера партии, без обращения к БД.
/// <code>
/// Задание, на персонажа = Ставка × РекДлительность × Coef(N) × КолвоКонтрактов
/// Задание, на партию    = N × (на персонажа)
/// Простой, на партию    = Ставка × Дни × КолвоПерсонажей          (без Coef)
/// Простой, на персонажа = (на партию) / N                          (средняя доля)
/// </code>
/// Выплата за контракт считается от рекомендуемой длительности, а не от фактически
/// потраченных дней (см. <c>EconomyActivity.RecommendedDurationDays</c>). Все множители
/// неотрицательны, поэтому нижняя граница ставки всегда даёт нижнюю границу суммы — и для
/// дохода, и для расхода (отрицательной ставки).
/// </summary>
public static class PartyIncomeCalculator
{
  #region Публичные методы

  /// <summary>
  /// Считает доход партии. Бросает <see cref="ArgumentException"/>, если размер партии меньше 1,
  /// количество контрактов/дней отрицательно, число персонажей в простое вне 0..N, или
  /// активность передана не в ту группу (задание в простой и наоборот).
  /// </summary>
  public static PartyIncomeResult Calculate(PartyIncomeInput input)
  {
    Validate(input);

    var partySize = input.PartySize;
    var coefficient = ResolveCoefficient(partySize, input.Coefficients);

    var questLines = input.Quests
      .Select(x => CalculateQuestLine(x, partySize, coefficient.Coefficient))
      .ToList();

    var downtimeLines = input.Downtime
      .Select(x => CalculateDowntimeLine(x, partySize))
      .ToList();

    var allLines = questLines.Concat(downtimeLines).ToList();

    return new PartyIncomeResult
    {
      Coefficient = coefficient,
      QuestLines = questLines,
      DowntimeLines = downtimeLines,
      PartyTotal = allLines.Aggregate(GoldRange.Zero, (sum, line) => sum + line.Party),
      PerCharacterTotal = allLines.Aggregate(GoldRange.Zero, (sum, line) => sum + line.PerCharacter)
    };
  }

  /// <summary>
  /// Коэффициент для размера партии N: строка с наибольшим размером ≤ N. Для N больше
  /// максимальной заведённой строки это последняя (наибольшая) строка — без экстраполяции.
  /// Если подходящей строки нет (таблица пуста) — 1.0.
  /// </summary>
  public static ResolvedPartySizeCoefficient ResolveCoefficient(int partySize, IReadOnlyList<PartySizeCoefficientSummary> coefficients)
  {
    var row = coefficients
      .Where(x => x.PartySize <= partySize)
      .MaxBy(x => x.PartySize);

    if (row is null)
      return new ResolvedPartySizeCoefficient(1m, PartySizeCoefficientSource.Default, null);

    var source = row.PartySize == partySize ? PartySizeCoefficientSource.Exact : PartySizeCoefficientSource.NearestLower;
    return new ResolvedPartySizeCoefficient(row.Coefficient, source, row.PartySize);
  }

  #endregion

  #region Приватные шаги

  private static IncomeLineResult CalculateQuestLine(QuestContractInput line, int partySize, decimal coefficient)
  {
    var perContractPerCharacter = RateRange(line.Activity) * line.Activity.RecommendedDurationDays * coefficient;
    var perCharacter = perContractPerCharacter * line.ContractCount;
    return new IncomeLineResult(line.Activity, perCharacter, perCharacter * partySize);
  }

  private static IncomeLineResult CalculateDowntimeLine(DowntimeInput line, int partySize)
  {
    var party = RateRange(line.Activity) * line.Days * line.CharacterCount;
    return new IncomeLineResult(line.Activity, party / partySize, party);
  }

  private static GoldRange RateRange(EconomyActivitySummary activity) => new(activity.RateMin, activity.RateMax);

  private static void Validate(PartyIncomeInput input)
  {
    if (input.PartySize < 1)
      throw new ArgumentException("Размер партии должен быть не меньше 1.");

    foreach (var quest in input.Quests)
    {
      if (quest.Activity.ActivityType != EconomyActivityType.Quest)
        throw new ArgumentException($"«{quest.Activity.Category}» — не задание.");
      if (quest.ContractCount < 0)
        throw new ArgumentException("Количество контрактов не может быть отрицательным.");
    }

    foreach (var downtime in input.Downtime)
    {
      if (downtime.Activity.ActivityType != EconomyActivityType.Downtime)
        throw new ArgumentException($"«{downtime.Activity.Category}» — не занятие в простое.");
      if (downtime.Days < 0)
        throw new ArgumentException("Количество дней простоя не может быть отрицательным.");
      if (downtime.CharacterCount < 0 || downtime.CharacterCount > input.PartySize)
        throw new ArgumentException($"Число персонажей в простое должно быть от 0 до размера партии ({input.PartySize}).");
    }
  }

  #endregion
}
