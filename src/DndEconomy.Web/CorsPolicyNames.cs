namespace DndEconomy.Web;

/// <summary>Имена CORS-политик, регистрируемых в Program.cs.</summary>
public static class CorsPolicyNames
{
  /// <summary>
  /// Разрешает кросс-сайтовые запросы с credentials с домена Foundry VTT (relaxerr-dnd.ru —
  /// другой сайт относительно relaxerr-dnd-economy.ru, хоть и на одном сервере) к API-контроллерам,
  /// используемым макросами Foundry (поиск предмета, создание предмета, логин).
  /// </summary>
  public const string FoundryVtt = "FoundryVtt";
}
