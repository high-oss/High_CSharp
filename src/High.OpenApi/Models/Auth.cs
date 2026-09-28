// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Text.Json.Serialization;

namespace High.OpenApi.Models;

/// <summary>A 24-hour access token for the Open API.</summary>
public sealed record AccessToken
{
    /// <summary>Send as <c>Authorization: Bearer &lt;token&gt;</c>.</summary>
    [JsonPropertyName("accessToken")] public required string Value { get; init; }

    [JsonPropertyName("expiresAt")] public required string ExpiresAt { get; init; }
}
