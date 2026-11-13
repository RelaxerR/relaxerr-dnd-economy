using DndEconomy.Infrastructure.Identity;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace DndEconomy.Web.Services;

/// <summary>Не допускает действия из ранее открытого circuit после отключения учётки.</summary>
public sealed class ActiveUserCircuitHandler(AuthenticationStateProvider auth, AccountAccess access, NavigationManager navigation) : CircuitHandler
{
  private bool wasAuthenticated;

  /// <inheritdoc />
  public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(Func<CircuitInboundActivityContext, Task> next) => async context =>
  {
    var principal = (await auth.GetAuthenticationStateAsync()).User;
    if (principal.Identity?.IsAuthenticated != true && wasAuthenticated)
    {
      navigation.NavigateTo("/Account/Login", forceLoad: true);
      return;
    }
    if (principal.Identity?.IsAuthenticated == true)
    {
      wasAuthenticated = true;
      var state = await access.Check(principal);
      if (state != AccountSessionState.Valid)
      {
        navigation.NavigateTo(state == AccountSessionState.Inactive ? "/Account/Inactive" : "/Account/Login", forceLoad: true);
        return;
      }
    }
    await next(context);
  };
}
