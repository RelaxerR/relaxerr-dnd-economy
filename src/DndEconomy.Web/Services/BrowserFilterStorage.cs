using System.Text.Json;
using Microsoft.JSInterop;

namespace DndEconomy.Web.Services;

/// <summary>
/// Сохранение фильтров страницы в localStorage браузера — фильтры личные для устройства
/// администратора, в БД им не место (тот же подход, что у фильтров каталога в
/// <c>CatalogIndex.razor</c>). JS interop недоступен во время статичного prerender'а, поэтому
/// вызывать только из <c>OnAfterRenderAsync</c> и позже. Ошибки хранилища (приватный режим,
/// заблокированные данные сайта, битый JSON) не пробрасываются — страница просто
/// открывается с фильтрами по умолчанию.
/// </summary>
public static class BrowserFilterStorage
{
  /// <summary>Читает сохранённое состояние или null, если его нет или оно не читается.</summary>
  public static async Task<T?> LoadFiltersAsync<T>(this IJSRuntime jsRuntime, string key) where T : class
  {
    try
    {
      var json = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
      return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<T>(json);
    }
    catch (Exception ex) when (ex is JSException or JsonException or InvalidOperationException)
    {
      return null;
    }
  }

  /// <summary>Сохраняет состояние; ошибки хранилища молча игнорируются.</summary>
  public static async Task SaveFiltersAsync<T>(this IJSRuntime jsRuntime, string key, T state)
  {
    try
    {
      await jsRuntime.InvokeVoidAsync("localStorage.setItem", key, JsonSerializer.Serialize(state));
    }
    catch (Exception ex) when (ex is JSException or InvalidOperationException)
    {
      // Хранилище недоступно — фильтры просто не запомнятся до следующего визита.
    }
  }
}
