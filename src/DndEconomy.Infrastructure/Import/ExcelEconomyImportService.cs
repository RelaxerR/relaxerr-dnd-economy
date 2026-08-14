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
/// Разбирает таблицу экономики (листы "Предметы", "Города", "Сезонность", "Настройки") и
/// наполняет БД. Повторную загрузку того же файла делает безопасной — существующие записи
/// обновляются по ключу, а не дублируются. Все четыре листа не обязаны быть в одном файле —
/// каждый лист импортируется независимо, если он присутствует в загруженной книге, поэтому
/// админ может грузить как один общий файл, так и четыре отдельных (по одному листу в каждом),
/// см. шаблоны в /templates.
/// </summary>
public sealed partial class ExcelEconomyImportService : IExcelEconomyImportService
{
  #region Поля, конструктор, регулярное выражение для разбора названий

  private const string ItemsSheetName = "Предметы";
  private const string CitiesSheetName = "Города";
  private const string SeasonsSheetName = "Сезонность";
  private const string SettingsSheetName = "Настройки";

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
    _logger.LogInformation("Начат импорт таблицы экономики");

    using var workbook = new XLWorkbook(fileStream);
    var summary = new EconomyImportSummary();
    var recognizedAnySheet = false;

    // Один DbContext на весь импорт — шаги последовательны (без параллельных await'ов на разных
    // сущностях), и городам/сессиям из поздних листов нужны Id городов/сессий, созданных на ранних.
    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

    if (workbook.Worksheets.TryGetWorksheet(ItemsSheetName, out var itemsSheet))
    {
      recognizedAnySheet = true;
      await ImportItemsAsync(dbContext, itemsSheet, summary, cancellationToken);
    }

    // Города грузим из БД независимо от того, есть ли лист "Города" в ЭТОМ файле — они нужны
    // листу "Настройки" (сессия ссылается на город по имени), а листы могут приходить разными
    // файлами по отдельности (см. шаблоны в /templates).
    var citiesByName = await dbContext.Cities.ToDictionaryAsync(x => x.Name, cancellationToken);

    if (workbook.Worksheets.TryGetWorksheet(CitiesSheetName, out var citiesSheet))
    {
      recognizedAnySheet = true;
      await ImportCitiesAndModifiersAsync(dbContext, citiesSheet, citiesByName, summary, cancellationToken);
    }

    if (workbook.Worksheets.TryGetWorksheet(SeasonsSheetName, out var seasonsSheet))
    {
      recognizedAnySheet = true;
      await ImportSeasonModifiersAsync(dbContext, seasonsSheet, summary, cancellationToken);
    }

    if (workbook.Worksheets.TryGetWorksheet(SettingsSheetName, out var settingsSheet))
    {
      recognizedAnySheet = true;
      await ImportSessionsAsync(dbContext, settingsSheet, citiesByName, summary, cancellationToken);
    }

    if (!recognizedAnySheet)
    {
      summary.Warnings.Add(
        $"В файле не найдено ни одного из ожидаемых листов ({ItemsSheetName} / {CitiesSheetName} / " +
        $"{SeasonsSheetName} / {SettingsSheetName}) — ничего не импортировано.");
    }

    _logger.LogInformation(
      "Импорт завершён: предметов {Items}, городов {Cities}, модификаторов города {CityMods}, модификаторов сезона {SeasonMods}, сессий {Sessions}",
      summary.ItemsImported, summary.CitiesImported, summary.CityModifiersImported, summary.SeasonModifiersImported, summary.SessionsImported);

    return summary;
  }

  #endregion

  #region Импорт листа "Предметы"

  /// <summary>Импортирует справочник предметов из листа "Предметы".</summary>
  private static async Task ImportItemsAsync(ApplicationDbContext dbContext, IXLWorksheet sheet, EconomyImportSummary summary, CancellationToken cancellationToken)
  {
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

      var isNewItem = !existingByUuid.TryGetValue(externalUuid, out var item);
      item ??= new Item { ExternalUuid = externalUuid };

      item.Category = row.Cell(1).GetString();
      item.Type = row.Cell(2).GetString();
      item.Subtype = row.Cell(3).GetString();
      item.NameRu = nameRu;
      item.NameEn = nameEn;
      item.BaseCost = row.Cell(5).GetValue<decimal>();
      item.Weight = row.Cell(6).GetValue<decimal>();
      item.UpdatedAtUtc = DateTime.UtcNow;

      // Item.Id получает Guid.NewGuid() уже в конструкторе (AuditableEntity) — проверка
      // "Id == default" здесь не сработала бы никогда, поэтому отслеживаем "новый ли объект"
      // явно через TryGetValue, а не через состояние Id.
      if (isNewItem)
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
  /// Новые/существующие города добавляются в переданный словарь Имя города → сущность —
  /// тот же словарь затем используется при импорте листа "Настройки" (в этом же файле или
  /// в отдельном, загруженном следующим).
  /// </summary>
  private static async Task ImportCitiesAndModifiersAsync(
    ApplicationDbContext dbContext, IXLWorksheet sheet, Dictionary<string, City> citiesByName, EconomyImportSummary summary, CancellationToken cancellationToken)
  {
    await ResolveOrCreateCitiesAsync(dbContext, sheet, citiesByName, summary, cancellationToken);
    await ImportCityModifierRowsAsync(dbContext, sheet, citiesByName, summary, cancellationToken);
  }

  /// <summary>Читает названия городов из строки 1 и их размер из строки 2, создаёт недостающие City.</summary>
  private static async Task ResolveOrCreateCitiesAsync(
    ApplicationDbContext dbContext, IXLWorksheet sheet, Dictionary<string, City> citiesByName, EconomyImportSummary summary, CancellationToken cancellationToken)
  {
    var headerRow = sheet.Row(1);
    var sizeRow = sheet.Row(2);
    var lastColumn = sheet.LastColumnUsed()!.ColumnNumber();

    // Города начинаются с колонки C (1=Тип, 2=Подтип).
    for (var column = 3; column <= lastColumn; column++)
    {
      var cityName = headerRow.Cell(column).GetString();
      if (string.IsNullOrWhiteSpace(cityName) || citiesByName.ContainsKey(cityName))
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
      citiesByName[cityName] = city;
      summary.CitiesImported++;
    }

    await dbContext.SaveChangesAsync(cancellationToken);
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
  private static async Task ImportSeasonModifiersAsync(ApplicationDbContext dbContext, IXLWorksheet sheet, EconomyImportSummary summary, CancellationToken cancellationToken)
  {
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
    ApplicationDbContext dbContext, IXLWorksheet sheet, Dictionary<string, City> citiesByName, EconomyImportSummary summary, CancellationToken cancellationToken)
  {
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

      // EconomySession.Id получает Guid.NewGuid() уже в конструкторе (AuditableEntity), поэтому
      // "новизну" определяем только по наличию в словаре существующих сессий, а не по Id.
      if (!existingSessions.ContainsKey(name))
      {
        dbContext.EconomySessions.Add(session);
      }

      summary.SessionsImported++;
    }

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  #endregion
}
