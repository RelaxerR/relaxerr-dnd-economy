using ClosedXML.Excel;
using DndEconomy.Application.Economy;
using DndEconomy.Application.Import;
using DndEconomy.Application.QuestPay;
using DndEconomy.Domain.Constants;
using DndEconomy.Domain.Enums;

namespace DndEconomy.Infrastructure.Import;

/// <inheritdoc cref="IExcelEconomyExportService" />
public sealed class ExcelEconomyExportService : IExcelEconomyExportService
{
  #region Поля, конструктор, справочные подписи

  private const string CitiesSheetName = "Города";
  private const string SeasonsSheetName = "Сезонность";
  private const string SettingsSheetName = "Настройки";
  private const string CoinAcceptanceSheetName = "Приём монет";
  private const string QuestPayRatesSheetName = "Оплата заданий";

  private static readonly Dictionary<CitySize, string> CitySizeLabels = new()
  {
    [CitySize.Metropolis] = "Крупный",
    [CitySize.Town] = "Город",
    [CitySize.Village] = "Деревня"
  };

  private static readonly Dictionary<Season, string> SeasonLabels = new()
  {
    [Season.Spring] = "Весна",
    [Season.Summer] = "Лето",
    [Season.Autumn] = "Осень",
    [Season.Winter] = "Зима"
  };

  private static readonly Season[] SeasonOrder = [Season.Spring, Season.Summer, Season.Autumn, Season.Winter];

  private static readonly Dictionary<QuestEpoch, string> QuestEpochLabels = new()
  {
    [QuestEpoch.I] = "I",
    [QuestEpoch.II] = "II",
    [QuestEpoch.III] = "III",
    [QuestEpoch.IV] = "IV"
  };

  private static readonly Dictionary<QuestDangerLevel, string> QuestDangerLevelLabels = new()
  {
    [QuestDangerLevel.Trivial] = "Тривиальная",
    [QuestDangerLevel.Easy] = "Лёгкая",
    [QuestDangerLevel.Standard] = "Стандартная",
    [QuestDangerLevel.Dangerous] = "Опасная",
    [QuestDangerLevel.Deadly] = "Смертельная"
  };

  private readonly IEconomyAdminService _economyAdminService;
  private readonly IQuestPayRateAdminService _questPayRateAdminService;

  public ExcelEconomyExportService(IEconomyAdminService economyAdminService, IQuestPayRateAdminService questPayRateAdminService)
  {
    _economyAdminService = economyAdminService;
    _questPayRateAdminService = questPayRateAdminService;
  }

  #endregion

  #region Публичные методы

  /// <inheritdoc />
  public async Task<byte[]> ExportCitiesAsync(CancellationToken cancellationToken)
  {
    var matrix = await _economyAdminService.GetCityModifierMatrixAsync(cancellationToken);
    var cities = await _economyAdminService.GetCitiesAsync(cancellationToken);

    using var workbook = new XLWorkbook();
    var sheet = workbook.Worksheets.Add(CitiesSheetName);

    sheet.Cell(1, 1).Value = "Тип";
    sheet.Cell(1, 2).Value = "Подтип";
    sheet.Cell(2, 1).Value = "";
    sheet.Cell(2, 2).Value = "";

    var cityColumns = new Dictionary<Guid, int>();
    for (var i = 0; i < cities.Count; i++)
    {
      var column = i + 3;
      cityColumns[cities[i].Id] = column;
      sheet.Cell(1, column).Value = cities[i].Name;
      sheet.Cell(2, column).Value = CitySizeLabels[cities[i].Size];
    }

    var row = 3;
    foreach (var modifierRow in matrix.Rows)
    {
      sheet.Cell(row, 1).Value = modifierRow.Type;
      sheet.Cell(row, 2).Value = modifierRow.Subtype;

      foreach (var city in cities)
      {
        sheet.Cell(row, cityColumns[city.Id]).Value = modifierRow.CoefficientsByCityId.GetValueOrDefault(city.Id, 1m);
      }

      row++;
    }

    sheet.Columns().AdjustToContents();
    return SaveToBytes(workbook);
  }

  /// <inheritdoc />
  public async Task<byte[]> ExportSeasonModifiersAsync(CancellationToken cancellationToken)
  {
    var rows = await _economyAdminService.GetSeasonModifierMatrixAsync(cancellationToken);

    using var workbook = new XLWorkbook();
    var sheet = workbook.Worksheets.Add(SeasonsSheetName);

    sheet.Cell(1, 1).Value = "Тип";
    sheet.Cell(1, 2).Value = "Подтип";
    for (var i = 0; i < SeasonOrder.Length; i++)
    {
      sheet.Cell(1, i + 3).Value = SeasonLabels[SeasonOrder[i]];
    }

    var row = 2;
    foreach (var modifierRow in rows)
    {
      sheet.Cell(row, 1).Value = modifierRow.Type;
      sheet.Cell(row, 2).Value = modifierRow.Subtype;

      for (var i = 0; i < SeasonOrder.Length; i++)
      {
        sheet.Cell(row, i + 3).Value = modifierRow.CoefficientsBySeason.GetValueOrDefault(SeasonOrder[i], 1m);
      }

      row++;
    }

    sheet.Columns().AdjustToContents();
    return SaveToBytes(workbook);
  }

  /// <inheritdoc />
  public async Task<byte[]> ExportSessionsAsync(CancellationToken cancellationToken)
  {
    var sessions = await _economyAdminService.GetSessionsAsync(cancellationToken);

    using var workbook = new XLWorkbook();
    var sheet = workbook.Worksheets.Add(SettingsSheetName);

    string[] headers =
      ["Название", "Описание", "Дата (реальная)", "Игровая дата", "Город", "Сезон", "Базовый коэффициент", "Коэффициент продажи"];
    for (var i = 0; i < headers.Length; i++)
    {
      sheet.Cell(1, i + 1).Value = headers[i];
    }

    var row = 2;
    foreach (var session in sessions)
    {
      sheet.Cell(row, 1).Value = session.Name;
      sheet.Cell(row, 2).Value = session.Description;
      sheet.Cell(row, 3).Value = session.RealDate.ToDateTime(TimeOnly.MinValue);
      sheet.Cell(row, 3).Style.DateFormat.Format = "yyyy-MM-dd";
      sheet.Cell(row, 4).Value = session.GameDateLabel;
      sheet.Cell(row, 5).Value = session.CityName;
      sheet.Cell(row, 6).Value = SeasonLabels[session.Season];
      sheet.Cell(row, 7).Value = session.BaseCoefficient;
      sheet.Cell(row, 8).Value = session.SellCoefficient;
      row++;
    }

    sheet.Columns().AdjustToContents();
    return SaveToBytes(workbook);
  }

  /// <inheritdoc />
  public async Task<byte[]> ExportCoinAcceptanceAsync(CancellationToken cancellationToken)
  {
    var matrix = await _economyAdminService.GetCoinAcceptanceMatrixAsync(cancellationToken);

    using var workbook = new XLWorkbook();
    var sheet = workbook.Worksheets.Add(CoinAcceptanceSheetName);

    sheet.Cell(1, 1).Value = "Номинал";
    var cityColumns = new Dictionary<Guid, int>();
    for (var i = 0; i < matrix.Cities.Count; i++)
    {
      var column = i + 2;
      cityColumns[matrix.Cities[i].Id] = column;
      sheet.Cell(1, column).Value = matrix.Cities[i].Name;
    }

    var row = 2;
    foreach (var acceptanceRow in matrix.Rows)
    {
      var info = CoinDenominations.All.Single(x => x.Denomination == acceptanceRow.Denomination);
      sheet.Cell(row, 1).Value = info.DisplayName;

      foreach (var city in matrix.Cities)
      {
        sheet.Cell(row, cityColumns[city.Id]).Value = acceptanceRow.AcceptanceRateByCityId.GetValueOrDefault(city.Id, 1m);
      }

      row++;
    }

    // Заметки по городам — двухколоночная таблица через пустую строку после матрицы
    // (см. ExcelEconomyImportService.ImportCoinAcceptanceRowsAsync — ищет строку "Город" по содержимому, не по номеру).
    row++;
    sheet.Cell(row, 1).Value = "Город";
    sheet.Cell(row, 2).Value = "Заметка";
    row++;
    foreach (var city in matrix.Cities)
    {
      sheet.Cell(row, 1).Value = city.Name;
      sheet.Cell(row, 2).Value = matrix.NotesByCityId.GetValueOrDefault(city.Id);
      row++;
    }

    sheet.Columns().AdjustToContents();
    return SaveToBytes(workbook);
  }

  /// <inheritdoc />
  public async Task<byte[]> ExportQuestPayRatesAsync(CancellationToken cancellationToken)
  {
    var rates = await _questPayRateAdminService.GetAllAsync(cancellationToken);

    using var workbook = new XLWorkbook();
    var sheet = workbook.Worksheets.Add(QuestPayRatesSheetName);

    string[] headers = ["Эпоха", "Категория", "Опасность", "Задание", "Длительность", "Оплата партии (зм)", "Примечание по балансу"];
    for (var i = 0; i < headers.Length; i++)
    {
      sheet.Cell(1, i + 1).Value = headers[i];
    }

    var row = 2;
    foreach (var rate in rates)
    {
      sheet.Cell(row, 1).Value = QuestEpochLabels[rate.Epoch];
      sheet.Cell(row, 2).Value = rate.Category;
      sheet.Cell(row, 3).Value = QuestDangerLevelLabels[rate.DangerLevel];
      sheet.Cell(row, 4).Value = rate.Description;
      sheet.Cell(row, 5).Value = rate.Duration;
      sheet.Cell(row, 6).Value = rate.PartyPayment;
      sheet.Cell(row, 7).Value = rate.BalanceNote;
      row++;
    }

    sheet.Columns().AdjustToContents();
    return SaveToBytes(workbook);
  }

  #endregion

  #region Приватные шаги

  private static byte[] SaveToBytes(XLWorkbook workbook)
  {
    using var stream = new MemoryStream();
    workbook.SaveAs(stream);
    return stream.ToArray();
  }

  #endregion
}
