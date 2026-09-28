// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Text.Json.Nodes;

namespace High.OpenApi;

/// <summary>
/// Every failure the SDK surfaces — transport, timeout, or an API error
/// response. Nothing else escapes the SDK: no raw <see cref="HttpRequestException"/>,
/// no JSON parse error.
/// </summary>
public sealed class HighApiError : Exception
{
    /// <summary>HTTP status, or 0 for a transport failure or timeout.</summary>
    public int Status { get; }

    /// <summary>
    /// An open string: the documented catalogue is <see cref="ErrorCodes"/>, but
    /// the gateway can emit codes outside it.
    /// </summary>
    public string? Code { get; }

    /// <summary>Quote this in support tickets.</summary>
    public string? RequestId { get; }

    /// <summary>Every message the API returned; validation errors return several.</summary>
    public IReadOnlyList<string>? Messages { get; }

    /// <summary>The parsed body, when there was one.</summary>
    public object? Body { get; }

    public HighApiError(
        string message,
        int status,
        string? code = null,
        string? requestId = null,
        IReadOnlyList<string>? messages = null,
        object? body = null)
        : base(message)
    {
        Status = status;
        Code = code;
        RequestId = requestId;
        Messages = messages;
        Body = body;
    }

    /// <summary>
    /// Builds the error for a non-2xx response. <paramref name="body"/> is the
    /// parsed JSON when the response had any; <paramref name="fallbackText"/> is
    /// the raw text when it did not — a proxy or load balancer failure returns
    /// HTML, and losing that to a JSON parse error would hide the status the
    /// caller needs.
    /// </summary>
    public static HighApiError FromResponse(int status, JsonNode? body, string? fallbackText = null)
    {
        var obj = body as JsonObject;
        var messages = MessagesOf(obj?["message"]);
        var code = obj?["code"]?.GetValue<string>();
        var requestId = obj?["requestId"]?.GetValue<string>();
        var snippet = fallbackText?.Trim();
        if (snippet is { Length: > 200 }) snippet = snippet[..200];

        var message = messages is { Count: > 0 }
            ? string.Join("; ", messages)
            : (snippet is { Length: > 0 } ? $"HTTP {status}: {snippet}" : $"HTTP {status}");

        // Explicitly typed as object?: a plain ternary between JsonNode and
        // string lets the compiler pick JsonNode as the common type (JsonValue
        // defines an implicit `string -> JsonNode` conversion for convenient
        // construction), silently wrapping fallbackText in a JsonValue instead
        // of keeping it a plain string.
        object? effectiveBody = body is not null ? (object?)body : fallbackText;

        return new HighApiError(message, status, code, requestId, messages, effectiveBody);
    }

    private static IReadOnlyList<string>? MessagesOf(JsonNode? value)
    {
        if (value is null) return null;
        if (value is JsonValue val && val.TryGetValue<string>(out var single)) return new[] { single };
        if (value is JsonArray array)
        {
            var strings = array
                .Select(item => item is JsonValue v && v.TryGetValue<string>(out var s) ? s : null)
                .Where(s => s is not null)
                .Select(s => s!)
                .ToArray();
            return strings.Length > 0 ? strings : null;
        }
        return null;
    }
}
