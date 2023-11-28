using System;
using System.Text.RegularExpressions;

namespace SRPTests.TestHelpers;

public static class StringHelpers
{
    private static Regex NewlineRegex = new Regex(@"\r\n|\n\r|\n|\r", RegexOptions.Compiled);
    public static string SanitizeNewlines( this string input)
    {
        return NewlineRegex.Replace(input, Environment.NewLine);
    }
}