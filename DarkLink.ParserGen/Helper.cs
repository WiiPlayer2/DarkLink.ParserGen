using System;
using System.Collections.Generic;
using System.Text;

namespace DarkLink.ParserGen
{
    internal static class Helper
    {
        public static string Decapitalize(this string s)
            => s == string.Empty
                ? s
                : $"{char.ToLower(s[0])}{s.Substring(1)}";

        public static IEnumerable<T> Yield<T>(this T item)
            => new[] { item };
    }
}