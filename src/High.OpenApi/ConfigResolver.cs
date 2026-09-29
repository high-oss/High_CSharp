// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Net.Http;
using High.OpenApi.Logging;

namespace High.OpenApi;

/// <summary>
/// Resolves a <see cref="HighClientOptions"/> plus the environment into a
/// <see cref="ResolvedConfig"/>.
///
/// Resolution order: an explicit <c>BaseUrl</c> wins outright; then an
/// explicit <c>Environment</c> — which also suppresses the environment
/// variables, so a <c>HIGH_BASE_URL</c> left in a shell profile cannot
/// silently route a client written as <c>Environment = Sandbox</c> at
/// production; then <c>HIGH_BASE_URL</c> / <c>HIGH_ENVIRONMENT</c>; then
/// production. An unknown environment or log level, or a non-positive
/// timeout, or a negative retry count fails here, at construction — never at
/// request time.
/// </summary>
public static class ConfigResolver
{
    private const string DefaultUserAgentPrefix = "high-sdk-csharp/0.0.1";

    public static ResolvedConfig Resolve(HighClientOptions? options = null, Func<string, string?>? envLookup = null)
    {
        options ??= new HighClientOptions();
        var env = envLookup ?? System.Environment.GetEnvironmentVariable;

        High.OpenApi.Environment environment = High.OpenApi.Environment.Production;
        var explicitEnvironment = options.Environment.HasValue;
        if (options.Environment.HasValue)
        {
            if (!Enum.IsDefined(options.Environment.Value))
            {
                throw new ArgumentException(
                    $"Unknown HIGH environment (options.Environment). Valid values: {Environments.ValidNames}.");
            }
            environment = options.Environment.Value;
        }
        else if (env("HIGH_ENVIRONMENT") is { Length: > 0 } fromEnv)
        {
            if (!Environments.TryParse(fromEnv, out environment))
            {
                throw new ArgumentException(
                    $"Unknown HIGH environment \"{fromEnv}\" (HIGH_ENVIRONMENT). Valid values: {Environments.ValidNames}.");
            }
        }

        var hosts = Environments.Map[environment];

        var baseUrl = options.BaseUrl
            ?? (explicitEnvironment ? hosts.Api : env("HIGH_BASE_URL") is { Length: > 0 } b ? b : hosts.Api);
        var wsBaseUrl = options.WsBaseUrl
            ?? (explicitEnvironment ? hosts.Ws : env("HIGH_WS_BASE_URL") is { Length: > 0 } w ? w : hosts.Ws);

        var userAgent = string.IsNullOrEmpty(options.UserAgent)
            ? DefaultUserAgentPrefix
            : $"{DefaultUserAgentPrefix} {options.UserAgent}";

        var logLevel = LogLevel.Silent;
        if (options.LogLevel.HasValue)
        {
            if (!Enum.IsDefined(options.LogLevel.Value))
            {
                throw new ArgumentException(
                    $"Unknown HIGH log level (options.LogLevel). Valid values: {LogLevels.ValidNames}.");
            }
            logLevel = options.LogLevel.Value;
        }
        else if (env("HIGH_LOG_LEVEL") is { Length: > 0 } fromEnvLog)
        {
            if (!LogLevels.TryParse(fromEnvLog, out logLevel))
            {
                throw new ArgumentException(
                    $"Unknown HIGH log level \"{fromEnvLog}\" (HIGH_LOG_LEVEL). Valid values: {LogLevels.ValidNames}.");
            }
        }

        var timeoutMs = RequirePositive(options.TimeoutMs ?? 30_000, nameof(HighClientOptions.TimeoutMs));
        var maxRetries = RequireNonNegative(options.MaxRetries ?? 2, nameof(HighClientOptions.MaxRetries));
        var maxRetryDelayMs = RequirePositive(options.MaxRetryDelayMs ?? 30_000, nameof(HighClientOptions.MaxRetryDelayMs));

        var ownsHttpClient = options.HttpClient is null;
        var httpClient = options.HttpClient ?? new HttpClient();

        var apiKey = options.ApiKey ?? env("HIGH_API_KEY");
        var accessToken = options.AccessToken ?? env("HIGH_ACCESS_TOKEN");

        var instrumentManifestAllowedHosts = options.InstrumentsAllowedHosts is { Count: > 0 } allowedHosts
            ? new HashSet<string>(allowedHosts, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Resources.InstrumentsResource.DefaultAllowedHost };

        return new ResolvedConfig
        {
            BaseUrl = TrimTrailingSlashes(baseUrl),
            WsBaseUrl = TrimTrailingSlashes(wsBaseUrl),
            Environment = environment,
            VersionPath = (options.VersionPath ?? "v1").Trim('/'),
            ApiKey = apiKey,
            AccessToken = accessToken,
            TimeoutMs = timeoutMs,
            MaxRetries = maxRetries,
            MaxRetryDelayMs = maxRetryDelayMs,
            UserAgent = userAgent,
            LogLevel = logLevel,
            InstrumentsAllowedHosts = instrumentManifestAllowedHosts,
            Logger = new Logger(logLevel, options.LogSink),
            HttpClient = httpClient,
            OwnsHttpClient = ownsHttpClient,
        };
    }

    private static string TrimTrailingSlashes(string value) => value.TrimEnd('/');

    private static int RequirePositive(int value, string name)
    {
        if (value <= 0) throw new ArgumentException($"{name} must be a positive number, received {value}.");
        return value;
    }

    private static int RequireNonNegative(int value, string name)
    {
        if (value < 0) throw new ArgumentException($"{name} must be a non-negative integer, received {value}.");
        return value;
    }
}
