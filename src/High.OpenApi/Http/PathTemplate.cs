// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Text.RegularExpressions;

namespace High.OpenApi.Http;

/// <summary>
/// Interpolates a path template, percent-encoding every value.
///
/// Trading symbols legitimately contain <c>&amp;</c> and spaces
/// (<c>M&amp;M-EQ</c>, <c>NIFTY 50</c>), which change the request if
/// interpolated raw. Encoding is not optional.
/// </summary>
public static partial class PathTemplate
{
    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder();

    public static string Of(string template, IReadOnlyDictionary<string, string> parameters)
    {
        return Placeholder().Replace(template, match =>
        {
            var name = match.Groups[1].Value;
            if (!parameters.TryGetValue(name, out var value))
            {
                throw new ArgumentException($"Missing path parameter \"{name}\" for {template}");
            }
            return Uri.EscapeDataString(value);
        });
    }
}
