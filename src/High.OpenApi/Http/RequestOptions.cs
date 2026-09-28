// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

namespace High.OpenApi.Http;

/// <summary>Describes one operation call, before it is turned into an <see cref="HttpRequestMessage"/>.</summary>
public sealed class RequestOptions
{
    public required HttpMethod Method { get; init; }

    /// <summary>Operation path, already interpolated and percent-encoded.</summary>
    public required string Path { get; init; }

    public required AuthKind Auth { get; init; }

    public IReadOnlyDictionary<string, string?>? Query { get; init; }

    /// <summary>Serialised as the JSON request body when present.</summary>
    public object? Body { get; init; }
}
