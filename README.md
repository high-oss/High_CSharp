# High.OpenApi

Official .NET SDK for the [HIGH Open API](https://openapi.high.live). Typed
against the canonical OpenAPI contract, with no runtime NuGet dependencies.

## Install

```bash
dotnet add package High.OpenApi
```

.NET 8 or newer. Built on the BCL `HttpClient`; nothing else to install.

## Quickstart

```csharp
using High.OpenApi;

using var high = new HighClient(new HighClientOptions
{
    Environment = Environment.Sandbox,   // or Environment.Production (the default)
    ApiKey = System.Environment.GetEnvironmentVariable("HIGH_API_KEY"),
    AccessToken = System.Environment.GetEnvironmentVariable("HIGH_ACCESS_TOKEN"),
});

var funds = await high.Portfolio.Funds();
Console.WriteLine(funds.AvailableBalance);
```

## Configuration

| Option | Default | Notes |
|---|---|---|
| `Environment` | `Production` | `Environment.Production` or `Environment.Sandbox` |
| `BaseUrl` | from `Environment` | Overrides the REST host |
| `WsBaseUrl` | from `Environment` | Datafeed socket host, consumed by `HighFeed` (see [Live datafeed](#live-datafeed)) |
| `VersionPath` | `"v1"` | The segment between host and operation path |
| `ApiKey` | `HIGH_API_KEY` | Sent as `x-api-key`, for `auth.GenerateAccessToken` |
| `AccessToken` | `HIGH_ACCESS_TOKEN` | Sent as `Authorization: Bearer` |
| `TimeoutMs` | `30000` | Per request, covering headers and body |
| `MaxRetries` | `2` | Idempotent reads only |
| `MaxRetryDelayMs` | `30000` | Ceiling on one retry delay; a longer `Retry-After` fails fast |
| `LogLevel` | `LogLevel.Silent` | `Silent`, `Error`, `Warn`, `Info`, `Debug` |
| `LogSink` | the console | Where log lines go (`ILogSink`) |
| `UserAgent` | — | Appended to the SDK's own |
| `HttpClient` | a new owned instance | Pass one from `IHttpClientFactory`, or a test double |

Host resolution order: explicit `BaseUrl`, then explicit `Environment`, then
`HIGH_BASE_URL`, then `HIGH_ENVIRONMENT`, then production. **An explicit
`Environment` suppresses the environment variables** — a `HIGH_BASE_URL` left
in a shell profile cannot silently route a client written as
`Environment = Environment.Sandbox` at production. An unknown environment or
log level, or a non-positive timeout, or a negative retry count throws at
construction, naming the valid values — never at request time.

Sandbox and production issue **separate API keys**. Switching environment
means switching credentials too; the SDK does not rewrite them for you.

`HighClient` implements `IDisposable`. When you pass your own `HttpClient`
(e.g. from `IHttpClientFactory`), disposing the client does not dispose the
`HttpClient` you gave it — the SDK never disposes a transport it did not create.

## Authentication

The SDK covers the **TOTP flow**, which is one call:

```csharp
using var high = new HighClient(new HighClientOptions { ApiKey = apiKey });

var token = await high.Auth.GenerateAccessToken(clientId: "C1", tOtp: "123456");

// Use it for everything else.
using var trading = new HighClient(new HighClientOptions { AccessToken = token.Value });
```

A HIGH access token lasts 24 hours. The SDK attaches whatever you give it and
**does not refresh** — minting the next one needs a fresh TOTP, so the timing
is yours to choose. Configuration is settled at construction, so a new token
means a new client.

### What this SDK does not do

It wraps the TOTP endpoint and nothing else from the auth area. The redirect
consent flow (`GET /auth/generate-consent`, `GET /auth/login`,
`GET /auth/consume-consent`) is built around a browser page only a human can
complete, and token introspection (`GET /auth/validate-token`) is not wrapped
either — a caller learns a token is invalid from the next call's 401. Call
those endpoints directly if you need them.

## Errors

Every failure is a `HighApiError`, including timeouts and transport failures
(which carry `Status: 0`). No raw `HttpRequestException` and no
`JsonException` escapes the SDK.

```csharp
using High.OpenApi;

try
{
    await high.Orders.Place(order);
}
catch (HighApiError error)
{
    error.Status;     // HTTP status, or 0 for transport failures
    error.Code;       // e.g. "ORDER_REJECTED" — see ErrorCodes
    error.RequestId;  // quote this in support tickets
    error.Messages;   // validation errors return several
    error.Body;       // the parsed payload, for a non-standard error
}
```

`ErrorCodes` holds the documented catalogue as `const string` fields plus an
`All` list. The gateway can return codes outside it, so `Code` is a plain
`string?` — compare against the constants, but do not assume the set is closed.

## Retries

Retried: `GET` and `HEAD` only (matched case-insensitively), on 429, 500, 502,
503 and 504, honouring `Retry-After` in both its seconds and HTTP-date forms,
with exponential backoff and jitter otherwise. A `Retry-After` longer than
`MaxRetryDelayMs` (30s by default) is **not** waited out — the SDK gives up and
throws, rather than blocking your call inside an `await` you cannot break out of.

Never retried: `POST`, `PATCH` and `DELETE`. A retried `Orders.Place` would be
a duplicate order, and a retried square-off would be a second square-off.

## Logging

Off by default. Each level prints itself and everything more severe.

```csharp
using var high = new HighClient(new HighClientOptions { AccessToken = accessToken, LogLevel = LogLevel.Debug });
// 2026-09-28T17:05:12.345Z DEBUG HIGH -> POST https://openapi.high.live/v1/orders
//   { body = { tradeSide = B, tradingSymbol = RELIANCE-EQ, quantity = 1, ... } }
// 2026-09-28T17:05:12.488Z DEBUG HIGH <- 200 in 143ms https://openapi.high.live/v1/orders
//   { requestId = a1b2c3, body = { orderId = 2609250000123456, error =  } }
```

| Level | What it prints |
|---|---|
| `Error` | API error responses (status, code, requestId), timeouts, transport failures |
| `Warn` | Each retry, and giving up when `Retry-After` exceeds the ceiling |
| `Info` | One line per request: method and URL |
| `Debug` | The above plus request and response bodies, and response timing |

Every line is prefixed with an ISO-8601 timestamp and the level.

Credentials never reach the log. Headers are never logged at all, and the
`tOtp`, `apiKey`, `accessToken`, `tokenId` and `stepToken` query parameters are
replaced with `REDACTED` — bodies are deep-redacted by key name at any nesting
depth, and an oversized payload is replaced by a note. So debug logs are safe
to ship to an aggregator. Implement `ILogSink` and pass `LogSink` to route
lines somewhere other than the console.

## Cancellation

Every method takes an optional `CancellationToken` as its last parameter. Your
cancellation is surfaced unchanged — never converted into a timeout, and never
retried, including when it fires during a retry's delay rather than during the
request itself.

```csharp
using var cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromSeconds(1));
await high.Scrips.Quotes(new QuotesRequest { Symbols = new[] { "RELIANCE-EQ" } }, cts.Token);
```

## Resources

27 operations across five namespaces.

| Namespace | Methods |
|---|---|
| `Auth` | `GenerateAccessToken` |
| `Orders` | `Place` · `Modify` · `Get` · `Cancel` · `List` · `Trades` · `TradesFor` · `Charges` · `Margin` |
| `Portfolio` | `Positions` · `Holdings` · `Funds` · `ConvertPosition` · `SetStoplossAsync` · `SetTargetAsync` · `ExitAllPositions` · `ExitPosition` |
| `Scrips` | `Quotes` · `Ohlc` · `Depth` · `Expiries` · `FutureData` · `Historical` · `IntradayAsync` · `OptionChain` |
| `Market` | `Status` |

Each method returns the response's `data` — the `{requestId, data}` envelope
is unwrapped for you, and `RequestId` reaches you on the error.

A few shapes worth knowing, because they are not what you might guess:

```csharp
// Quotes and Ohlc return a Dictionary keyed by trading symbol, not a list.
var quotes = await high.Scrips.Quotes(new QuotesRequest { Symbols = new[] { "RELIANCE-EQ" } });
quotes["RELIANCE-EQ"].Ltp;

// Historical returns COLUMNAR arrays — index i across them is one candle.
var candles = await high.Scrips.Historical(new HistoricalRequest
{
    TradingSymbol = "RELIANCE-EQ",
    Interval = HistoricalInterval.OneDay,
    FromTime = 1789929000,   // epoch seconds
    ToTime = 1790330400,
});
candles.Close[0];             // not candles[0].Close

// Historical and IntradayAsync name the instrument by TradingSymbol, or by
// ExchangeSegment + ScripCode instead. IntradayAsync covers the last trading session.
var today = await high.Scrips.IntradayAsync(new IntradayRequest
{
    ExchangeSegment = ExchangeSegment.Nse,
    ScripCode = "2885",
    Interval = IntradayInterval.FiveMinutes,
});

// Positions and Holdings both have a Snapshot, with DIFFERENT shapes.
(await high.Portfolio.Positions()).Snapshot.TotalPl;      // PnlSnapshot
(await high.Portfolio.Holdings()).Snapshot.Investment;    // InvestmentSnapshot

// ScripInfo.ScripCode is a long; Order.ScripCode and Trade.ScripCode are strings.
// Order.Error is nullable, and Order.TradingSymbol may be null when the
// instrument is not in the local master. An option leg may have every
// property null (an empty object on the wire) and its Touchline may be null.
```

Every spec enum is a real C# enum — `TradeSide`, `Flavor`, `ProductType`,
`OrderTypeEnum`, `Validity`, `ExchangeSegment`, `ExpiryType`, `ExpiryFrequency`,
`HistoricalInterval`, `IntradayInterval`, `RiskOrderLeg`, `RiskOrderAction`, and
the narrower `ExitFlavor` (which `Portfolio.ExitPosition` alone accepts, with no
`GTT`) and `ExitProductType` (no `MTF`, shared by `ExitPosition`,
`SetStoplossAsync` and `SetTargetAsync`) — serialised through a shared
`SpecEnumConverter<T>` that reads the exact wire string from each member's
`[EnumMember]` attribute. None of them is widened to a plain `string` in a
public method signature.

## Instrument list

`high.Instruments` reads the instrument list — every scrip HIGH knows, as a
17-column CSV — for one of five categories: `All`, `Equity`, `Derivatives`,
`Commodity`, `Etfs`. It needs no credentials at all.

```csharp
// Streaming is the primary form: All and Derivatives are 14 MB and 11 MB,
// so this never buffers the whole file.
await foreach (var instrument in high.Instruments.Stream(InstrumentCategory.Equity))
{
    Console.WriteLine($"{instrument.TradingSymbol} {instrument.Name}");
}

// A materialised alternative for smaller categories.
var etfs = await high.Instruments.List(InstrumentCategory.Etfs);
```

Each `Instrument` carries the same 17 columns: `Exchange`, `Segment`,
`InstrumentType`, `TradingSymbol`, `ScripKey`, `Isin`, `ScripCode`, `Symbol`,
`Name`, `GroupSeries`, `HasFnO`, `UnderlyingSymbol`, `Expiry`, `OptionType`,
`StrikePrice`, `PriceTick` and `LotSize`. A blank CSV field comes back as
`null`, never an empty string. `StrikePrice` is a `decimal?` (it is money);
`Expiry` is a `DateOnly?`. `PriceTick` is never rescaled — its meaning varies
by segment, so it is passed through exactly as published.

The list is rebuilt once each trading morning and does not change intraday:
download it once a day and cache it yourself rather than re-fetching per
request.

## Live datafeed

`HighFeed` is a separate client beside `HighClient`, built from the same
`HighClientOptions` and credentials, over the BCL `ClientWebSocket` — **no new
dependency**. It is production-only: there is no sandbox feed, and
constructing one with `Environment.Sandbox` throws immediately rather than
quietly connecting somewhere unexpected.

```csharp
using High.OpenApi;
using High.OpenApi.Feed;

await using var feed = new HighFeed(new HighClientOptions { AccessToken = accessToken });
await feed.ConnectAsync();

await feed.SubscribeQuotesAsync(new[] { "NSE@2885", "BSE@532540" });
await feed.SubscribeIndicesAsync(new[] { "NSE@26000" });   // Nifty 50

await foreach (var evt in feed.Events())
{
    switch (evt)
    {
        case QuoteEvent q: Console.WriteLine($"{q.ScripKey} {q.Quote.LastTradedPrice}"); break;
        case DepthEvent d: Console.WriteLine($"{d.ScripKey} best bid {d.Depth.Bids[0].Price}"); break;
        case IndexEvent i: Console.WriteLine($"{i.ScripKey} {i.Index.IndexValue}"); break;
    }
}
```

You subscribe with the same HIGH scrip keys you use everywhere else in this
SDK — `NSE@2885`, `BSEFO@842150`, and so on. Three independent channels, each
with its own dedicated methods rather than one method taking a kind, so a
caller says what they mean and an index key can never end up where only a
quote or depth subscription belongs:

| Channel | Subscribe | Unsubscribe | Snapshot |
|---|---|---|---|
| Quotes | `SubscribeQuotesAsync` | `UnsubscribeQuotesAsync` | `SnapshotQuotesAsync` → `IReadOnlyDictionary<string, Quote>` |
| Depth (5 levels) | `SubscribeDepthAsync` | `UnsubscribeDepthAsync` | `SnapshotDepthAsync` → `IReadOnlyDictionary<string, FeedDepth>` |
| Indices | `SubscribeIndicesAsync` | `UnsubscribeIndicesAsync` | `SnapshotIndicesAsync` → `IReadOnlyDictionary<string, IndexQuote>` |

`SubscribeQuotesAsync`/`SubscribeDepthAsync` reject a key the feed treats as
an index, naming it and pointing at `SubscribeIndicesAsync` instead;
`SubscribeIndicesAsync` accepts only a key from the committed index table
(81 entries) and rejects anything else — including the handful of indices
HIGH lists that the feed itself does not carry, and the 6 scrip-master tokens
shared by two different indices, which are refused by name rather than
resolved by a coin-flip — both directions fail loudly at the call rather than
silently never ticking.

Delivery is both `feed.Events()` (a single `IAsyncEnumerable<FeedEvent>`,
ordered) and the `OnQuote`/`OnDepth`/`OnIndex` events, whichever idiom your
caller prefers — the same ticks reach both. The feed's own ticks are deltas;
`HighFeed` merges them for you; every `Quote`/`FeedDepth`/`IndexQuote` is
always a **complete** snapshot, and each event's `ChangedFields` names exactly
what this particular tick moved. A full market-watch tick carries quote data
and top-of-book together on the wire, and this SDK splits that into a
`QuoteEvent` and a one-level `DepthEvent` — `FeedDepth.Source` and
`LevelCount` tell you whether a book is that one-level top-of-book or the
full five-level `SubscribeDepthAsync` book, so the two are never presented as
the same shape.

On a transport failure, `HighFeed` reconnects with backoff, re-authenticates,
and resubscribes everything you had subscribed before it raises
`OnReconnected` — you do not need to remember your own subscriptions across a
blip. An auth rejection (`HighFeedAuthError`, carrying the server's own
`StCode`/`ServerMessage` plus a classified `Reason` such as
`MissingDataPlan` or `InvalidToken`) is **never** retried or reconnected —
the server will keep refusing, so `HighFeed` surfaces it once and stops.

Prices are decimal strings on the wire and are parsed to `decimal`, never a
binary float. Every model also carries an `Extra` dictionary for wire fields
this SDK does not (yet) name, so a future vendor addition still reaches you
instead of vanishing.

## Regenerating from the spec

Models in `src/High.OpenApi/Models` are **hand-written**, not generated by
NSwag or OpenAPI Generator — see the plan
(`docs/superpowers/plans/2026-09-28-high-sdk-csharp.md` in the `high-api`
repo) for why. `spec.lock.json` pins the exact commit of `High/sdk/spec` this
SDK was written against, and `spec/openapi.json` is a vendored, read-only copy
of the spec at that commit. `ConformanceTests` diffs `Environments`,
`ErrorCodes` and `ExpiryType` against the vendored copy on every test run, so
drift between the hand-written source and the pinned contract fails the suite
rather than shipping silently.

Moving to a newer contract is a manual, deliberate act: update
`spec.lock.json`, re-vendor `spec/openapi.json` from the new commit, re-check
every model against the new schema field-by-field, and update the models that
changed.

## Development

No .NET SDK is required on the host; everything runs in Docker.

```bash
docker run --rm -v "${PWD}:/w" -v nuget-cache:/root/.nuget/packages -w /w \
  mcr.microsoft.com/dotnet/sdk:8.0 bash -lc "dotnet test"
```

Tests run against a real local `HttpListener`, never a mocked
`HttpMessageHandler`, so a change in how the SDK builds requests is caught
rather than asserted around.

## Licence

MIT. See [LICENSE](./LICENSE).
