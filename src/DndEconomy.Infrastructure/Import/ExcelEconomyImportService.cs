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
  private const string QuestPayRatesSheetName = "Оплата заданий";

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

  private static readonly Dictionary<string, QuestEpoch> QuestEpochLabels = new()
  {
    ["I"] = QuestEpoch.I,
    ["II"] = QuestEpoch.II,
    ["III"] = QuestEpoch.III,
    ["IV"] = QuestEpoch.IV
  };

  private static readonly Dictionary<string, QuestDangerLevel> QuestDangerLevelLabels = new()
  {
    ["Тривиальная"] = QuestDangerLevel.Trivial,
    ["Лёгкая"] = QuestDangerLevel.Easy,
    ["Стандартная"] = QuestDangerLevel.Standard,
    ["Опасная"] = QuestDangerLevel.Dangerous,
    ["Смертельная"] = QuestDangerLevel.Deadly
  };

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
  public async Task<EconomyImportSummary> ImportItemsAsync(Stream fileStream, bool replaceExisting, bool persist, CancellationToken cancellationToken)
  {
    var summary = new EconomyImportSummary();
    using var workbook = OpenWorkbook(fileStream);

    if (!workbook.Worksheets.TryGetWorksheet(ItemsSheetName, out var sheet))
    {
      summary.Warnings.Add($"В файле не найден лист «{ItemsSheetName}» — ничего не импортировано.");
      return summary;
    }

    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    await ImportItemsAsync(dbContext, sheet, replaceExisting, persist, summary, cancellationToken);

    _logger.LogInformation(
      "Импортирован лист «{Sheet}»: предметов {Items}, удалено {Removed} (persist={Persist})", ItemsSheetName, summary.ItemsImported, summary.ItemsRemoved, persist);
    return summary;
  }

  /// <inheritdoc />
  public async Task<EconomyImportSummary> ImportCitiesAsync(Stream fileStream, bool replaceExisting, bool persist, CancellationToken cancellationToken)
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
    await ImportCitiesAndModifiersAsync(dbContext, sheet, citiesByName, replaceExisting, persist, summary, cancellationToken);

    _logger.LogInformation(
      "Импортирован лист «{Sheet}»: городов {Cities} (удалено {CitiesRemoved}), коэф. города {Mods} (удалено {ModsRemoved}) (persist={Persist})",
      CitiesSheetName, summary.CitiesImported, summary.CitiesRemoved, summary.CityModifiersImported, summary.CityModifiersRemoved, persist);
    return summary;
  }

  /// <inheritdoc />
  public async Task<EconomyImportSummary> ImportSeasonModifiersAsync(Stream fileStream, bool replaceExisting, bool persist, CancellationToken cancellationToken)
  {
    var summary = new EconomyImportSummary();
    using var workbook = OpenWorkbook(fileStream);

    if (!workbook.Worksheets.TryGetWorksheet(SeasonsSheetName, out var sheet))
    {
      summary.Warnings.Add($"В файле не найден лист «{SeasonsSheetName}» — ничего не импортировано.");
      return summary;
    }

    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    await ImportSeasonModifiersAsync(dbContext, sheet, replaceExisting, persist, summary, cancellationToken);

    _logger.LogInformation(
      "Импортирован лист «{Sheet}»: коэф. сезона {Mods}, удалено {Removed} (persist={Persist})", SeasonsSheetName, summary.SeasonModifiersImported, summary.SeasonModifiersRemoved, persist);
    return summary;
  }

  /// <inheritdoc />
  public async Task<EconomyImportSummary> ImportSessionsAsync(Stream fileStream, bool replaceExisting, bool persist, CancellationToken cancellationToken)
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
    await ImportSessionsAsync(dbContext, sheet, citiesByName, replaceExisting, persist, summary, cancellationToken);

    _logger.LogInformation(
      "Импортирован лист «{Sheet}»: сессий {Sessions}, удалено {Removed} (persist={Persist})", SettingsSheetName, summary.SessionsImported, summary.SessionsRemoved, persist);
    return summary;
  }

  /// <inheritdoc />
  public async Task<EconomyImportSummary> ImportCoinAcceptanceAsync(Stream fileStream, bool replaceExisting, bool persist, CancellationToken cancellationToken)
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
    await ImportCoinAcceptanceRowsAsync(dbContext, sheet, citiesByName, replaceExisting, persist, summary, cancellationToken);

    _logger.LogInformation(
      "Импортирован лист «{Sheet}»: приём монет {Count}, удалено {Removed} (persist={Persist})", CoinAcceptanceSheetName, summary.CoinAcceptancesImported, summary.CoinAcceptancesRemoved, persist);
    return summary;
  }

  /// <inheritdoc />
  public async Task<EconomyImportSummary> ImportQuestPayRatesAsync(Stream fileStream, bool replaceExisting, bool persist, CancellationToken cancellationToken)
  {
    var summary = new EconomyImportSummary();
    using var workbook = OpenWorkbook(fileStream);

    if (!workbook.Worksheets.TryGetWorksheet(QuestPayRatesSheetName, out var sheet))
    {
      summary.Warnings.Add($"В файле не найден лист «{QuestPayRatesSheetName}» — ничего не импортировано.");
      return summary;
    }

    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    await ImportQuestPayRatesAsync(dbContext, sheet, persist, summary, cancellationToken);

    _logger.LogInformation("Импортирован лист «{Sheet}»: заданий {Count}, удалено {Removed} (persist={Persist})", QuestPayRatesSheetName, summary.QuestPayRatesImported, summary.QuestPayRatesRemoved, persist);
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

  /// <summary>
  /// Импортирует справочник предметов из листа "Предметы". При <paramref name="replaceExisting"/>
  /// предметы, не встретившиеся в файле, удаляются (каскадно снося их из <c>UserSavedItem</c> —
  /// избранное игроков — и обнуляя <c>ItemRequest.ResultingItemId</c>, та же механика, что и у
  /// ручного удаления одного предмета на /admin/items).
  /// </summary>
  private static async Task ImportItemsAsync(
    ApplicationDbContext dbContext, IXLWorksheet sheet, bool replaceExisting, bool persist, EconomyImportSummary summary, CancellationToken cancellationToken)
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

    var touchedIds = new HashSet<Guid>();

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

      touchedIds.Add(item.Id);
      summary.ItemsImported++;
    }

    if (replaceExisting)
    {
      var toRemove = allItems.Where(x => !touchedIds.Contains(x.Id)).ToList();
      summary.ItemsRemoved = toRemove.Count;
      if (persist)
      {
        dbContext.Items.RemoveRange(toRemove);
      }
    }

    if (persist)
    {
      await dbContext.SaveChangesAsync(cancellationToken);
    }
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
    ApplicationDbContext dbContext, IXLWorksheet sheet, Dictionary<string, City> citiesByName,
    bool replaceExisting, bool persist, EconomyImportSummary summary, CancellationToken cancellationToken)
  {
    await ResolveOrCreateCitiesAsync(dbContext, sheet, citiesByName, replaceExisting, persist, summary, cancellationToken);
    await ImportCityModifierRowsAsync(dbContext, sheet, citiesByName, replaceExisting, persist, summary, cancellationToken);
  }

  /// <summary>
  /// Читает названия городов из строки 1 и их размер из строки 2, создаёт недостающие City.
  /// При <paramref name="replaceExisting"/> города, не встретившиеся в шапке, удаляются —
  /// каскадно снося их же CityModifier/CityCoinAcceptance (настроено на уровне схемы БД) и
  /// обнуляя CityId у сессий, где стоял этот город.
  /// </summary>
  private static async Task ResolveOrCreateCitiesAsync(
    ApplicationDbContext dbContext, IXLWorksheet sheet, Dictionary<string, City> citiesByName,
    bool replaceExisting, bool persist, EconomyImportSummary summary, CancellationToken cancellationToken)
  {
    var headerRow = sheet.Row(1);
    var sizeRow = sheet.Row(2);
    var lastColumn = sheet.LastColumnUsed()!.ColumnNumber();
    var touchedNames = new HashSet<string>();

    // Города начинаются с колонки C (1=Тип, 2=Подтип).
    for (var column = 3; column <= lastColumn; column++)
    {
      var cityName = headerRow.Cell(column).GetString();
      if (string.IsNullOrWhiteSpace(cityName))
      {
        continue;
      }

      touchedNames.Add(cityName);

      if (citiesByName.ContainsKey(cityName))
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

    if (replaceExisting)
    {
      var toRemove = citiesByName.Values.Where(x => !touchedNames.Contains(x.Name)).ToList();
      summary.CitiesRemoved = toRemove.Count;
      if (persist)
      {
        dbContext.Cities.RemoveRange(toRemove);
      }
    }

    if (persist)
    {
      await dbContext.SaveChangesAsync(cancellationToken);
    }
  }

  /// <summary>
  /// Читает строки 3+ (Тип, Подтип, коэффициент по каждому городу) и наполняет CityModifier.
  /// При <paramref name="replaceExisting"/> строки матрицы, не встретившиеся в файле, удаляются
  /// (коэффициент такой пары Тип+Подтип+Город просто вернётся к дефолту 1 — см. <c>AdminCityModifiers.razor</c>).
  /// </summary>
  private static async Task ImportCityModifierRowsAsync(
    ApplicationDbContext dbContext, IXLWorksheet sheet, Dictionary<string, City> citiesByName,
    bool replaceExisting, bool persist, EconomyImportSummary summary, CancellationToken cancellationToken)
  {
    var existingModifiers = await dbContext.CityModifiers.ToListAsync(cancellationToken);
    var headerRow = sheet.Row(1);
    var lastColumn = sheet.LastColumnUsed()!.ColumnNumber();
    var touchedIds = new HashSet<Guid>();

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
          touchedIds.Add(existing.Id);
        }
        else
        {
          var modifier = new CityModifier
          {
            Type = type,
            Subtype = subtype,
            CityId = city.Id,
            Coefficient = coefficient
          };
          dbContext.CityModifiers.Add(modifier);
          touchedIds.Add(modifier.Id);
        }

        summary.CityModifiersImported++;
      }
    }

    if (replaceExisting)
    {
      var toRemove = existingModifiers.Where(x => !touchedIds.Contains(x.Id)).ToList();
      summary.CityModifiersRemoved = toRemove.Count;
      if (persist)
      {
        dbContext.CityModifiers.RemoveRange(toRemove);
      }
    }

    if (persist)
    {
      await dbContext.SaveChangesAsync(cancellationToken);
    }
  }

  #endregion

  #region Импорт листа "Сезонность"

  /// <summary>
  /// Импортирует матрицу коэффициентов "Тип+Подтип × Сезон". При <paramref name="replaceExisting"/>
  /// строки, не встретившиеся в файле, удаляются (коэффициент вернётся к дефолту 1).
  /// </summary>
  private static async Task ImportSeasonModifiersAsync(
    ApplicationDbContext dbContext, IXLWorksheet sheet, bool replaceExisting, bool persist, EconomyImportSummary summary, CancellationToken cancellationToken)
  {
    var headerRow = sheet.Row(1);
    var existingModifiers = await dbContext.SeasonModifiers.ToListAsync(cancellationToken);
    var touchedIds = new HashSet<Guid>();

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
          touchedIds.Add(existing.Id);
        }
        else
        {
          var modifier = new SeasonModifier
          {
            Type = type,
            Subtype = subtype,
            Season = season,
            Coefficient = coefficient
          };
          dbContext.SeasonModifiers.Add(modifier);
          touchedIds.Add(modifier.Id);
        }

        summary.SeasonModifiersImported++;
      }
    }

    if (replaceExisting)
    {
      var toRemove = existingModifiers.Where(x => !touchedIds.Contains(x.Id)).ToList();
      summary.SeasonModifiersRemoved = toRemove.Count;
      if (persist)
      {
        dbContext.SeasonModifiers.RemoveRange(toRemove);
      }
    }

    if (persist)
    {
      await dbContext.SaveChangesAsync(cancellationToken);
    }
  }

  #endregion

  #region Импорт листа "Настройки"

  /// <summary>
  /// Импортирует игровые сессии ("Партии") из листа "Настройки". При <paramref name="replaceExisting"/>
  /// сессии, не встретившиеся в файле, удаляются безвозвратно — если среди них окажется текущая
  /// закреплённая/активная сессия, каталог у игроков сразу переключится на следующую по дате
  /// (или на "Нет в наличии" везде, если сессий не останется совсем).
  /// </summary>
  private static async Task ImportSessionsAsync(
    ApplicationDbContext dbContext, IXLWorksheet sheet, Dictionary<string, City> citiesByName,
    bool replaceExisting, bool persist, EconomyImportSummary summary, CancellationToken cancellationToken)
  {
    var existingSessions = await dbContext.EconomySessions.ToDictionaryAsync(x => x.Name, cancellationToken);
    var touchedNames = new HashSet<string>();

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

      touchedNames.Add(name);
      summary.SessionsImported++;
    }

    if (replaceExisting)
    {
      var toRemove = existingSessions.Values.Where(x => !touchedNames.Contains(x.Name)).ToList();
      summary.SessionsRemoved = toRemove.Count;
      if (persist)
      {
        dbContext.EconomySessions.RemoveRange(toRemove);
      }
    }

    if (persist)
    {
      await dbContext.SaveChangesAsync(cancellationToken);
    }
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
  /// с уже существующими. При <paramref name="replaceExisting"/> удаляются строки матрицы
  /// (пары Номинал+Город), не встретившиеся в файле — доля приёма вернётся к дефолту 1. Заметки
  /// по городам (<c>City.CoinAcceptanceNote</c>) полная замена не трогает — это не строка этой
  /// таблицы, а поле города.
  /// </summary>
  private static async Task ImportCoinAcceptanceRowsAsync(
    ApplicationDbContext dbContext, IXLWorksheet sheet, Dictionary<string, City> citiesByName,
    bool replaceExisting, bool persist, EconomyImportSummary summary, CancellationToken cancellationToken)
  {
    var existingAcceptances = await dbContext.CityCoinAcceptances.ToListAsync(cancellationToken);
    var headerRow = sheet.Row(1);
    var lastColumn = sheet.LastColumnUsed()!.ColumnNumber();
    var touchedIds = new HashSet<Guid>();

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
          touchedIds.Add(existing.Id);
        }
        else
        {
          var acceptance = new CityCoinAcceptance { Denomination = denomination, CityId = city.Id, AcceptanceRate = rate };
          dbContext.CityCoinAcceptances.Add(acceptance);
          touchedIds.Add(acceptance.Id);
        }

        summary.CoinAcceptancesImported++;
      }
    }

    if (replaceExisting)
    {
      var toRemove = existingAcceptances.Where(x => !touchedIds.Contains(x.Id)).ToList();
      summary.CoinAcceptancesRemoved = toRemove.Count;
      if (persist)
      {
        dbContext.CityCoinAcceptances.RemoveRange(toRemove);
      }
    }

    if (persist)
    {
      await dbContext.SaveChangesAsync(cancellationToken);
    }
  }

  #endregion

  #region Импорт листа "Оплата заданий"

  /// <summary>
  /// Импортирует справочник оплаты заданий. В отличие от остальных листов у строк нет
  /// естественного ключа (несколько заданий одной Категории+Опасности+Эпохи — обычное дело,
  /// это не матрица коэффициентов), поэтому загрузка всегда полностью заменяет текущее
  /// содержимое таблицы содержимым листа, а не обновляет по ключу (нет параметра
  /// <c>replaceExisting</c> — это единственный лист, где полная замена не опциональна).
  /// </summary>
  private static async Task ImportQuestPayRatesAsync(ApplicationDbContext dbContext, IXLWorksheet sheet, bool persist, EconomyImportSummary summary, CancellationToken cancellationToken)
  {
    var newRates = new List<QuestPayRate>();

    foreach (var row in sheet.RowsUsed().Skip(1))
    {
      var epochLabel = row.Cell(1).GetString().Trim();
      var category = row.Cell(2).GetString();
      var dangerLabel = row.Cell(3).GetString().Trim();

      if (string.IsNullOrWhiteSpace(category) || !QuestEpochLabels.TryGetValue(epochLabel, out var epoch))
      {
        continue;
      }

      if (!QuestDangerLevelLabels.TryGetValue(dangerLabel, out var dangerLevel))
      {
        summary.Warnings.Add($"Задание «{category}» пропущено — не распознан уровень опасности «{dangerLabel}».");
        continue;
      }

      newRates.Add(new QuestPayRate
      {
        Epoch = epoch,
        Category = category,
        DangerLevel = dangerLevel,
        Description = row.Cell(4).GetString(),
        Duration = row.Cell(5).GetString(),
        PartyPayment = row.Cell(6).GetValue<int>(),
        BalanceNote = row.Cell(7).GetString()
      });

      summary.QuestPayRatesImported++;
    }

    var existingRates = await dbContext.QuestPayRates.ToListAsync(cancellationToken);
    summary.QuestPayRatesRemoved = existingRates.Count;

    if (persist)
    {
      dbContext.QuestPayRates.RemoveRange(existingRates);
      dbContext.QuestPayRates.AddRange(newRates);
      await dbContext.SaveChangesAsync(cancellationToken);
    }
  }

  #endregion
}
