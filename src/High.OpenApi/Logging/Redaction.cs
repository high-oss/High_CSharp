// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Text.Json;
using System.Text.Json.Nodes;

namespace High.OpenApi.Logging;

/// <summary>
/// Redaction is not optional, and it is why logging goes through this one
/// module. Headers are never logged at all (the caller never routes them
/// here); this covers URLs and bodies.
/// </summary>
public static class Redaction
{
    /// <summary>
    /// Query parameters that must never reach a log sink. <c>tOtp</c> is a live
    /// second factor and the token parameters are bearer credentials.
    /// </summary>
    private static readonly HashSet<string> SensitiveParams = new(StringComparer.Ordinal)
    {
        "tOtp", "apiKey", "accessToken", "tokenId", "stepToken",
    };

    /// <summary>
    /// Object keys whose values must never be logged, whatever nesting they sit
    /// at. Query parameters are handled by <see cref="RedactUrl"/>; this covers
    /// request and response bodies.
    /// </summary>
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.Ordinal)
    {
        "tOtp", "totp", "otp", "apiKey", "accessToken", "refreshToken",
        "tokenId", "stepToken", "password", "pin", "authorization",
        // "sessionid" is the datafeed socket's own auth frame field
        // ({"type":"cn","sessionid":"<accessToken>"}) — the same bearer
        // credential under the wire's own name.
        "sessionid",
    };

    /// <summary>Above this, a logged payload is replaced by a note rather than printed.</summary>
    private const int MaxLoggedJson = 1500;

    /// <summary>Replaces sensitive query parameter values with <c>REDACTED</c>.</summary>
    public static string RedactUrl(string url)
    {
        Uri parsed;
        try
        {
            parsed = new Uri(url, UriKind.Absolute);
        }
        catch (UriFormatException)
        {
            // Not a URL we can parse — return it as-is rather than throwing inside
            // a log call, which would turn an observability feature into an outage.
            return url;
        }

        var query = parsed.Query;
        if (string.IsNullOrEmpty(query)) return url;

        var pairs = ParseQuery(query.TrimStart('?'));
        var touched = false;
        for (var i = 0; i < pairs.Count; i++)
        {
            if (SensitiveParams.Contains(pairs[i].Key))
            {
                pairs[i] = (pairs[i].Key, "REDACTED");
                touched = true;
            }
        }

        if (!touched) return url;

        var rebuiltQuery = string.Join("&", pairs.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        var builder = new UriBuilder(parsed) { Query = rebuiltQuery };
        return builder.Uri.ToString();
    }

    private static List<(string Key, string Value)> ParseQuery(string query)
    {
        var result = new List<(string, string)>();
        foreach (var segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = segment.IndexOf('=');
            var key = idx >= 0 ? segment[..idx] : segment;
            var value = idx >= 0 ? segment[(idx + 1)..] : string.Empty;
            result.Add((Uri.UnescapeDataString(key), Uri.UnescapeDataString(value)));
        }
        return result;
    }

    /// <summary>
    /// Deep-copies a JSON payload for logging, masking credential-shaped keys
    /// and replacing anything oversized with a note. Returns <c>null</c> for a
    /// null payload.
    /// </summary>
    public static object? RedactBody(JsonNode? value)
    {
        if (value is null) return null;

        var masked = Mask(value);
        string serialised;
        try
        {
            serialised = masked?.ToJsonString() ?? "null";
        }
        catch (Exception)
        {
            return "[unserialisable]";
        }

        if (serialised.Length > MaxLoggedJson)
        {
            return $"[truncated: {serialised.Length} bytes of JSON]";
        }

        return masked;
    }

    /// <summary>Same as <see cref="RedactBody(JsonNode?)"/>, serialising an arbitrary object first.</summary>
    public static object? RedactBody(object? value, JsonSerializerOptions? jsonOptions = null)
    {
        if (value is null) return null;
        if (value is JsonNode node) return RedactBody(node);
        if (value is string text) return RedactString(text);

        JsonNode? serialised;
        try
        {
            serialised = JsonSerializer.SerializeToNode(value, jsonOptions);
        }
        catch (Exception)
        {
            return "[unserialisable]";
        }

        return RedactBody(serialised);
    }

    private static object RedactString(string text) =>
        text.Length > MaxLoggedJson ? $"[truncated: {text.Length} bytes of JSON]" : text;

    private static JsonNode? Mask(JsonNode? input)
    {
        switch (input)
        {
            case JsonObject obj:
            {
                var result = new JsonObject();
                foreach (var (key, nested) in obj)
                {
                    result[key] = SensitiveKeys.Contains(key)
                        ? JsonValue.Create("REDACTED")
                        : Mask(nested?.DeepClone());
                }
                return result;
            }
            case JsonArray arr:
            {
                var result = new JsonArray();
                foreach (var item in arr) result.Add(Mask(item?.DeepClone()));
                return result;
            }
            default:
                return input?.DeepClone();
        }
    }
}
