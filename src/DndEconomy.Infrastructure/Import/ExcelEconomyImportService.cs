using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DndEconomy.Application.Import;
using DndEconomy.Domain.Constants;
using DndEconomy.Domain.Entities;
using DndEconomy.Domain.Enums;
using DndEconomy.Infrastructure.Persistence;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DndEconomy.Infrastructure.Import;

/// <summary>
/// Разбирает листы таблицы экономики ("Предметы", "Города", "Сезонность", "Настройки",
/// "Приём монет") и наполняет БД. Каждый лист импортируется отдельным публичным методом —
/// вызывается с профильной страницы админки (например, лист "Сезонность" — со страницы
/// /admin/economy/season-modifiers), которая грузит файл целиком, но использует из него
/// только свой лист, даже если в книге есть остальные (так один и тот же файл-мастер
/// кампании можно скормить любой странице). Повторную загрузку делает безопасной —
/// существующие записи обновляются по ключу, а не дублируются.
/// </summary>
public sealed partial class ExcelEconomyImportService : IExcelEconomyImportService
{
  #region Поля, конструктор, регулярное выражение для разбора названий

  private const string ItemsSheetName = "Предметы";
  private const string CitiesSheetName = "Города";
  private const string SeasonsSheetName = "Сезонность";
  private const string SettingsSheetName = "Настройки";
  private const string CoinAcceptanceSheetName = "Приём монет";

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

  private static readonly Dictionary<string, CoinDenomination> DenominationLabels =
    CoinDenominations.All.ToDictionary(x => x.DisplayName, x => x.Denomination);

  private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
  private readonly ILogger<ExcelEconomyImportService> _logger;

  public ExcelEconomyImportService(IDbContextFactory<ApplicationDbContext> dbContextFactory, ILogger<ExcelEconomyImportService> logger)
  {
    _dbContextFactory = dbContextFactory;
    _logger = logger;
  }

  #endregion

  #region Оркестрация импорта — одна точка входа на лист

  /// <inheritdoc />
  public async Task<EconomyImportSummary> ImportItemsAsync(Stream fileStream, CancellationToken cancellationToken)
  {
    var summary = new EconomyImportSummary();
    using var workbook = OpenWorkbook(fileStream);

    if (!workbook.Worksheets.TryGetWorksheet(ItemsSheetName, out var sheet))
    {
      summary.Warnings.Add($"В файле не найден лист «{ItemsSheetName}» — ничего не импортировано.");
      return summary;
    }

    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    await ImportItemsAsync(dbContext, sheet, summary, cancellationToken);

    _logger.LogInformation("Импортирован лист «{Sheet}»: предметов {Items}", ItemsSheetName, summary.ItemsImported);
    return summary;
  }

  /// <inheritdoc />
  public async Task<EconomyImportSummary> ImportCitiesAsync(Stream fileStream, CancellationToken cancellationToken)
  {
    var summary = new EconomyImportSummary();
    using var workbook = OpenWorkbook(fileStream);

    if (!workbook.Worksheets.TryGetWorksheet(CitiesSheetName, out var sheet))
    {
      summary.Warnings.Add($"В файле не найден лист «{CitiesSheetName}» — ничего не импортировано.");
      return summary;
    }

    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    var citiesByName = await dbContext.Cities.ToDictionaryAsync(x => x.Name, cancellationToken);
    await ImportCitiesAndModifiersAsync(dbContext, sheet, citiesByName, summary, cancellationToken);

    _logger.LogInformation(
      "Импортирован лист «{Sheet}»: городов {Cities}, коэф. города {Mods}", CitiesSheetName, summary.CitiesImported, summary.CityModifiersImported);
    return summary;
  }

  /// <inheritdoc />
  public async Task<EconomyImportSummary> ImportSeasonModifiersAsync(Stream fileStream, CancellationToken cancellationToken)
  {
    var summary = new EconomyImportSummary();
    using var workbook = OpenWorkbook(fileStream);

    if (!workbook.Worksheets.TryGetWorksheet(SeasonsSheetName, out var sheet))
    {
      summary.Warnings.Add($"В файле не найден лист «{SeasonsSheetName}» — ничего не импортировано.");
      return summary;
    }

    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    await ImportSeasonModifiersAsync(dbContext, sheet, summary, cancellationToken);

    _logger.LogInformation("Импортирован лист «{Sheet}»: коэф. сезона {Mods}", SeasonsSheetName, summary.SeasonModifiersImported);
    return summary;
  }

  /// <inheritdoc />
  public async Task<EconomyImportSummary> ImportSessionsAsync(Stream fileStream, CancellationToken cancellationToken)
  {
    var summary = new EconomyImportSummary();
    using var workbook = OpenWorkbook(fileStream);

    if (!workbook.Worksheets.TryGetWorksheet(SettingsSheetName, out var sheet))
    {
      summary.Warnings.Add($"В файле не найден лист «{SettingsSheetName}» — ничего не импортировано.");
      return summary;
    }

    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    var citiesByName = await dbContext.Cities.ToDictionaryAsync(x => x.Name, cancellationToken);
    await ImportSessionsAsync(dbContext, sheet, citiesByName, summary, cancellationToken);

    _logger.LogInformation("Импортирован лист «{Sheet}»: сессий {Sessions}", SettingsSheetName, summary.SessionsImported);
    return summary;
  }

  /// <inheritdoc />
  public async Task<EconomyImportSummary> ImportCoinAcceptanceAsync(Stream fileStream, CancellationToken cancellationToken)
  {
    var summary = new EconomyImportSummary();
    using var workbook = OpenWorkbook(fileStream);

    if (!workbook.Worksheets.TryGetWorksheet(CoinAcceptanceSheetName, out var sheet))
    {
      summary.Warnings.Add($"В файле не найден лист «{CoinAcceptanceSheetName}» — ничего не импортировано.");
      return summary;
    }

    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    var citiesByName = await dbContext.Cities.ToDictionaryAsync(x => x.Name, cancellationToken);
    await ImportCoinAcceptanceRowsAsync(dbContext, sheet, citiesByName, summary, cancellationToken);

    _logger.LogInformation("Импортирован лист «{Sheet}»: приём монет {Count}", CoinAcceptanceSheetName, summary.CoinAcceptancesImported);
    return summary;
  }

  /// <summary>Открывает книгу после обхода бага ClosedXML #1772 (см. <see cref="StripLegacyComments"/>).</summary>
  private static XLWorkbook OpenWorkbook(Stream fileStream)
  {
    using var sanitizedStream = StripLegacyComments(fileStream);
    return new XLWorkbook(sanitizedStream);
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
  /// Новые/существующие города добавляются в переданный словарь Имя города → сущность.
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

  #region Импорт листа "Приём монет"

  /// <summary>
  /// Импортирует матрицу приёма номиналов "Номинал × Город" (строка 1 — города начиная с
  /// колонки B, колонка A — подпись "Номинал"; последующие строки — 5 номиналов по
  /// отображаемому имени из <see cref="CoinDenominations.All"/>) и заметки по городам —
  /// двухколоночную таблицу "Город"/"Заметка", идущую после матрицы (начало таблицы ищется
  /// по строке, где колонка A содержит "Город", в ходе того же однопроходного обхода листа —
  /// НЕ повторным запросом <c>RowsUsed().Skip(notesHeaderRow.RowNumber())</c>, как было раньше:
  /// это теряло заметку первого города, потому что пустая строка-разделитель между матрицей
  /// и таблицей заметок не входит в RowsUsed(), а RowNumber() — абсолютный номер строки листа,
  /// а не порядковый номер в "сжатой" (без пустых строк) последовательности RowsUsed() — они
  /// расходятся на количество пропущенных пустых строк перед найденной, и Skip() пропускал на
  /// одну строку больше, чем нужно). Города не создаются этим листом — сопоставляются по имени
  /// с уже существующими.
  /// </summary>
  private static async Task ImportCoinAcceptanceRowsAsync(
    ApplicationDbContext dbContext, IXLWorksheet sheet, Dictionary<string, City> citiesByName, EconomyImportSummary summary, CancellationToken cancellationToken)
  {
    var existingAcceptances = await dbContext.CityCoinAcceptances.ToListAsync(cancellationToken);
    var headerRow = sheet.Row(1);
    var lastColumn = sheet.LastColumnUsed()!.ColumnNumber();

    var readingNotes = false;

    foreach (var row in sheet.RowsUsed().Skip(1))
    {
      var label = row.Cell(1).GetString().Trim();

      if (!readingNotes && label.Equals("Город", StringComparison.OrdinalIgnoreCase))
      {
        readingNotes = true;
        continue;
      }

      if (readingNotes)
      {
        var cityName = row.Cell(1).GetString();
        if (string.IsNullOrWhiteSpace(cityName))
        {
          continue;
        }

        if (!citiesByName.TryGetValue(cityName, out var city))
        {
          summary.Warnings.Add($"Заметка для города «{cityName}» пропущена — город не найден.");
          continue;
        }

        var note = row.Cell(2).GetString();
        city.CoinAcceptanceNote = string.IsNullOrWhiteSpace(note) ? null : note;
        city.UpdatedAtUtc = DateTime.UtcNow;
        continue;
      }

      if (!DenominationLabels.TryGetValue(label, out var denomination))
      {
        continue;
      }

      for (var column = 2; column <= lastColumn; column++)
      {
        var cityName = headerRow.Cell(column).GetString();
        if (string.IsNullOrWhiteSpace(cityName) || !citiesByName.TryGetValue(cityName, out var city))
        {
          continue;
        }

        var rate = row.Cell(column).GetValue<decimal>();
        var existing = existingAcceptances.SingleOrDefault(x => x.Denomination == denomination && x.CityId == city.Id);

        if (existing is not null)
        {
          existing.AcceptanceRate = rate;
          existing.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
          dbContext.CityCoinAcceptances.Add(new CityCoinAcceptance { Denomination = denomination, CityId = city.Id, AcceptanceRate = rate });
        }

        summary.CoinAcceptancesImported++;
      }
    }

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  #endregion
}
