using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DndEconomy.Application.Import;
using DndEconomy.Domain.Entities;
using DndEconomy.Domain.Enums;
using DndEconomy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DndEconomy.Infrastructure.Import;

/// <summary>
/// Разбирает исходную таблицу экономики (листы "Исходник ДнД", "Города", "Сезонность",
/// "Настройки") и наполняет БД. Повторную загрузку того же файла делает безопасной —
/// существующие записи обновляются по ключу, а не дублируются.
/// </summary>
public sealed partial class ExcelEconomyImportService : IExcelEconomyImportService
{
  #region Поля, конструктор, регулярное выражение для разбора названий

  // Формат названия в исходнике: "Русское название [English Name]" — English опционален.
  [GeneratedRegex(@"^(?<ru>.+?)\s*(\[(?<en>.+)\])?$")]
  private static partial Regex ItemNamePattern();

  private static readonly Dictionary<string, CitySize> CitySizeLabels = new()
  {
    ["Крупный"] = CitySize.Metropolis,
    ["Город"] = CitySize.Town,
    ["Деревня"] = CitySize.Village
  };

  private static readonly Dictionary<string, Season> SeasonLabels = new()
  {
    ["Весна"] = Season.Spring,
    ["Лето"] = Season.Summer,
    ["Осень"] = Season.Autumn,
    ["Зима"] = Season.Winter
  };

  private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
  private readonly ILogger<ExcelEconomyImportService> _logger;

  public ExcelEconomyImportService(IDbContextFactory<ApplicationDbContext> dbContextFactory, ILogger<ExcelEconomyImportService> logger)
  {
    _dbContextFactory = dbContextFactory;
    _logger = logger;
  }

  #endregion

  #region Оркестрация импорта

  /// <inheritdoc />
  public async Task<EconomyImportSummary> ImportAsync(Stream fileStream, CancellationToken cancellationToken)
  {
    _logger.LogInformation("Начат импорт исходной таблицы экономики");

    using var workbook = new XLWorkbook(fileStream);
    var summary = new EconomyImportSummary();

    // Один DbContext на весь импорт — шаги последовательны (без параллельных await'ов на разных
    // сущностях), и городам/сессиям из поздних листов нужны Id городов/сессий, созданных на ранних.
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    await ImportItemsAsync(dbContext, workbook, summary, cancellationToken);
    var citiesByName = await ImportCitiesAndModifiersAsync(dbContext, workbook, summary, cancellationToken);
    await ImportSeasonModifiersAsync(dbContext, workbook, summary, cancellationToken);
    await ImportSessionsAsync(dbContext, workbook, citiesByName, summary, cancellationToken);

    _logger.LogInformation(
      "Импорт завершён: предметов {Items}, городов {Cities}, модификаторов города {CityMods}, модификаторов сезона {SeasonMods}, сессий {Sessions}",
      summary.ItemsImported, summary.CitiesImported, summary.CityModifiersImported, summary.SeasonModifiersImported, summary.SessionsImported);

    return summary;
  }

  #endregion

  #region Импорт листа "Исходник ДнД"

  /// <summary>Импортирует справочник предметов из листа "Исходник ДнД".</summary>
  private static async Task ImportItemsAsync(ApplicationDbContext dbContext, XLWorkbook workbook, EconomyImportSummary summary, CancellationToken cancellationToken)
  {
    var sheet = workbook.Worksheet("Исходник ДнД");
    var existingByUuid = await dbContext.Items
      .Where(x => x.ExternalUuid != null)
      .ToDictionaryAsync(x => x.ExternalUuid!, cancellationToken);

    foreach (var row in sheet.RowsUsed().Skip(1))
    {
      var rawName = row.Cell(4).GetString();
      if (string.IsNullOrWhiteSpace(rawName))
      {
        continue;
      }

      var (nameRu, nameEn) = SplitItemName(rawName);
      var externalUuid = row.Cell(7).GetString();

      var item = existingByUuid.GetValueOrDefault(externalUuid) ?? new Item { ExternalUuid = externalUuid };

      item.Category = row.Cell(1).GetString();
      item.Type = row.Cell(2).GetString();
      item.Subtype = row.Cell(3).GetString();
      item.NameRu = nameRu;
      item.NameEn = nameEn;
      item.BaseCost = row.Cell(5).GetValue<decimal>();
      item.Weight = row.Cell(6).GetValue<decimal>();
      item.UpdatedAtUtc = DateTime.UtcNow;

      if (item.Id == default)
      {
        dbContext.Items.Add(item);
      }

      summary.ItemsImported++;
    }

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  /// <summary>Разбивает строку вида "Название [English]" на русскую и английскую части.</summary>
  private static (string NameRu, string? NameEn) SplitItemName(string rawName)
  {
    var match = ItemNamePattern().Match(rawName.Trim());
    var ru = match.Success ? match.Groups["ru"].Value.Trim() : rawName.Trim();
    var en = match.Success && match.Groups["en"].Success ? match.Groups["en"].Value.Trim() : null;
    return (ru, en);
  }

  #endregion

  #region Импорт листа "Города"

  /// <summary>
  /// Импортирует города (шапка листа) и матрицу коэффициентов "Тип+Подтип × Город".
  /// Возвращает словарь Имя города → сущность, используемый затем при импорте сессий.
  /// </summary>
  private static async Task<Dictionary<string, City>> ImportCitiesAndModifiersAsync(
    ApplicationDbContext dbContext, XLWorkbook workbook, EconomyImportSummary summary, CancellationToken cancellationToken)
  {
    var sheet = workbook.Worksheet("Города");
    var citiesByName = await ResolveOrCreateCitiesAsync(dbContext, sheet, summary, cancellationToken);
    await ImportCityModifierRowsAsync(dbContext, sheet, citiesByName, summary, cancellationToken);
    return citiesByName;
  }

  /// <summary>Читает названия городов из строки 1 и их размер из строки 2, создаёт недостающие City.</summary>
  private static async Task<Dictionary<string, City>> ResolveOrCreateCitiesAsync(
    ApplicationDbContext dbContext, IXLWorksheet sheet, EconomyImportSummary summary, CancellationToken cancellationToken)
  {
    var existingCities = await dbContext.Cities.ToDictionaryAsync(x => x.Name, cancellationToken);
    var headerRow = sheet.Row(1);
    var sizeRow = sheet.Row(2);
    var lastColumn = sheet.LastColumnUsed()!.ColumnNumber();

    // Города начинаются с колонки C (1=Тип, 2=Подтип).
    for (var column = 3; column <= lastColumn; column++)
    {
      var cityName = headerRow.Cell(column).GetString();
      if (string.IsNullOrWhiteSpace(cityName) || existingCities.ContainsKey(cityName))
      {
        continue;
      }

      var sizeLabel = sizeRow.Cell(column).GetString();
      var city = new City
      {
        Name = cityName,
        Size = CitySizeLabels.GetValueOrDefault(sizeLabel, CitySize.Town)
      };

      dbContext.Cities.Add(city);
      existingCities[cityName] = city;
      summary.CitiesImported++;
    }

    await dbContext.SaveChangesAsync(cancellationToken);
    return existingCities;
  }

  /// <summary>Читает строки 3+ (Тип, Подтип, коэффициент по каждому городу) и наполняет CityModifier.</summary>
  private static async Task ImportCityModifierRowsAsync(
    ApplicationDbContext dbContext, IXLWorksheet sheet, Dictionary<string, City> citiesByName, EconomyImportSummary summary, CancellationToken cancellationToken)
  {
    var existingModifiers = await dbContext.CityModifiers.ToListAsync(cancellationToken);
    var headerRow = sheet.Row(1);
    var lastColumn = sheet.LastColumnUsed()!.ColumnNumber();

    foreach (var row in sheet.RowsUsed().Skip(2))
    {
      var type = row.Cell(1).GetString();
      var subtype = row.Cell(2).GetString();
      if (string.IsNullOrWhiteSpace(type))
      {
        continue;
      }

      for (var column = 3; column <= lastColumn; column++)
      {
        var cityName = headerRow.Cell(column).GetString();
        if (string.IsNullOrWhiteSpace(cityName) || !citiesByName.TryGetValue(cityName, out var city))
        {
          continue;
        }

        var coefficient = row.Cell(column).GetValue<decimal>();
        var existing = existingModifiers.SingleOrDefault(
          x => x.Type == type && x.Subtype == subtype && x.CityId == city.Id);

        if (existing is not null)
        {
          existing.Coefficient = coefficient;
          existing.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
          dbContext.CityModifiers.Add(new CityModifier
          {
            Type = type,
            Subtype = subtype,
            CityId = city.Id,
            Coefficient = coefficient
          });
        }

        summary.CityModifiersImported++;
      }
    }

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  #endregion

  #region Импорт листа "Сезонность"

  /// <summary>Импортирует матрицу коэффициентов "Тип+Подтип × Сезон".</summary>
  private static async Task ImportSeasonModifiersAsync(ApplicationDbContext dbContext, XLWorkbook workbook, EconomyImportSummary summary, CancellationToken cancellationToken)
  {
    var sheet = workbook.Worksheet("Сезонность");
    var headerRow = sheet.Row(1);
    var existingModifiers = await dbContext.SeasonModifiers.ToListAsync(cancellationToken);

    foreach (var row in sheet.RowsUsed().Skip(1))
    {
      var type = row.Cell(1).GetString();
      var subtype = row.Cell(2).GetString();
      if (string.IsNullOrWhiteSpace(type))
      {
        continue;
      }

      // Сезоны занимают колонки 3–6 (Весна/Лето/Осень/Зима).
      for (var column = 3; column <= 6; column++)
      {
        var seasonLabel = headerRow.Cell(column).GetString();
        if (!SeasonLabels.TryGetValue(seasonLabel, out var season))
        {
          continue;
        }

        var coefficient = row.Cell(column).GetValue<decimal>();
        var existing = existingModifiers.SingleOrDefault(
          x => x.Type == type && x.Subtype == subtype && x.Season == season);

        if (existing is not null)
        {
          existing.Coefficient = coefficient;
          existing.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
          dbContext.SeasonModifiers.Add(new SeasonModifier
          {
            Type = type,
            Subtype = subtype,
            Season = season,
            Coefficient = coefficient
          });
        }

        summary.SeasonModifiersImported++;
      }
    }

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  #endregion

  #region Импорт листа "Настройки"

  /// <summary>Импортирует игровые сессии ("Партии") из листа "Настройки".</summary>
  private static async Task ImportSessionsAsync(
    ApplicationDbContext dbContext, XLWorkbook workbook, Dictionary<string, City> citiesByName, EconomyImportSummary summary, CancellationToken cancellationToken)
  {
    var sheet = workbook.Worksheet("Настройки");
    var existingSessions = await dbContext.EconomySessions.ToDictionaryAsync(x => x.Name, cancellationToken);

    foreach (var row in sheet.RowsUsed().Skip(1))
    {
      var name = row.Cell(1).GetString();
      var realDateCell = row.Cell(3);
      if (string.IsNullOrWhiteSpace(name) || realDateCell.IsEmpty())
      {
        summary.Warnings.Add($"Партия '{name}' пропущена — не заполнена дата, коэффициенты не привязаны к активному периоду.");
        continue;
      }

      var session = existingSessions.GetValueOrDefault(name) ?? new EconomySession { Name = name };
      session.Description = row.Cell(2).GetString();
      session.RealDate = DateOnly.FromDateTime(realDateCell.GetDateTime());
      session.GameDateLabel = row.Cell(4).GetString();

      var cityName = row.Cell(5).GetString();
      session.CityId = citiesByName.TryGetValue(cityName, out var city) ? city.Id : null;

      var seasonLabel = row.Cell(6).GetString();
      session.Season = SeasonLabels.GetValueOrDefault(seasonLabel, Season.Spring);

      session.BaseCoefficient = row.Cell(7).GetValue<decimal>();
      session.SellCoefficient = row.Cell(8).GetValue<decimal>();
      session.UpdatedAtUtc = DateTime.UtcNow;

      if (session.Id == default || !existingSessions.ContainsKey(name))
      {
        dbContext.EconomySessions.Add(session);
      }

      summary.SessionsImported++;
    }

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  #endregion
}
