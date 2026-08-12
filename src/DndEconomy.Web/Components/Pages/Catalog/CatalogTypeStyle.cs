namespace DndEconomy.Web.Components.Pages.Catalog;

/// <summary>Эмодзи и акцентный цвет карточки предмета в каталоге для конкретного значения Item.Type.</summary>
public sealed record CatalogTypeStyle(string Emoji, string ColorHex);

/// <summary>
/// Сопоставляет Item.Type (не Category — Category в этом проекте означает исходную книгу
/// правил, а не смысловую категорию предмета) с эмодзи и акцентным цветом карточки каталога.
/// Список значений Type статичен (задаётся импортом Excel), поэтому маппинг — простой словарь,
/// а не что-то настраиваемое из БД. Цвета подобраны с контрастом ≥ 4.5:1 на белом фоне карточки.
/// </summary>
public static class CatalogTypeStyleProvider
{
  private static readonly CatalogTypeStyle Fallback = new("📦", "#6e6e73");

  private static readonly Dictionary<string, CatalogTypeStyle> StylesByType = new()
  {
    ["Оружие"] = new CatalogTypeStyle("⚔️", "#8a3b2c"),
    ["Доспехи"] = new CatalogTypeStyle("🛡️", "#35618f"),
    ["Боеприпасы"] = new CatalogTypeStyle("🏹", "#96591b"),
    ["Расходники"] = new CatalogTypeStyle("🧪", "#157a63"),
    ["Драгоценные камни"] = new CatalogTypeStyle("💎", "#1f7a8c"),
    ["Произведения искусства"] = new CatalogTypeStyle("🎨", "#a3406b"),
    ["Чудесные предметы"] = new CatalogTypeStyle("✨", "#6a3fa0"),
    ["Инструменты"] = new CatalogTypeStyle("🛠️", "#5b6470"),
    ["Заклинательная фокусировка"] = new CatalogTypeStyle("🔮", "#4453a8"),
    ["Снаряжение приключенца"] = new CatalogTypeStyle("🎒", "#4f7a3d"),
  };

  /// <summary>Возвращает стиль для Type; неизвестные/новые значения получают нейтральный вид, а не падают с ошибкой.</summary>
  public static CatalogTypeStyle Resolve(string type) =>
    StylesByType.GetValueOrDefault(type, Fallback);
}
