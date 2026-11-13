using System.Globalization;
using System.Text.RegularExpressions;

namespace DndEconomy.Application.Collaboration;

/// <summary>Предлагает только один полный локальный timestamp без домысливания дат и зон.</summary>
public static class ChatTimeParser
{
  /// <summary>Возвращает локальное время для явного подтверждения автором; текст остаётся неизменным.</summary>
  public static DateTime? Recognize(string text)
  {
    var matches = Regex.Matches(text, @"(?<!\d)\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}(?!\d|:|[+Zz-])");
    return matches.Count == 1 && DateTime.TryParseExact(matches[0].Value.Replace('T', ' '), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
  }
}
