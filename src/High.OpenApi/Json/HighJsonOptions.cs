// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Text.Json;

namespace High.OpenApi.Json;

/// <summary>
/// The single <see cref="JsonSerializerOptions"/> the SDK serialises and
/// deserialises with. Every model property carries an explicit
/// <c>[JsonPropertyName]</c> rather than relying on a naming-policy heuristic —
/// the spec has enough irregular casing (<c>LTP</c>, <c>hasFnO</c>, <c>isAMO</c>)
/// that a policy would guess wrong somewhere.
/// </summary>
public static class HighJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}
