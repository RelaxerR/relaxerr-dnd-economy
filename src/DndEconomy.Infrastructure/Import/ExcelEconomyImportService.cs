using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DndEconomy.Application.Import;
using DndEconomy.Domain.Entities;
using DndEconomy.Domain.Enums;
using DndEconomy.Infrastructure.Persistence;
using DocumentFormat.OpenXml.Packaging;
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

    using var sanitizedStream = StripLegacyComments(fileStream);
    using var workbook = new XLWorkbook(sanitizedStream);
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

  /// <summary>
  /// Вырезает из книги старые VML cell-комментарии (заметки) перед тем, как её увидит ClosedXML.
  /// Мастер-таблица кампании ("Задание точно финал N.xlsx") сделана не в настоящем Excel (шрифт
  /// "Helvetica Neue", пространство имён macVmlSchemaUri в VML), и такие редакторы не пишут
  /// обязательный для Excel элемент &lt;v:textbox&gt; внутри фигуры комментария. ClosedXML
  /// 0.105.1 не умеет с этим работать — падает на первом же комментарии с "Sequence contains no
  /// matching element" (открытый и не исправленный годами баг библиотеки,
  /// github.com/ClosedXML/ClosedXML/issues/1772), то есть открыть такую книгу не получится вообще
  /// ни при каком составе листов. Импорт нигде не читает текст комментариев, поэтому их можно
  /// целиком выбросить средствами Open XML SDK (который открывает файл нормально — ломается
  /// именно построчный разбор ClosedXML) — так админу не нужно вручную чистить комментарии в
  /// мастер-файле перед каждой перезаливкой.
  /// </summary>
  private static Stream StripLegacyComments(Stream fileStream)
  {
    var buffer = new MemoryStream();
    fileStream.CopyTo(buffer);
    buffer.Position = 0;

    using (var document = SpreadsheetDocument.Open(buffer, isEditable: true))
    {
      foreach (var worksheetPart in document.WorkbookPart!.WorksheetParts)
      {
        var hadLegacyComments = false;

        if (worksheetPart.WorksheetCommentsPart is { } commentsPart)
        {
          worksheetPart.DeletePart(commentsPart);
          hadLegacyComments = true;
        }

        foreach (var vmlPart in worksheetPart.VmlDrawingParts.ToList())
        {
          worksheetPart.DeletePart(vmlPart);
          hadLegacyComments = true;
        }

        if (hadLegacyComments)
        {
          worksheetPart.Worksheet.RemoveAllChildren<DocumentFormat.OpenXml.Spreadsheet.LegacyDrawing>();
          worksheetPart.Worksheet.Save();
        }
      }
    }

    buffer.Position = 0;
    return buffer;
  }

  #endregion

  #region Импорт листа "Предметы"

  /// <summary>Импортирует справочник предметов из листа "Предметы".</summary>
  private static async Task ImportItemsAsync(ApplicationDbContext dbContext, IXLWorksheet sheet, EconomyImportSummary summary, CancellationToken cancellationToken)
  {
    // GetString() у пустой ячейки возвращает "", а не null — раньше это попадало в словарь как
    // ключ ExternalUuid == "" для КАЖДОГО предмета без UUID (их в исходнике большинство). Пустая
    // строка проходила фильтр "!= null", и на любой второй загрузке ToDictionaryAsync падал с
    // "An item with the same key has already been added", как только предметов без UUID
    // набиралось больше одного. Пустые/пробельные значения теперь приравниваются к null.
    // Предметы без UUID сопоставляются по составному ключу (Категория, Тип, Подтип, NameRu) —
    // тому же, по которому они уникальны в исходной Excel-модели — иначе повторная загрузка
    // такого предмета создавала бы новый дубликат при каждой загрузке (что и произошло: в БД
    // уже была пара дублей вроде "Резная статуэтка" × 2 до этого фикса).
    var allItems = await dbContext.Items.ToListAsync(cancellationToken);

    var existingByUuid = allItems
      .Where(x => !string.IsNullOrEmpty(x.ExternalUuid))
      .GroupBy(x => x.ExternalUuid!)
      .ToDictionary(g => g.Key, g => g.First());

    var existingByComposite = allItems
      .GroupBy(x => (x.Category, x.Type, x.Subtype, x.NameRu))
      .ToDictionary(g => g.Key, g => g.First());

    foreach (var row in sheet.RowsUsed().Skip(1))
    {
      var rawName = row.Cell(4).GetString();
      if (string.IsNullOrWhiteSpace(rawName))
      {
        continue;
      }

      var (nameRu, nameEn) = SplitItemName(rawName);
      var rawExternalUuid = row.Cell(7).GetString();
      var externalUuid = string.IsNullOrWhiteSpace(rawExternalUuid) ? null : rawExternalUuid.Trim();
      var category = row.Cell(1).GetString();
      var type = row.Cell(2).GetString();
      var subtype = row.Cell(3).GetString();

      var item = (externalUuid is not null && existingByUuid.TryGetValue(externalUuid, out var byUuid))
        ? byUuid
        : existingByComposite.GetValueOrDefault((category, type, subtype, nameRu));
      var isNewItem = item is null;
      item ??= new Item { ExternalUuid = externalUuid };

      item.Category = category;
      item.Type = type;
      item.Subtype = subtype;
      item.NameRu = nameRu;
      item.NameEn = nameEn;
      item.BaseCost = row.Cell(5).GetValue<decimal>();
      item.Weight = row.Cell(6).GetValue<decimal>();
      item.UpdatedAtUtc = DateTime.UtcNow;

      // Не затираем уже сохранённый UUID пустым значением, если в этой загрузке колонка
      // "UUID (Foundry)" для найденного по составному ключу предмета вдруг оказалась пустой.
      if (externalUuid is not null)
      {
        item.ExternalUuid = externalUuid;
      }

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
