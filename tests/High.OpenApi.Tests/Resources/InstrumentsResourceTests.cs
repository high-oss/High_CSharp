// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using High.OpenApi;
using High.OpenApi.Models;
using Xunit;

namespace High.OpenApi.Tests.Resources;

public class InstrumentsResourceTests
{
    private static readonly string[] StandardColumns =
    {
        "exchange", "segment", "instrument", "high_trading_symbol", "scrip_key", "isin", "scrip_code",
        "symbol", "name", "group_series", "has_fno", "underlying_symbol", "expiry", "option_type",
        "strike_price", "price_tick", "lot_size",
    };

    private const string EquityCsvHeader =
        "exchange,segment,instrument,high_trading_symbol,scrip_key,isin,scrip_code,symbol,name,group_series,has_fno,underlying_symbol,expiry,option_type,strike_price,price_tick,lot_size";

    // A plain row (blank for every derivative-only column) and a row whose
    // name is quoted and contains both a comma and an escaped quote, with
    // every derivative-only column populated and a different price_tick —
    // proving CSV quoting is parsed properly (not Split(',')) and that
    // price_tick is carried through unscaled, per row.
    private const string EquityCsvRows =
        "NSE,ES,EQUITY,RELIANCE-EQ,NSE@2885,INE002A01018,2885,RELIANCE,Reliance Industries Ltd,EQ,1,,,,,5,1\n" +
        "NSE,FO,OPTSTK,RELIANCE-25DEC26-1400-CE,NSEFO@88412,,88412,RELIANCE,\"Tata Motors, Ltd. \"\"Class A\"\"\",,0,RELIANCE,2026-12-25,CE,1400.5,10,250\n";

    private const string EquityCsv = EquityCsvHeader + "\n" + EquityCsvRows;

    /// <summary>
    /// A real HttpListener serving the manifest JSON that <paramref name="manifestJson"/>
    /// returns at the moment of each request (a <c>Func</c> so the caller can
    /// finish building it, using the server's own <c>BaseUrl</c>, after
    /// construction) plus whatever CSV fixtures are registered under
    /// <paramref name="csvByPath"/>, keyed by absolute path.
    /// </summary>
    private static TestServer BuildServer(Func<string> manifestJson, IReadOnlyDictionary<string, string> csvByPath)
    {
        return new TestServer(async (ctx, _) =>
        {
            var path = ctx.Request.Url!.AbsolutePath;
            if (path == "/v1/instruments")
            {
                await TestServerResponses.EnvelopeAsync(ctx, 200, "r", manifestJson());
                return;
            }

            if (csvByPath.TryGetValue(path, out var csv))
            {
                var bytes = Encoding.UTF8.GetBytes(csv);
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/csv";
                ctx.Response.ContentLength64 = bytes.Length;
                await ctx.Response.OutputStream.WriteAsync(bytes);
                ctx.Response.OutputStream.Close();
                return;
            }

            ctx.Response.StatusCode = 404;
            ctx.Response.OutputStream.Close();
        });
    }

    private static string ManifestJson(string baseUrl, params (string Category, string Path, long Rows)[] files)
    {
        var manifest = new JsonObject
        {
            ["generatedAt"] = "2026-09-29T02:53:10.000Z",
            ["columns"] = new JsonArray(StandardColumns.Select(c => (JsonNode)c).ToArray()),
            ["files"] = new JsonArray(files.Select(f => (JsonNode)new JsonObject
            {
                ["instrument"] = f.Category,
                ["url"] = baseUrl + f.Path,
                ["bytes"] = 100,
                ["rows"] = f.Rows,
                ["checksum"] = "abc",
                ["updatedAt"] = "2026-09-29T02:53:09.000Z",
            }).ToArray()),
        };
        return manifest.ToJsonString();
    }

    /// <summary>Same shape as <see cref="ManifestJson"/> but with an arbitrary raw JSON snippet spliced in as one more <c>files</c> entry — for a category this SDK does not know.</summary>
    private static string ManifestJsonWithExtraCategory(string baseUrl, (string Category, string Path, long Rows) known, string unknownCategory)
    {
        var manifest = new JsonObject
        {
            ["generatedAt"] = "2026-09-29T02:53:10.000Z",
            ["columns"] = new JsonArray(StandardColumns.Select(c => (JsonNode)c).ToArray()),
            ["files"] = new JsonArray(
                new JsonObject
                {
                    ["instrument"] = known.Category,
                    ["url"] = baseUrl + known.Path,
                    ["bytes"] = 100,
                    ["rows"] = known.Rows,
                    ["checksum"] = "abc",
                    ["updatedAt"] = "2026-09-29T02:53:09.000Z",
                },
                new JsonObject
                {
                    ["instrument"] = unknownCategory,
                    ["url"] = baseUrl + "/csv/unknown.csv",
                    ["bytes"] = 1,
                    ["rows"] = 1,
                    ["checksum"] = "abc",
                    ["updatedAt"] = "2026-09-29T02:53:09.000Z",
                }),
        };
        return manifest.ToJsonString();
    }

    private static HighClient ClientFor(TestServer server, Action<HighClientOptions>? configure = null)
    {
        var options = new HighClientOptions
        {
            BaseUrl = server.BaseUrl,
            AccessToken = "SECRET-TOKEN",
            ApiKey = "SECRET-KEY",
            // The fixture CSV lives on the same loopback TestServer as the
            // manifest, so the host allowlist must be widened to admit it —
            // exactly the documented escape hatch for a trusted non-default host.
            InstrumentsAllowedHosts = new[] { "127.0.0.1" },
        };
        configure?.Invoke(options);
        return new HighClient(options);
    }

    [Fact]
    public async Task Streaming_reads_typed_rows_with_blanks_as_null_and_price_tick_left_unscaled()
    {
        string? manifest = null;
        using var server = BuildServer(() => manifest!, new Dictionary<string, string> { ["/csv/equity.csv"] = EquityCsv });
        manifest = ManifestJson(server.BaseUrl, ("equity", "/csv/equity.csv", 2));

        using var client = ClientFor(server);

        var rows = new List<Instrument>();
        await foreach (var row in client.Instruments.Stream(InstrumentCategory.Equity))
        {
            rows.Add(row);
        }

        Assert.Equal(2, rows.Count);

        var equity = rows[0];
        Assert.Equal("NSE", equity.Exchange);
        Assert.Equal("ES", equity.Segment);
        Assert.Equal("EQUITY", equity.InstrumentType);
        Assert.Equal("RELIANCE-EQ", equity.TradingSymbol);
        Assert.Equal("INE002A01018", equity.Isin);
        Assert.Equal(2885, equity.ScripCode);
        Assert.Equal("Reliance Industries Ltd", equity.Name);
        Assert.Equal("EQ", equity.GroupSeries);
        Assert.Equal(1, equity.HasFnO);
        Assert.Null(equity.UnderlyingSymbol);
        Assert.Null(equity.Expiry);
        Assert.Null(equity.OptionType);
        Assert.Null(equity.StrikePrice);
        Assert.Equal(5, equity.PriceTick);
        Assert.Equal(1, equity.LotSize);

        var option = rows[1];
        Assert.Equal("Tata Motors, Ltd. \"Class A\"", option.Name);
        Assert.Null(option.Isin);
        Assert.Null(option.GroupSeries);
        Assert.Equal(0, option.HasFnO);
        Assert.Equal("RELIANCE", option.UnderlyingSymbol);
        Assert.Equal(new DateOnly(2026, 12, 25), option.Expiry);
        Assert.Equal("CE", option.OptionType);
        Assert.Equal(1400.5m, option.StrikePrice);
        Assert.Equal(10, option.PriceTick); // different from row 1's 5: never normalised.
        Assert.Equal(250, option.LotSize);
    }

    [Fact]
    public async Task List_materialises_the_same_rows_as_Stream()
    {
        string? manifest = null;
        using var server = BuildServer(() => manifest!, new Dictionary<string, string> { ["/csv/equity.csv"] = EquityCsv });
        manifest = ManifestJson(server.BaseUrl, ("equity", "/csv/equity.csv", 2));

        using var client = ClientFor(server);

        var listed = await client.Instruments.List(InstrumentCategory.Equity);

        Assert.Equal(2, listed.Count);
        Assert.Equal("RELIANCE-EQ", listed[0].TradingSymbol);
        Assert.Equal("RELIANCE-25DEC26-1400-CE", listed[1].TradingSymbol);
    }

    [Fact]
    public async Task Neither_the_manifest_call_nor_the_CSV_download_carries_credentials()
    {
        string? manifest = null;
        using var server = BuildServer(() => manifest!, new Dictionary<string, string> { ["/csv/equity.csv"] = EquityCsv });
        manifest = ManifestJson(server.BaseUrl, ("equity", "/csv/equity.csv", 2));

        using var client = ClientFor(server);
        await client.Instruments.List(InstrumentCategory.Equity);

        var manifestRequest = Assert.Single(server.Requests, r => r.Url == "/v1/instruments");
        var csvRequest = Assert.Single(server.Requests, r => r.Url == "/csv/equity.csv");

        foreach (var request in new[] { manifestRequest, csvRequest })
        {
            Assert.DoesNotContain("authorization", request.Headers.Keys);
            Assert.DoesNotContain("x-api-key", request.Headers.Keys);
        }
    }

    [Fact]
    public async Task An_unknown_category_in_the_manifest_is_ignored_not_an_error()
    {
        string? manifest = null;
        using var server = BuildServer(() => manifest!, new Dictionary<string, string> { ["/csv/equity.csv"] = EquityCsv });
        manifest = ManifestJsonWithExtraCategory(server.BaseUrl, ("equity", "/csv/equity.csv", 2), "bonds");

        using var client = ClientFor(server);

        var listed = await client.Instruments.List(InstrumentCategory.Equity);

        Assert.Equal(2, listed.Count);
        // The unknown category's file was never requested.
        Assert.DoesNotContain(server.Requests, r => r.Url == "/csv/unknown.csv");
    }

    [Fact]
    public async Task A_requested_category_the_manifest_does_not_list_is_a_clear_error()
    {
        string? manifest = null;
        using var server = BuildServer(() => manifest!, new Dictionary<string, string> { ["/csv/equity.csv"] = EquityCsv });
        manifest = ManifestJson(server.BaseUrl, ("equity", "/csv/equity.csv", 2)); // no "commodity" entry

        using var client = ClientFor(server);

        var error = await Assert.ThrowsAsync<HighApiError>(() => client.Instruments.List(InstrumentCategory.Commodity));
        Assert.Contains("commodity", error.Message);
    }

    [Fact]
    public async Task A_CSV_header_that_does_not_match_the_manifests_columns_fails_loudly()
    {
        const string badCsv = "exchange,segment,high_trading_symbol\nNSE,ES,RELIANCE-EQ\n"; // missing most columns
        string? manifest = null;
        using var server = BuildServer(() => manifest!, new Dictionary<string, string> { ["/csv/equity.csv"] = badCsv });
        manifest = ManifestJson(server.BaseUrl, ("equity", "/csv/equity.csv", 1));

        using var client = ClientFor(server);

        var error = await Assert.ThrowsAsync<HighApiError>(() => client.Instruments.List(InstrumentCategory.Equity));
        Assert.Contains("header", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_non_https_manifest_url_is_refused_before_any_request()
    {
        string? manifest = null;
        using var server = BuildServer(() => manifest!, new Dictionary<string, string> { ["/csv/equity.csv"] = EquityCsv });
        // A plausible non-loopback host, so this cannot be mistaken for the
        // test-only loopback exception: it must be refused purely for its scheme.
        manifest = ManifestJson("http://example.com", ("equity", "/csv/equity.csv", 2));

        using var client = ClientFor(server);

        var error = await Assert.ThrowsAsync<HighApiError>(() => client.Instruments.List(InstrumentCategory.Equity));
        Assert.Contains("HTTPS", error.Message, StringComparison.OrdinalIgnoreCase);

        // Only the manifest fetch happened — never a request for the CSV itself.
        Assert.DoesNotContain(server.Requests, r => r.Url == "/csv/equity.csv");
    }

    [Fact]
    public async Task A_manifest_url_on_an_unexpected_host_is_refused_before_any_request()
    {
        string? manifest = null;
        using var server = BuildServer(() => manifest!, new Dictionary<string, string> { ["/csv/equity.csv"] = EquityCsv });
        manifest = ManifestJson("https://evil.example.com", ("equity", "/csv/equity.csv", 2));

        using var client = ClientFor(server);

        var error = await Assert.ThrowsAsync<HighApiError>(() => client.Instruments.List(InstrumentCategory.Equity));
        Assert.Contains("evil.example.com", error.Message);
        Assert.DoesNotContain(server.Requests, r => r.Url == "/csv/equity.csv");
    }

    [Fact]
    public async Task Abandoning_enumeration_disposes_the_response()
    {
        string? manifest = null;
        using var server = BuildServer(() => manifest!, new Dictionary<string, string> { ["/csv/equity.csv"] = EquityCsv });
        manifest = ManifestJson(server.BaseUrl, ("equity", "/csv/equity.csv", 2));

        // A DelegatingHandler layered over a real HttpClientHandler: every
        // request still goes out over real HTTP to the TestServer above (this
        // is not a mocked transport), but it lets the test observe that the
        // HttpResponseMessage/its content were disposed once enumeration stops.
        var tracking = new DisposeTrackingHandler();
        using var httpClient = new HttpClient(tracking);
        using var client = new HighClient(new HighClientOptions
        {
            BaseUrl = server.BaseUrl,
            AccessToken = "tok",
            InstrumentsAllowedHosts = new[] { "127.0.0.1" },
            HttpClient = httpClient,
        });

        var seen = 0;
        await foreach (var row in client.Instruments.Stream(InstrumentCategory.Equity))
        {
            seen++;
            break;
        }

        Assert.Equal(1, seen);
        Assert.NotNull(tracking.LastContent);
        Assert.True(tracking.LastContent!.Disposed);
    }

    [Fact]
    public async Task The_default_TimeoutMs_does_not_bound_the_CSV_download()
    {
        const string path = "/csv/slow.csv";
        string? manifest = null;
        using var server = new TestServer(async (ctx, _) =>
        {
            var reqPath = ctx.Request.Url!.AbsolutePath;
            if (reqPath == "/v1/instruments")
            {
                await TestServerResponses.EnvelopeAsync(ctx, 200, "r", manifest!);
                return;
            }

            if (reqPath == path)
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/csv";
                ctx.Response.SendChunked = true;
                var header = Encoding.UTF8.GetBytes(EquityCsvHeader + "\n");
                await ctx.Response.OutputStream.WriteAsync(header);
                await ctx.Response.OutputStream.FlushAsync();
                // Much longer than the artificially tiny TimeoutMs below, proving
                // the CSV download is not bound by it.
                await Task.Delay(500);
                var rows = Encoding.UTF8.GetBytes(EquityCsvRows);
                await ctx.Response.OutputStream.WriteAsync(rows);
                ctx.Response.OutputStream.Close();
                return;
            }

            ctx.Response.StatusCode = 404;
            ctx.Response.OutputStream.Close();
        });
        manifest = ManifestJson(server.BaseUrl, ("equity", path, 2));

        using var client = ClientFor(server, o => o.TimeoutMs = 150);

        var listed = await client.Instruments.List(InstrumentCategory.Equity);

        Assert.Equal(2, listed.Count);
    }

    private sealed class DisposeTrackingContent : HttpContent
    {
        private readonly HttpContent _inner;

        public DisposeTrackingContent(HttpContent inner)
        {
            _inner = inner;
            foreach (var header in inner.Headers)
            {
                Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        public bool Disposed { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => _inner.CopyToAsync(stream);

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }

        protected override Task<Stream> CreateContentReadStreamAsync() => _inner.ReadAsStreamAsync();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposed = true;
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    private sealed class DisposeTrackingHandler : DelegatingHandler
    {
        public DisposeTrackingHandler() : base(new HttpClientHandler())
        {
        }

        public DisposeTrackingContent? LastContent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.Content is not null)
            {
                var tracked = new DisposeTrackingContent(response.Content);
                response.Content = tracked;
                LastContent = tracked;
            }
            return response;
        }
    }
}
