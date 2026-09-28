// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using High.OpenApi.Json;
using High.OpenApi.Logging;

namespace High.OpenApi.Http;

/// <summary>
/// Sends one operation and returns its unwrapped <c>data</c>.
///
/// Retries apply to idempotent reads only (<c>GET</c>/<c>HEAD</c>, matched
/// case-insensitively), on 429 and 5xx, honouring <c>Retry-After</c> in both
/// its seconds and HTTP-date forms and backing off exponentially when it is
/// not present. Writes are never retried. A caller's cancellation is
/// surfaced unchanged, including mid retry-delay; a timeout is a
/// <see cref="HighApiError"/> with <c>status: 0</c> and covers the whole
/// exchange, headers and body.
/// </summary>
internal static class HighHttpTransport
{
    private static readonly HashSet<int> RetryableStatuses = new() { 429, 500, 502, 503, 504 };

    public static async Task<T> SendAsync<T>(ResolvedConfig config, RequestOptions options, CancellationToken cancellationToken)
    {
        var data = await SendAsync(config, options, cancellationToken).ConfigureAwait(false);
        return data.Deserialize<T>(HighJsonOptions.Default)!;
    }

    public static async Task<JsonNode?> SendAsync(ResolvedConfig config, RequestOptions options, CancellationToken cancellationToken)
    {
        // A missing credential fails before anything is sent — checked once,
        // outside the retry loop, so it can never surface as a raw exception
        // from deep inside a retry.
        RequireCredential(config, options.Auth);

        var url = UrlFor(config, options);
        var retryable = options.Method == HttpMethod.Get || options.Method == HttpMethod.Head;
        var maxAttempts = retryable ? config.MaxRetries + 1 : 1;

        Attempt? last = null;
        for (var index = 0; index < maxAttempts; index++)
        {
            last = await AttemptAsync(config, options, url, cancellationToken).ConfigureAwait(false);

            if (last.Error is null) return last.Data;
            if (!retryable || !RetryableStatuses.Contains(last.Status)) throw last.Error;
            if (index == maxAttempts - 1) break;

            var backoffMs = 250 * Math.Pow(2, index) + Random.Shared.Next(0, 100);
            var delay = last.RetryAfter ?? TimeSpan.FromMilliseconds(backoffMs);

            // A Retry-After past the ceiling means "come back much later", not
            // "block this call". Waiting it out inside an await the caller cannot
            // break is worse than failing now.
            if (delay.TotalMilliseconds > config.MaxRetryDelayMs)
            {
                config.Logger!.Warn(
                    $"HIGH giving up: server asked for {(long)delay.TotalMilliseconds}ms, over the {config.MaxRetryDelayMs}ms ceiling {Redaction.RedactUrl(url)}");
                break;
            }

            config.Logger!.Warn(
                $"HIGH retry {index + 1}/{maxAttempts - 1} after {(long)delay.TotalMilliseconds}ms (HTTP {last.Status}) {Redaction.RedactUrl(url)}");

            // Task.Delay observes cancellationToken natively, so a caller's abort
            // during this wait surfaces immediately rather than letting the call
            // go on to issue its next attempt.
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        throw last!.Error!;
    }

    private static void RequireCredential(ResolvedConfig config, AuthKind auth)
    {
        if (auth == AuthKind.Bearer && string.IsNullOrEmpty(config.AccessToken))
        {
            throw new HighApiError(
                "This operation needs an accessToken. Pass it to the client, or set HIGH_ACCESS_TOKEN.", 0);
        }
        if (auth == AuthKind.ApiKey && string.IsNullOrEmpty(config.ApiKey))
        {
            throw new HighApiError(
                "This operation needs an apiKey. Pass it to the client, or set HIGH_API_KEY.", 0);
        }
    }

    private static string UrlFor(ResolvedConfig config, RequestOptions options)
    {
        var url = config.BuildUrl(options.Path);
        if (options.Query is null || options.Query.Count == 0) return url;

        var pairs = options.Query
            .Where(kv => kv.Value is not null)
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}");
        var query = string.Join("&", pairs);
        return query.Length == 0 ? url : $"{url}?{query}";
    }

    private static async Task<Attempt> AttemptAsync(
        ResolvedConfig config, RequestOptions options, string url, CancellationToken callerToken)
    {
        using var request = BuildRequest(config, options, url);

        config.Logger!.Debug(
            $"HIGH -> {options.Method.Method} {Redaction.RedactUrl(url)}",
            options.Body is null ? null : new { body = Redaction.RedactBody(options.Body, HighJsonOptions.Default) });
        config.Logger!.Info($"HIGH {options.Method.Method} {Redaction.RedactUrl(url)}");

        using var timeoutCts = new CancellationTokenSource(config.TimeoutMs);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken, timeoutCts.Token);

        var startedAt = DateTime.UtcNow;
        HttpResponseMessage response;
        string text;
        try
        {
            response = await config.HttpClient!
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token)
                .ConfigureAwait(false);
            // Read under the same linked token as the send: one timer covers the
            // whole exchange, headers AND body, so a stalled body cannot hang a
            // call that documents a bounded timeout.
            text = await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
        {
            // The caller's own decision — surface it unchanged, never as a timeout.
            throw;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            config.Logger!.Error($"HIGH <- timeout after {config.TimeoutMs}ms {Redaction.RedactUrl(url)}");
            throw new HighApiError($"Request timed out after {config.TimeoutMs}ms", 0);
        }
        catch (HttpRequestException ex)
        {
            config.Logger!.Error($"HIGH <- transport failure {Redaction.RedactUrl(url)}", ex.Message);
            throw new HighApiError($"Request failed: {ex.Message}", 0, body: ex.Message);
        }

        var status = (int)response.StatusCode;
        JsonNode? parsed = null;
        var parseFailed = false;
        if (!string.IsNullOrEmpty(text))
        {
            try
            {
                parsed = JsonNode.Parse(text);
            }
            catch (JsonException)
            {
                parseFailed = true;
            }
        }

        var elapsedMs = (long)(DateTime.UtcNow - startedAt).TotalMilliseconds;
        var envelopeRequestId = !parseFailed ? (parsed as JsonObject)?["requestId"]?.GetValue<string>() : null;

        config.Logger!.Debug(
            $"HIGH <- {status} in {elapsedMs}ms {Redaction.RedactUrl(url)}",
            new { requestId = envelopeRequestId, body = Redaction.RedactBody(parseFailed ? (object?)Truncate(text) : parsed) });

        if (!response.IsSuccessStatusCode)
        {
            var error = HighApiError.FromResponse(status, parseFailed ? null : parsed, text);
            config.Logger!.Error(
                $"HIGH <- {status} {error.Code ?? "no code"} in {elapsedMs}ms {Redaction.RedactUrl(url)}",
                new { error.RequestId, error.Messages });
            return new Attempt { Status = status, Error = error, RetryAfter = RetryAfterOf(response) };
        }

        if (parseFailed)
        {
            return new Attempt
            {
                Status = status,
                Error = new HighApiError("Expected JSON but the response was not parseable", status, body: text),
            };
        }

        var data = (parsed as JsonObject)?["data"];
        return new Attempt { Status = status, Data = data };
    }

    private static string Truncate(string text) => text.Length > 200 ? text[..200] : text;

    private static HttpRequestMessage BuildRequest(ResolvedConfig config, RequestOptions options, string url)
    {
        var request = new HttpRequestMessage(options.Method, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd(config.UserAgent);

        if (options.Auth == AuthKind.Bearer)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AccessToken);
        }
        else
        {
            request.Headers.Add("x-api-key", config.ApiKey);
        }

        if (options.Body is not null)
        {
            var json = JsonSerializer.Serialize(options.Body, options.Body.GetType(), HighJsonOptions.Default);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return request;
    }

    /// <summary>
    /// Parses a <c>Retry-After</c> header in both forms the spec allows: a
    /// delay in seconds, or an HTTP-date. Never yields a negative delay.
    /// </summary>
    internal static TimeSpan? RetryAfterOf(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Retry-After", out var values)) return null;
        var header = values.FirstOrDefault();
        return ParseRetryAfter(header);
    }

    internal static TimeSpan? ParseRetryAfter(string? header, DateTimeOffset? now = null)
    {
        if (string.IsNullOrEmpty(header)) return null;

        if (double.TryParse(header, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        if (DateTimeOffset.TryParse(header, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at))
        {
            var delta = at - (now ?? DateTimeOffset.UtcNow);
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        return null;
    }

    private sealed class Attempt
    {
        public int Status { get; init; }
        public JsonNode? Data { get; init; }
        public HighApiError? Error { get; init; }
        public TimeSpan? RetryAfter { get; init; }
    }
}
