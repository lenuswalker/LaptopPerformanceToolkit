using System;

namespace LenovoLegionToolkit.Lib.Extensions;

public static class StringExtensions
{
    public static string GetUntilOrEmpty(this string text, string stopAt)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var charLocation = text.IndexOf(stopAt, StringComparison.Ordinal);
        if (charLocation > 0)
            return text[..charLocation];

        return string.Empty;
    }

    public static string Between(this string text, string after)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var index = text.IndexOf(after, StringComparison.Ordinal);
        if (index < 0)
            return string.Empty;

        return text[(index + after.Length)..];
    }
}
