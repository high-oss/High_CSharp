// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using High.OpenApi.Csv;
using High.OpenApi.Http;
using High.OpenApi.Json;
using High.OpenApi.Logging;
using High.OpenApi.Models;

namespace High.OpenApi.Resources;

/// <summary>
/// The instrument list (the scrip master): every scrip HIGH knows, per
/// category, as a 17-column CSV. Needs no credentials — neither this call nor
/// the file it downloads attaches an API key or access token.
///
/// The download URL for each category can change without notice, so it is
/// resolved internally and cached for this client's lifetime; nothing about
/// that resolution is public. <see cref="Stream"/> is the primary way to read
/// the list — <c>all</c> and <c>derivatives</c> are 14&#160;MB and 11&#160;MB —
/// and <see cref="List"/> is a materialised convenience for smaller categories.
/// </summary>
public sealed class InstrumentsResource
{
    /// <summary>
    /// The CDN host the instrument list normally downloads from — the default
    /// entry of <see cref="HighClientOptions.InstrumentManifestAllowedHosts"/>.
    /// </summary>
    public const string DefaultAllowedHost = "high-space.blr1.cdn.digitaloceanspaces.com";

    private static readonly IReadOnlyList<string> FallbackColumns = new[]
    {
        "exchange", "segment", "instrument", "high_trading_symbol", "scrip_key", "isin", "scrip_code",
        "symbol", "name", "group_series", "has_fno", "underlying_symbol", "expiry", "option_type",
        "strike_price", "price_tick", "lot_size",
    };

    /// <summary>
    /// The last-known download URLs, baked in at build time. Used only when a
    /// live manifest fetch fails — a fresh environment, a network blip, or the
    /// gateway briefly unable to serve one — so a transient failure there does
    /// not also fail every instrument list read.
    /// </summary>
    private static readonly InstrumentsManifest FallbackManifest = new()
    {
        GeneratedAt = "2026-09-29T00:00:00.000Z",
        Columns = FallbackColumns,
        Files = new[]
        {
            FallbackFile(InstrumentCategory.All, "scrip-master.csv"),
            FallbackFile(InstrumentCategory.Equity, "scrip-master-equity.csv"),
            FallbackFile(InstrumentCategory.Derivatives, "scrip-master-derivatives.csv"),
            FallbackFile(InstrumentCategory.Commodity, "scrip-master-commodity.csv"),
            FallbackFile(InstrumentCategory.Etfs, "scrip-master-etfs.csv"),
        },
    };

    private static InstrumentFile FallbackFile(InstrumentCategory category, string fileName) => new()
    {
        Instrument = category,
        Url = $"https://{DefaultAllowedHost}/scrip-master/{fileName}",
        Bytes = 0,
        Rows = 0,
        Checksum = string.Empty,
        UpdatedAt = "2026-09-29T00:00:00.000Z",
    };

    private readonly ResolvedConfig _config;
    private readonly object _manifestLock = new();
    private Task<InstrumentsManifest>? _manifestTask;

    internal InstrumentsResource(ResolvedConfig config) => _config = config;

    /// <summary>
    /// Streams every row of one category without buffering the whole file.
    ///
    /// The default <c>TimeoutMs</c> (see <see cref="HighClientOptions.TimeoutMs"/>)
    /// does not bound this call — only <paramref name="cancellationToken"/>
    /// does, so a slow connection downloading a 14&#160;MB file is not starved
    /// by the same 30-second ceiling that protects small JSON calls. If you
    /// want this specific call to time out, pass a token from your own
    /// <see cref="CancellationTokenSource"/>, e.g.
    /// <c>new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token</c>.
    /// </summary>
    /// <param name="category">Which scrips to read.</param>
    /// <param name="cancellationToken">Covers the manifest lookup and the whole download, headers and body.</param>
    /// <exception cref="HighApiError">
    /// The requested category is not published, the resolved URL fails the
    /// scheme/host safety check, or the downloaded header does not match the
    /// manifest's columns.
    /// </exception>
    public async IAsyncEnumerable<Instrument> Stream(
        InstrumentCategory category,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var manifest = await GetManifestAsync(cancellationToken).ConfigureAwait(false);
        var file = FileFor(manifest, category);
        var url = ValidatedUrl(file.Url);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        // Deliberately no Authorization / x-api-key header: the file is a
        // public, third-party CDN download, and attaching a credential to it
        // would leak that credential to that host.
        request.Headers.UserAgent.ParseAdd(_config.UserAgent);

        _config.Logger!.Info($"HIGH GET {Redaction.RedactUrl(url.ToString())}");

        using var response = await _config.HttpClient!
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _config.Logger!.Error($"HIGH <- {(int)response.StatusCode} {Redaction.RedactUrl(url.ToString())}");
            throw HighApiError.FromResponse((int)response.StatusCode, null, text);
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        IReadOnlyDictionary<string, int>? index = null;
        await foreach (var row in CsvReader.ReadRowsAsync(reader, cancellationToken).ConfigureAwait(false))
        {
            if (index is null)
            {
                index = ValidateHeader(row, manifest.Columns);
                continue;
            }

            yield return MapRow(row, index);
        }
    }

    /// <summary>
    /// Materialises one category into memory. Prefer <see cref="Stream"/> for
    /// <c>all</c> or <c>derivatives</c> (14&#160;MB and 11&#160;MB) — this
    /// holds every row for the category at once.
    /// </summary>
    public async Task<IReadOnlyList<Instrument>> List(InstrumentCategory category, CancellationToken cancellationToken = default)
    {
        var result = new List<Instrument>();
        await foreach (var instrument in Stream(category, cancellationToken).ConfigureAwait(false))
        {
            result.Add(instrument);
        }
        return result;
    }

    // ---- Manifest resolution ------------------------------------------------

    private Task<InstrumentsManifest> GetManifestAsync(CancellationToken cancellationToken)
    {
        lock (_manifestLock)
        {
            _manifestTask ??= FetchManifestWithFallbackAsync(cancellationToken);
            return _manifestTask;
        }
    }

    private async Task<InstrumentsManifest> FetchManifestWithFallbackAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await FetchManifestAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _config.Logger!.Warn(
                $"HIGH instrument list manifest fetch failed; falling back to the last-known download URLs: {ex.Message}");
            return FallbackManifest;
        }
    }

    /// <summary>
    /// <c>GET {BaseUrl}/{VersionPath}/instruments</c>, unauthenticated. Parsed
    /// by hand rather than through <see cref="HighHttpTransport.SendAsync{T}"/>'s
    /// generic deserialisation: a category the manifest lists that this SDK
    /// does not (yet) know must be ignored, not fail the whole manifest, and a
    /// strict enum converter would throw on the first one it meets.
    /// </summary>
    private async Task<InstrumentsManifest> FetchManifestAsync(CancellationToken cancellationToken)
    {
        var node = await HighHttpTransport.SendAsync(
            _config,
            new RequestOptions { Method = HttpMethod.Get, Path = "/instruments", Auth = AuthKind.None },
            cancellationToken).ConfigureAwait(false);

        if (node is not JsonObject obj)
        {
            throw new HighApiError("The instrument list manifest response was not a JSON object.", 0);
        }

        var generatedAt = obj["generatedAt"]?.GetValue<string>()
            ?? throw new HighApiError("The instrument list manifest is missing \"generatedAt\".", 0);

        var columnsNode = obj["columns"] as JsonArray
            ?? throw new HighApiError("The instrument list manifest is missing \"columns\".", 0);
        var columns = columnsNode.Select(v => v!.GetValue<string>()).ToList();

        var filesNode = obj["files"] as JsonArray
            ?? throw new HighApiError("The instrument list manifest is missing \"files\".", 0);

        var knownCategories = KnownCategoryWireValues();
        var files = new List<InstrumentFile>();
        foreach (var entry in filesNode)
        {
            var wireValue = entry?["instrument"]?.GetValue<string>();
            if (wireValue is null || !knownCategories.Contains(wireValue))
            {
                // A category this SDK does not know yet — ignored, not an error.
                continue;
            }

            files.Add(entry!.Deserialize<InstrumentFile>(HighJsonOptions.Default)!);
        }

        return new InstrumentsManifest { GeneratedAt = generatedAt, Columns = columns, Files = files };
    }

    private static HashSet<string> KnownCategoryWireValues() =>
        Enum.GetValues<InstrumentCategory>().Select(WireValueOf).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// The path segment/wire value is the enum's exact spec string, read
    /// through the one converter that owns the wire vocabulary rather than a
    /// second hand-written mapping that could drift from it.
    /// </summary>
    private static string WireValueOf(InstrumentCategory category) =>
        JsonSerializer.Serialize(category, HighJsonOptions.Default).Trim('"');

    private static InstrumentFile FileFor(InstrumentsManifest manifest, InstrumentCategory category)
    {
        foreach (var file in manifest.Files)
        {
            if (file.Instrument == category) return file;
        }

        var available = manifest.Files.Count == 0
            ? "(none)"
            : string.Join(", ", manifest.Files.Select(f => WireValueOf(f.Instrument)));
        throw new HighApiError(
            $"The instrument list manifest does not publish the \"{WireValueOf(category)}\" category. " +
            $"Categories available: {available}.",
            0);
    }

    // ---- Safety --------------------------------------------------------------

    /// <summary>Refuses a non-<c>https</c> scheme or an unlisted host before any request is made.</summary>
    private Uri ValidatedUrl(string rawUrl)
    {
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var url))
        {
            throw new HighApiError($"The instrument list manifest gave an unusable URL: \"{rawUrl}\".", 0);
        }

        var isHttps = string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

        // The one narrow exception: plain http to loopback. Every real deployment
        // target is a non-loopback host and still requires https unconditionally.
        // The exception exists only so this path can be exercised against a real
        // local HttpListener server in tests — this SDK's tests run against a
        // genuine local HTTP exchange rather than a mocked transport, and a
        // managed HttpListener cannot terminate TLS on every platform this SDK
        // is built on.
        var isLoopbackHttp = !isHttps
            && string.Equals(url.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && url.IsLoopback;

        if (!isHttps && !isLoopbackHttp)
        {
            throw new HighApiError(
                $"Refusing to download the instrument list from a non-HTTPS URL ({Redaction.RedactUrl(rawUrl)}). " +
                "The manifest must only ever point at an https:// host.",
                0);
        }

        if (!_config.InstrumentManifestAllowedHosts.Contains(url.Host))
        {
            throw new HighApiError(
                $"Refusing to download the instrument list from an unexpected host \"{url.Host}\". " +
                $"Allowed hosts: {string.Join(", ", _config.InstrumentManifestAllowedHosts)}. " +
                $"Pass {nameof(HighClientOptions.InstrumentManifestAllowedHosts)} to widen this if the host is trusted.",
                0);
        }

        return url;
    }

    // ---- CSV parsing -----------------------------------------------------------

    /// <summary>
    /// Maps each manifest column to its index in the header actually served,
    /// rather than assuming a fixed position — the spec explicitly does not
    /// promise one. Fails loudly (rather than silently shifting fields) when
    /// the set of columns served does not match the set the manifest promised.
    /// </summary>
    private static IReadOnlyDictionary<string, int> ValidateHeader(IReadOnlyList<string> header, IReadOnlyList<string> manifestColumns)
    {
        var headerSet = new HashSet<string>(header, StringComparer.Ordinal);
        var manifestSet = new HashSet<string>(manifestColumns, StringComparer.Ordinal);

        if (!headerSet.SetEquals(manifestSet))
        {
            var missing = manifestSet.Except(headerSet).ToList();
            var unexpected = headerSet.Except(manifestSet).ToList();
            var detail = string.Join(" ", new[]
            {
                missing.Count > 0 ? $"Missing: {string.Join(", ", missing)}." : null,
                unexpected.Count > 0 ? $"Unexpected: {string.Join(", ", unexpected)}." : null,
            }.Where(s => s is not null));

            throw new HighApiError(
                $"The instrument list CSV header does not match the manifest's columns. {detail}",
                0);
        }

        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < header.Count; i++) index[header[i]] = i;
        return index;
    }

    private static Instrument MapRow(IReadOnlyList<string> row, IReadOnlyDictionary<string, int> index)
    {
        if (row.Count != index.Count)
        {
            throw new HighApiError(
                $"An instrument list row has {row.Count} fields; the header has {index.Count}.", 0);
        }

        string? Cell(string column) => NullIfBlank(row[index[column]]);

        string RequiredCell(string column) =>
            NullIfBlank(row[index[column]]) ?? throw new HighApiError($"An instrument list row is missing required column \"{column}\".", 0);

        int RequiredInt(string column)
        {
            var raw = row[index[column]];
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                throw new HighApiError($"An instrument list row has a non-numeric \"{column}\" (\"{raw}\").", 0);
            }
            return value;
        }

        return new Instrument
        {
            Exchange = RequiredCell("exchange"),
            Segment = RequiredCell("segment"),
            InstrumentType = Cell("instrument"),
            TradingSymbol = RequiredCell("high_trading_symbol"),
            ScripKey = Cell("scrip_key"),
            Isin = Cell("isin"),
            ScripCode = RequiredInt("scrip_code"),
            Symbol = Cell("symbol"),
            Name = Cell("name"),
            GroupSeries = Cell("group_series"),
            HasFnO = RequiredInt("has_fno"),
            UnderlyingSymbol = Cell("underlying_symbol"),
            Expiry = ParseExpiry(row[index["expiry"]]),
            OptionType = Cell("option_type"),
            StrikePrice = ParseStrikePrice(row[index["strike_price"]]),
            PriceTick = RequiredInt("price_tick"),
            LotSize = RequiredInt("lot_size"),
        };
    }

    private static string? NullIfBlank(string value) => string.IsNullOrEmpty(value) ? null : value;

    private static DateOnly? ParseExpiry(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiry))
        {
            throw new HighApiError($"An instrument list row has an unparsable \"expiry\" (\"{value}\").", 0);
        }
        return expiry;
    }

    private static decimal? ParseStrikePrice(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
        {
            throw new HighApiError($"An instrument list row has an unparsable \"strike_price\" (\"{value}\").", 0);
        }
        return price;
    }
}
