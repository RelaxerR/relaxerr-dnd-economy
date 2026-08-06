using System.Security.Claims;
using DndEconomy.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DndEconomy.Web.Components.Account;

/// <summary>Минимальные API-эндпоинты, нужные страницам Account/*. Logout — единственный,
/// т.к. SignInManager.SignOutAsync пишет заголовки и не может быть вызван из интерактивного компонента.</summary>
internal static class AccountEndpointRouteBuilderExtensions
{
  public static IEndpointConventionBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
  {
    var group = endpoints.MapGroup("/Account");

    group.MapPost("/Logout", async (
      ClaimsPrincipal user,
      [FromServices] SignInManager<ApplicationUser> signInManager,
      [FromForm] string returnUrl) =>
    {
      await signInManager.SignOutAsync();
      return TypedResults.LocalRedirect($"~/{returnUrl}");
    });

    return group;
  }
}
