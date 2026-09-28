// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using High.OpenApi;
using High.OpenApi.Http;
using Xunit;

namespace High.OpenApi.Tests;

public class RetryTests
{
    private static ResolvedConfig ConfigFor(TestServer server, Action<HighClientOptions>? configure = null)
    {
        var options = new HighClientOptions { BaseUrl = server.BaseUrl, AccessToken = "tok", TimeoutMs = 2000 };
        configure?.Invoke(options);
        return ConfigResolver.Resolve(options, _ => null);
    }

    [Fact]
    public async Task Retries_a_GET_on_429_and_succeeds_on_the_next_attempt()
    {
        using var server = new TestServer(async (ctx, index) =>
        {
            if (index == 0)
            {
                await TestServerResponses.JsonAsync(ctx, 429, """{"code":"SERVICE_UNAVAILABLE"}""",
                    new Dictionary<string, string> { ["Retry-After"] = "0" });
                return;
            }
            await TestServerResponses.EnvelopeAsync(ctx, 200, "r", "true");
        });

        var data = await HighHttpTransport.SendAsync<bool>(ConfigFor(server, o => o.MaxRetries = 2),
            new RequestOptions { Method = HttpMethod.Get, Path = "/orders/list", Auth = AuthKind.Bearer },
            CancellationToken.None);

        Assert.True(data);
        Assert.Equal(2, server.Requests.Count);
    }

    [Fact]
    public async Task Honours_a_Retry_After_HTTP_date_form()
    {
        using var server = new TestServer(async (ctx, index) =>
        {
            if (index == 0)
            {
                var retryAt = DateTime.UtcNow.AddMilliseconds(50).ToString("R");
                await TestServerResponses.JsonAsync(ctx, 503, """{"code":"SERVICE_UNAVAILABLE"}""",
                    new Dictionary<string, string> { ["Retry-After"] = retryAt });
                return;
            }
            await TestServerResponses.EnvelopeAsync(ctx, 200, "r", "true");
        });

        var data = await HighHttpTransport.SendAsync<bool>(ConfigFor(server, o => o.MaxRetries = 2),
            new RequestOptions { Method = HttpMethod.Get, Path = "/orders/list", Auth = AuthKind.Bearer },
            CancellationToken.None);

        Assert.True(data);
        Assert.Equal(2, server.Requests.Count);
    }

    [Fact]
    public async Task Never_retries_a_write_even_on_a_retryable_status()
    {
        using var server = new TestServer((ctx, _) =>
            TestServerResponses.JsonAsync(ctx, 503, """{"code":"SERVICE_UNAVAILABLE"}"""));

        await Assert.ThrowsAsync<HighApiError>(() => HighHttpTransport.SendAsync<object>(
            ConfigFor(server, o => o.MaxRetries = 3),
            new RequestOptions { Method = HttpMethod.Post, Path = "/orders", Auth = AuthKind.Bearer, Body = new { } },
            CancellationToken.None));

        Assert.Single(server.Requests);
    }

    [Theory]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task Never_retries_other_write_methods(string method)
    {
        using var server = new TestServer((ctx, _) =>
            TestServerResponses.JsonAsync(ctx, 500, """{"code":"UNHANDLED_ERROR"}"""));

        await Assert.ThrowsAsync<HighApiError>(() => HighHttpTransport.SendAsync<object>(
            ConfigFor(server, o => o.MaxRetries = 3),
            new RequestOptions { Method = new HttpMethod(method), Path = "/orders/1", Auth = AuthKind.Bearer },
            CancellationToken.None));

        Assert.Single(server.Requests);
    }

    [Fact]
    public async Task Does_not_retry_on_a_non_retryable_4xx()
    {
        using var server = new TestServer((ctx, _) =>
            TestServerResponses.JsonAsync(ctx, 404, """{"code":"ORDER_NOT_FOUND"}"""));

        await Assert.ThrowsAsync<HighApiError>(() => HighHttpTransport.SendAsync<object>(
            ConfigFor(server, o => o.MaxRetries = 3),
            new RequestOptions { Method = HttpMethod.Get, Path = "/orders/1", Auth = AuthKind.Bearer },
            CancellationToken.None));

        Assert.Single(server.Requests);
    }

    [Fact]
    public async Task A_Retry_After_above_the_ceiling_fails_fast_rather_than_sleeping_it_out()
    {
        using var server = new TestServer((ctx, _) =>
            TestServerResponses.JsonAsync(ctx, 429, """{"code":"SERVICE_UNAVAILABLE"}""",
                new Dictionary<string, string> { ["Retry-After"] = "86400" }));

        var startedAt = DateTime.UtcNow;
        var error = await Assert.ThrowsAsync<HighApiError>(() => HighHttpTransport.SendAsync<object>(
            ConfigFor(server, o => { o.MaxRetries = 2; o.MaxRetryDelayMs = 100; }),
            new RequestOptions { Method = HttpMethod.Get, Path = "/orders/list", Auth = AuthKind.Bearer },
            CancellationToken.None));

        Assert.Equal(429, error.Status);
        Assert.True(DateTime.UtcNow - startedAt < TimeSpan.FromSeconds(2));
        Assert.Single(server.Requests);
    }

    [Fact]
    public async Task Cancellation_during_a_retry_delay_stops_the_next_attempt_from_being_sent()
    {
        using var server = new TestServer((ctx, _) =>
            TestServerResponses.JsonAsync(ctx, 429, """{"code":"SERVICE_UNAVAILABLE"}""",
                new Dictionary<string, string> { ["Retry-After"] = "3" }));

        using var cts = new CancellationTokenSource();
        var task = HighHttpTransport.SendAsync<object>(ConfigFor(server, o => o.MaxRetries = 2),
            new RequestOptions { Method = HttpMethod.Get, Path = "/orders/list", Auth = AuthKind.Bearer },
            cts.Token);
        cts.CancelAfter(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        // Give any wrongly-issued second attempt time to land before asserting it didn't.
        await Task.Delay(200);
        Assert.Single(server.Requests);
    }

    [Fact]
    public async Task Exponential_backoff_applies_when_no_Retry_After_is_present()
    {
        using var server = new TestServer(async (ctx, index) =>
        {
            if (index < 2)
            {
                await TestServerResponses.JsonAsync(ctx, 500, """{"code":"UNHANDLED_ERROR"}""");
                return;
            }
            await TestServerResponses.EnvelopeAsync(ctx, 200, "r", "true");
        });

        var data = await HighHttpTransport.SendAsync<bool>(ConfigFor(server, o => o.MaxRetries = 2),
            new RequestOptions { Method = HttpMethod.Get, Path = "/orders/list", Auth = AuthKind.Bearer },
            CancellationToken.None);

        Assert.True(data);
        Assert.Equal(3, server.Requests.Count);
    }

    [Fact]
    public void ParseRetryAfter_handles_seconds_HTTP_date_and_invalid_forms()
    {
        Assert.Equal(TimeSpan.FromSeconds(3), HighHttpTransport.ParseRetryAfter("3"));
        Assert.Null(HighHttpTransport.ParseRetryAfter("not-a-valid-header"));
        Assert.Null(HighHttpTransport.ParseRetryAfter(null));

        var now = DateTimeOffset.UtcNow;
        var future = now.AddSeconds(10);
        var delay = HighHttpTransport.ParseRetryAfter(future.ToString("R"), now);
        Assert.NotNull(delay);
        Assert.InRange(delay!.Value.TotalSeconds, 9, 11);
    }

    [Fact]
    public void ParseRetryAfter_never_yields_a_negative_delay()
    {
        var now = DateTimeOffset.UtcNow;
        var past = now.AddSeconds(-10);
        Assert.Equal(TimeSpan.Zero, HighHttpTransport.ParseRetryAfter(past.ToString("R"), now));
    }
}
