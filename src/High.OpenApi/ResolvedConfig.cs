// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Net.Http;
using System.Text.Json.Serialization;
using High.OpenApi.Logging;

namespace High.OpenApi;

/// <summary>
/// Settles where requests go and what they carry, once, at client construction.
///
/// Deliberately a plain class, never a <c>record</c>: a record's generated
/// <c>ToString()</c> prints every property, which would print
/// <see cref="ApiKey"/> and <see cref="AccessToken"/> straight into any log
/// line or crash report built from <c>$"{config}"</c>. <see cref="ToString"/>
/// is overridden here to omit both, and both carry <see cref="JsonIgnoreAttribute"/>
/// so <c>JsonSerializer.Serialize</c> omits them too.
/// </summary>
public sealed class ResolvedConfig
{
    public required string BaseUrl { get; init; }

    /// <summary>Reserved for the datafeed client. Unused by the REST resources.</summary>
    public required string WsBaseUrl { get; init; }

    public required string VersionPath { get; init; }

    [JsonIgnore]
    public string? ApiKey { get; init; }

    [JsonIgnore]
    public string? AccessToken { get; init; }

    public required int TimeoutMs { get; init; }

    public required int MaxRetries { get; init; }

    public required int MaxRetryDelayMs { get; init; }

    public required string UserAgent { get; init; }

    public required LogLevel LogLevel { get; init; }

    /// <summary>Allowed hosts for the instrument list's downloaded CSV. See <see cref="HighClientOptions.InstrumentManifestAllowedHosts"/>.</summary>
    public required IReadOnlySet<string> InstrumentManifestAllowedHosts { get; init; }

    // Not `required`: System.Text.Json still validates required-member
    // constructability for a JsonIgnore'd property when the type has no
    // JsonConstructor, so combining `required` with [JsonIgnore] here throws
    // at first serialization ("marked required but does not specify a
    // setter") even though the property has an init accessor. ConfigResolver
    // is the only supported way to build a ResolvedConfig and always sets
    // these three.

    /// <summary>Ready to use; already honours <see cref="LogLevel"/>.</summary>
    [JsonIgnore]
    public Logger? Logger { get; init; }

    [JsonIgnore]
    public HttpClient? HttpClient { get; init; }

    /// <summary>True when this client constructed (and therefore owns) its own <see cref="HttpClient"/>.</summary>
    [JsonIgnore]
    public bool OwnsHttpClient { get; init; }

    /// <summary><c>BaseUrl</c> + <c>VersionPath</c> + operation path, with no doubled or missing slashes.</summary>
    public string BuildUrl(string path)
    {
        var segments = new[] { BaseUrl, VersionPath, path.Trim('/') }
            .Where(segment => !string.IsNullOrEmpty(segment));
        return string.Join("/", segments);
    }

    /// <summary>Never prints <see cref="ApiKey"/> or <see cref="AccessToken"/>.</summary>
    public override string ToString() =>
        $"ResolvedConfig {{ BaseUrl = {BaseUrl}, VersionPath = {VersionPath}, TimeoutMs = {TimeoutMs}, " +
        $"MaxRetries = {MaxRetries}, MaxRetryDelayMs = {MaxRetryDelayMs}, LogLevel = {LogLevel} }}";
}
