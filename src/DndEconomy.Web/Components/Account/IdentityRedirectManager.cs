using DndEconomy.Domain.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace DndEconomy.Web.Components.Account;

/// <summary>
/// Редиректы со страниц Account/* (статичный SSR-рендер, см. Pages/_Imports.razor) вместе
/// с передачей одноразового статус-сообщения через короткоживущую cookie — обычный
/// NavigationManager.NavigateTo этого сделать не может, потому что редирект происходит
/// в рамках того же HTTP-ответа, до следующей навигации.
/// </summary>
internal sealed class IdentityRedirectManager(NavigationManager navigationManager)
{
  /// <summary>Имя cookie, в которой на один переход хранится статус-сообщение (см. StatusMessage.razor).</summary>
  public const string StatusCookieName = "Identity.StatusMessage";

  private static readonly CookieBuilder StatusCookieBuilder = new()
  {
    SameSite = SameSiteMode.Strict,
    HttpOnly = true,
    IsEssential = true,
    MaxAge = TimeSpan.FromSeconds(5)
  };

  public void RedirectTo(string? uri)
  {
    uri ??= "";

    // Защита от open redirect — если uri не относительный, обрезаем до относительного пути.
    if (!Uri.IsWellFormedUriString(uri, UriKind.Relative))
      uri = navigationManager.ToBaseRelativePath(uri);

    navigationManager.NavigateTo(uri);
  }

  public void RedirectTo(string uri, Dictionary<string, object?> queryParameters)
  {
    var uriWithoutQuery = navigationManager.ToAbsoluteUri(uri).GetLeftPart(UriPartial.Path);
    var newUri = navigationManager.GetUriWithQueryParameters(uriWithoutQuery, queryParameters);
    RedirectTo(newUri);
  }

  public void RedirectToWithStatus(string uri, string message, HttpContext context)
  {
    context.Response.Cookies.Append(StatusCookieName, message, StatusCookieBuilder.Build(context));
    RedirectTo(uri);
  }

  private string CurrentPath => navigationManager.ToAbsoluteUri(navigationManager.Uri).GetLeftPart(UriPartial.Path);

  public void RedirectToCurrentPageWithStatus(string message, HttpContext context)
    => RedirectToWithStatus(CurrentPath, message, context);

  public void RedirectToInvalidUser(UserManager<ApplicationUser> userManager, HttpContext context)
    => RedirectToWithStatus("Account/InvalidUser", $"Error: не удалось загрузить пользователя '{userManager.GetUserId(context.User)}'.", context);
}
