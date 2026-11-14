using System.Security.Claims;
using DndEconomy.Infrastructure.Identity;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace DndEconomy.Web.Services;

/// <summary>Не допускает действия из ранее открытого circuit после отключения учётки.</summary>
public sealed class ActiveUserCircuitHandler(AuthenticationStateProvider auth, AccountAccess access, NavigationManager navigation) : CircuitHandler
{
  private ClaimsPrincipal? authenticatedUser;

  /// <inheritdoc />
  public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(Func<CircuitInboundActivityContext, Task> next) => async context =>
  {
    var principal = (await auth.GetAuthenticationStateAsync()).User;
    if (principal.Identity?.IsAuthenticated == true) authenticatedUser = principal;
    if (authenticatedUser is not null)
    {
      // RevalidatingAuthenticationStateProvider может уже заменить principal на анонимный.
      // Сохраняем причину отзыва, проверяя последнюю аутентифицированную учётку.
      var state = await access.Check(authenticatedUser);
      if (state != AccountSessionState.Valid || principal.Identity?.IsAuthenticated != true)
      {
        navigation.NavigateTo(state == AccountSessionState.Inactive ? "/Account/Inactive" : "/Account/Login", forceLoad: true);
        return;
      }
    }
    await next(context);
  };
}
