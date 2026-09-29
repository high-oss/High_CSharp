// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Net.Http;
using High.OpenApi.Logging;

namespace High.OpenApi;

/// <summary>
/// Options for <see cref="HighClient"/>. Every property is optional; unset
/// values fall back to an environment variable, then a documented default.
/// See the README's Configuration section for the full resolution order.
/// </summary>
public sealed class HighClientOptions
{
    /// <summary>Which HIGH environment to talk to. Ignored when <see cref="BaseUrl"/> is set. Default: production.</summary>
    public Environment? Environment { get; set; }

    /// <summary>Overrides <see cref="Environment"/> entirely. Use for a staging host or a local mock.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Datafeed socket host. Reserved for the feed client; overrides <see cref="Environment"/>.</summary>
    public string? WsBaseUrl { get; set; }

    /// <summary>API version segment between the host and the operation path. Default: "v1".</summary>
    public string? VersionPath { get; set; }

    /// <summary>Sent as <c>x-api-key</c>. Required by <c>auth.GenerateAccessToken</c>.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Sent as <c>Authorization: Bearer</c>. Required by the other 23 operations.</summary>
    public string? AccessToken { get; set; }

    /// <summary>Per-request timeout, covering headers and body. Default: 30000.</summary>
    public int? TimeoutMs { get; set; }

    /// <summary>Retries for idempotent reads only. Default: 2.</summary>
    public int? MaxRetries { get; set; }

    /// <summary>
    /// Ceiling on a single retry delay. A <c>Retry-After</c> longer than this is
    /// not waited out — the SDK gives up and throws instead. Default: 30000.
    /// </summary>
    public int? MaxRetryDelayMs { get; set; }

    /// <summary>Appended to the SDK's own User-Agent.</summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// How much the SDK prints. Each level prints itself and everything more
    /// severe. Default: silent. Credentials and TOTPs are redacted at every level.
    /// </summary>
    public LogLevel? LogLevel { get; set; }

    /// <summary>Where log lines go. Default: the console.</summary>
    public ILogSink? LogSink { get; set; }

    /// <summary>
    /// Hosts the instrument list's downloaded CSV is allowed to come from.
    /// <c>Instruments.Stream</c>/<c>List</c> refuse a URL whose host is not in
    /// this set, and refuse a non-<c>https</c> scheme outright — before making
    /// any request. Default: <see cref="Resources.InstrumentsResource.DefaultAllowedHost"/>,
    /// the CDN host the manifest normally points at.
    /// </summary>
    public IReadOnlyCollection<string>? InstrumentManifestAllowedHosts { get; set; }

    /// <summary>
    /// Transport override. Pass an <see cref="HttpClient"/> from
    /// <c>IHttpClientFactory</c>, a test double, or a proxy-aware client. When
    /// omitted, the SDK creates and owns its own.
    /// </summary>
    public HttpClient? HttpClient { get; set; }
}
