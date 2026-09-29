// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Text.Json.Serialization;

namespace High.OpenApi.Models;

/// <summary>
/// Where to download one category of the instrument list and what the file
/// contains. <c>Bytes</c> and <c>Checksum</c> describe exactly what
/// <c>Url</c> served when the list was built.
/// </summary>
public sealed record InstrumentFile
{
    [JsonPropertyName("instrument")] public required InstrumentCategory Instrument { get; init; }

    /// <summary>Direct HTTPS download. Needs no API key and no access token.</summary>
    [JsonPropertyName("url")] public required string Url { get; init; }

    [JsonPropertyName("bytes")] public required long Bytes { get; init; }

    /// <summary>Data rows, excluding the header.</summary>
    [JsonPropertyName("rows")] public required long Rows { get; init; }

    /// <summary>MD5 of the file contents, hex encoded.</summary>
    [JsonPropertyName("checksum")] public required string Checksum { get; init; }

    [JsonPropertyName("updatedAt")] public required string UpdatedAt { get; init; }
}

/// <summary>
/// Where to download each category of the instrument list, and what the CSVs
/// contain. Resolved and cached internally by the <c>Instruments</c> resource —
/// nothing here is a public entry point.
/// </summary>
public sealed record InstrumentsManifest
{
    [JsonPropertyName("generatedAt")] public required string GeneratedAt { get; init; }

    /// <summary>The CSV header, in file order. A downloaded file's own header is validated against this rather than assumed.</summary>
    [JsonPropertyName("columns")] public required IReadOnlyList<string> Columns { get; init; }

    [JsonPropertyName("files")] public required IReadOnlyList<InstrumentFile> Files { get; init; }
}

/// <summary>
/// One row of the instrument list (the scrip master): every scrip HIGH knows,
/// for one category. A blank CSV field is <c>null</c>, never an empty string.
///
/// Deliberately a plain data row, not tied to any credential — a
/// <c>record</c>'s generated <c>ToString()</c> prints every property, and
/// nothing here is sensitive.
/// </summary>
public sealed record Instrument
{
    public required string Exchange { get; init; }

    /// <summary>ES = equity spot, FO = futures and options, I = index, ETF, SPOT.</summary>
    public required string Segment { get; init; }

    /// <summary>
    /// The row's own instrument classification (e.g. <c>EQUITY</c>,
    /// <c>FUTSTK</c>, <c>OPTIDX</c>) — distinct from the category the file was
    /// downloaded under. Left blank by the exporter for a derivative whose
    /// underlying it cannot classify.
    /// </summary>
    public string? InstrumentType { get; init; }

    /// <summary>HIGH's trading symbol, e.g. <c>RELIANCE-EQ</c>.</summary>
    public required string TradingSymbol { get; init; }

    /// <summary><c>&lt;segment&gt;@&lt;scripCode&gt;</c>, the key the trading and data operations take.</summary>
    public string? ScripKey { get; init; }

    /// <summary>Equity only.</summary>
    public string? Isin { get; init; }

    /// <summary>Broker-assigned numeric token.</summary>
    public required int ScripCode { get; init; }

    public string? Symbol { get; init; }

    public string? Name { get; init; }

    /// <summary>NSE/BSE series (e.g. <c>EQ</c>, <c>BE</c>) or MCX group.</summary>
    public string? GroupSeries { get; init; }

    public required int HasFnO { get; init; }

    /// <summary>Derivatives only.</summary>
    public string? UnderlyingSymbol { get; init; }

    /// <summary>Derivatives only.</summary>
    public DateOnly? Expiry { get; init; }

    /// <summary><c>CE</c> or <c>PE</c>. Options only.</summary>
    public string? OptionType { get; init; }

    /// <summary>
    /// Derivatives only, and futures leave it blank. Money, so a <c>decimal</c>
    /// — never a <c>double</c>.
    /// </summary>
    public decimal? StrikePrice { get; init; }

    /// <summary>
    /// Never normalised: its scale varies by segment, so treating it as a
    /// fixed number of decimal places would be a silent pricing bug.
    /// </summary>
    public required int PriceTick { get; init; }

    public required int LotSize { get; init; }
}
