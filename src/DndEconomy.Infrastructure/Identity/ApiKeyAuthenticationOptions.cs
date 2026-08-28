using Microsoft.AspNetCore.Authentication;

namespace DndEconomy.Infrastructure.Identity;

/// <summary>
/// Опции схемы <see cref="Domain.Constants.AuthenticationSchemeNames.MacroApiKey"/> — два
/// статичных ключа, привязанные к конфигурации (секция "MacroApi"), не к записям пользователей:
/// макросам Foundry VTT не нужна привязка к конкретному игроку, только факт "это доверенный макрос".
/// </summary>
public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
  /// <summary>Ключ для макроса поиска цены — доступен любому игроку с этим макросом.</summary>
  public string PlayerKey { get; set; } = string.Empty;

  /// <summary>Ключ для макроса создания предмета — даёт роль Admin, держать только у ГМ.</summary>
  public string AdminKey { get; set; } = string.Empty;
}
