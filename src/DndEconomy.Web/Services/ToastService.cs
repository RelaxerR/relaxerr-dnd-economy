namespace DndEconomy.Web.Services;

/// <summary>
/// Всплывающие уведомления об успешных действиях (справа внизу, см. ToastContainer.razor).
/// Scoped — один экземпляр на circuit, то есть общий для всех компонентов текущей интерактивной
/// страницы пользователя.
/// </summary>
public sealed class ToastService
{
  public event Action<string>? OnShow;

  public void ShowSuccess(string message) => OnShow?.Invoke(message);
}
