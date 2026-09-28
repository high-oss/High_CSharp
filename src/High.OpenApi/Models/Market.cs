// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Text.Json.Serialization;

namespace High.OpenApi.Models;

/// <summary>Session state per exchange for the given date.</summary>
public sealed record MarketStatus
{
    [JsonPropertyName("isHoliday")] public required bool IsHoliday { get; init; }
    [JsonPropertyName("date")] public required string Date { get; init; }

    /// <summary>Keyed by exchange code (NSE, BSE, NSEFO, BSEFO, NSECDS, MCX).</summary>
    [JsonPropertyName("exchangeStatus")] public required Dictionary<string, ExchangeStatus> ExchangeStatus { get; init; }
}

public sealed record ExchangeStatus
{
    [JsonPropertyName("status")] public required string Status { get; init; }

    /// <summary>Seconds until this session's state changes.</summary>
    [JsonPropertyName("timeRemainingInSeconds")] public required long TimeRemainingInSeconds { get; init; }

    /// <summary>True during an exchange mock session. Null or absent when unrecorded.</summary>
    [JsonPropertyName("isMock")] public bool? IsMock { get; init; }
}
