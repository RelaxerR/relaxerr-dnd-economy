using DndEconomy.Domain.Enums;

namespace DndEconomy.Application.Pricing;

/// <summary>Условие приёма одного номинала монеты в городе — см. <see cref="ICityCoinAcceptanceReadStore"/>.</summary>
public sealed record CoinAcceptanceRate
{
  public required CoinDenomination Denomination { get; init; }

  /// <summary>1 — принимается по номиналу без скидки, 0 &lt; x &lt; 1 — со скидкой, 0 — отказ.</summary>
  public required decimal AcceptanceRate { get; init; }
}

/// <summary>Условия приёма всех номиналов в одном городе — для отображения игрокам в каталоге.</summary>
public sealed class CityCoinAcceptanceInfo
{
  /// <summary>Все 5 номиналов, с умолчанием 1 для тех, для которых явной строки в БД нет.</summary>
  public required IReadOnlyList<CoinAcceptanceRate> Denominations { get; init; }

  /// <summary>Свободный текст с нюансами приёма, не сводимыми к номиналу (City.CoinAcceptanceNote).</summary>
  public string? Note { get; init; }
}

/// <summary>
/// Узкий read-only порт для чтения условий приёма номиналов монет игроками — по тому же
/// приёму, что и <see cref="IEconomyPricingReadStore"/> (реализация в Infrastructure через
/// прямые запросы к EF Core, здесь только контракт).
/// </summary>
public interface ICityCoinAcceptanceReadStore
{
  /// <summary>Возвращает условия приёма для указанного города (всегда все 5 номиналов).</summary>
  Task<CityCoinAcceptanceInfo> GetForCityAsync(Guid cityId, CancellationToken cancellationToken);
}
