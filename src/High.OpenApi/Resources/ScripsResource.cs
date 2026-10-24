// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using High.OpenApi.Http;
using High.OpenApi.Models;

namespace High.OpenApi.Resources;

public sealed class ScripsResource
{
    private readonly ResolvedConfig _config;

    internal ScripsResource(ResolvedConfig config) => _config = config;

    /// <summary>Last traded price and session change, keyed by trading symbol.</summary>
    public Task<Dictionary<string, Touchline>> Quotes(QuotesRequest body, CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<Dictionary<string, Touchline>>(_config,
            new RequestOptions { Method = HttpMethod.Post, Path = "/scrips/quotes", Auth = AuthKind.Bearer, Body = body },
            cancellationToken);

    /// <summary>Quotes plus the session OHLCV block, keyed by trading symbol.</summary>
    public Task<Dictionary<string, Touchline>> Ohlc(OhlcRequest body, CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<Dictionary<string, Touchline>>(_config,
            new RequestOptions { Method = HttpMethod.Post, Path = "/scrips/ohlc", Auth = AuthKind.Bearer, Body = body },
            cancellationToken);

    /// <summary>Five levels of bids and asks.</summary>
    public Task<MarketDepth> Depth(string symbol, CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<MarketDepth>(_config,
            new RequestOptions
            {
                Method = HttpMethod.Get,
                Path = PathTemplate.Of("/scrips/{symbol}/depth", new Dictionary<string, string> { ["symbol"] = symbol }),
                Auth = AuthKind.Bearer,
            },
            cancellationToken);

    /// <summary>Available expiries for a derivative underlying.</summary>
    public Task<IReadOnlyList<Expiry>> Expiries(string symbol, ExpiryType type, CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<IReadOnlyList<Expiry>>(_config,
            new RequestOptions
            {
                Method = HttpMethod.Get,
                Path = PathTemplate.Of(
                    "/scrips/{symbol}/{type}/expiries",
                    new Dictionary<string, string> { ["symbol"] = symbol, ["type"] = WireValueOf(type) }),
                Auth = AuthKind.Bearer,
            },
            cancellationToken);

    /// <summary>The futures contracts on an underlying.</summary>
    public Task<IReadOnlyList<ScripInfo>> FutureData(string symbol, CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<IReadOnlyList<ScripInfo>>(_config,
            new RequestOptions
            {
                Method = HttpMethod.Get,
                Path = PathTemplate.Of("/scrips/{symbol}/future-data", new Dictionary<string, string> { ["symbol"] = symbol }),
                Auth = AuthKind.Bearer,
            },
            cancellationToken);

    /// <summary>
    /// Historical candles, returned columnar: every array has the same length
    /// and index i across them describes one candle.
    /// </summary>
    public Task<HistoricalCandles> Historical(HistoricalRequest body, CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<HistoricalCandles>(_config,
            new RequestOptions { Method = HttpMethod.Post, Path = "/scrips/historical", Auth = AuthKind.Bearer, Body = body },
            cancellationToken);

    /// <summary>OHLCV candles of the most recent trading session. Same columnar shape as <see cref="Historical"/>.</summary>
    public Task<HistoricalCandles> IntradayAsync(IntradayRequest body, CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<HistoricalCandles>(_config,
            new RequestOptions { Method = HttpMethod.Post, Path = "/scrips/intraday", Auth = AuthKind.Bearer, Body = body },
            cancellationToken);

    /// <summary>The option chain for one underlying and expiry.</summary>
    public Task<OptionChain> OptionChain(OptionChainRequest body, CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<OptionChain>(_config,
            new RequestOptions { Method = HttpMethod.Post, Path = "/scrips/option-chain", Auth = AuthKind.Bearer, Body = body },
            cancellationToken);

    /// <summary>
    /// The path segment is the enum's exact spec string (lower-case), so the
    /// path is built the same way the JSON body of every other enum field is —
    /// through the one converter that owns the wire vocabulary, not a second
    /// hand-written mapping that could drift from it.
    /// </summary>
    private static string WireValueOf(ExpiryType type) =>
        System.Text.Json.JsonSerializer.Serialize(type, High.OpenApi.Json.HighJsonOptions.Default).Trim('"');
}
