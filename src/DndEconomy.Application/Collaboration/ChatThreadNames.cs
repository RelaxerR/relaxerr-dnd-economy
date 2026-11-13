using System.Text;
using System.Text.RegularExpressions;

namespace DndEconomy.Application.Collaboration;

/// <summary>Короткое, различимое имя ветки по исходному сообщению.</summary>
public static partial class ChatThreadNames
{
  /// <summary>Убирает переносы строк и ограничивает длину превью.</summary>
  public static string FromMessage(string text)
  {
    var preview = Whitespace().Replace(text, " ").Trim();
    var runes = preview.EnumerateRunes().ToArray();
    return "Ветка: " + (runes.Length <= 72 ? preview : string.Concat(runes.Take(71).Select(x => x.ToString())) + "…");
  }

  [GeneratedRegex(@"\s+")]
  private static partial Regex Whitespace();
}
