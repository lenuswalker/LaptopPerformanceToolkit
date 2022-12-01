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

    public static string Between(string str, string firstString, string lastString = null, bool keepBorders = false)
    {
        if (string.IsNullOrEmpty(str))
            return string.Empty;
            
        string finalString;
        int pos1 = str.IndexOf(firstString) + firstString.Length;
        int pos2 = str.Length;

        if (lastString != null)
            pos2 = str.IndexOf(lastString, pos1);

        finalString = str.Substring(pos1, pos2 - pos1);
        return keepBorders ? firstString + finalString + lastString : finalString;
    }
}