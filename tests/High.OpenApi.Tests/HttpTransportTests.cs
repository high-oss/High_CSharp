// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Net;
using High.OpenApi;
using High.OpenApi.Http;
using Xunit;

namespace High.OpenApi.Tests;

public class HttpTransportTests
{
    private static ResolvedConfig ConfigFor(TestServer server, Action<HighClientOptions>? configure = null)
    {
        var options = new HighClientOptions { BaseUrl = server.BaseUrl, AccessToken = "tok", ApiKey = "key", TimeoutMs = 2000 };
        configure?.Invoke(options);
        return ConfigResolver.Resolve(options, _ => null);
    }

    [Fact]
    public async Task Unwraps_the_envelope_and_returns_data()
    {
        using var server = new TestServer((ctx, _) =>
            TestServerResponses.EnvelopeAsync(ctx, 200, "req-1", """{"ok":true}"""));

        var data = await HighHttpTransport.SendAsync<Dictionary<string, bool>>(
            ConfigFor(server),
            new RequestOptions { Method = HttpMethod.Get, Path = "/orders/list", Auth = AuthKind.Bearer },
            CancellationToken.None);

        Assert.True(data["ok"]);
    }

    [Fact]
    public async Task URL_is_composed_from_baseUrl_and_versionPath_tolerant_of_stray_slashes()
    {
        using var server = new TestServer((ctx, _) =>
            TestServerResponses.EnvelopeAsync(ctx, 200, "r", "true"));

        var config = ConfigFor(server, o => o.VersionPath = "/v1/");
        await HighHttpTransport.SendAsync<bool>(config,
            new RequestOptions { Method = HttpMethod.Get, Path = "//orders/list/", Auth = AuthKind.Bearer },
            CancellationToken.None);

        Assert.Equal("/v1/orders/list", server.Requests[0].Url);
    }

    [Fact]
    public async Task RequestId_is_preserved_on_an_error()
    {
        using var server = new TestServer((ctx, _) =>
            TestServerResponses.JsonAsync(ctx, 404, """{"requestId":"req-err","code":"ORDER_NOT_FOUND","message":"no such order"}"""));

        var error = await Assert.ThrowsAsync<HighApiError>(() => HighHttpTransport.SendAsync<object>(
            ConfigFor(server),
            new RequestOptions { Method = HttpMethod.Get, Path = "/orders/x", Auth = AuthKind.Bearer },
            CancellationToken.None));

        Assert.Equal(404, error.Status);
        Assert.Equal("ORDER_NOT_FOUND", error.Code);
        Assert.Equal("req-err", error.RequestId);
        Assert.Equal(new[] { "no such order" }, error.Messages);
    }

    [Fact]
    public async Task A_non_JSON_error_body_surfaces_with_its_real_status()
    {
        using var server = new TestServer(async (ctx, _) =>
        {
            ctx.Response.StatusCode = 502;
            ctx.Response.ContentType = "text/html";
            var bytes = System.Text.Encoding.UTF8.GetBytes("<html>Bad Gateway</html>");
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes);
            ctx.Response.OutputStream.Close();
        });

        var error = await Assert.ThrowsAsync<HighApiError>(() => HighHttpTransport.SendAsync<object>(
            ConfigFor(server, o => o.MaxRetries = 0),
            new RequestOptions { Method = HttpMethod.Get, Path = "/orders/list", Auth = AuthKind.Bearer },
            CancellationToken.None));

        Assert.Equal(502, error.Status);
        Assert.Contains("Bad Gateway", error.Message);
    }

    [Fact]
    public async Task Missing_bearer_credential_fails_before_anything_is_sent()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", "true"));
        var config = ConfigResolver.Resolve(new HighClientOptions { BaseUrl = server.BaseUrl }, _ => null);

        var error = await Assert.ThrowsAsync<HighApiError>(() => HighHttpTransport.SendAsync<object>(
            config, new RequestOptions { Method = HttpMethod.Get, Path = "/orders/list", Auth = AuthKind.Bearer },
            CancellationToken.None));

        Assert.Contains("accessToken", error.Message);
        Assert.Empty(server.Requests);
    }

    [Fact]
    public async Task Missing_apiKey_credential_fails_before_anything_is_sent()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", "true"));
        var config = ConfigResolver.Resolve(new HighClientOptions { BaseUrl = server.BaseUrl }, _ => null);

        var error = await Assert.ThrowsAsync<HighApiError>(() => HighHttpTransport.SendAsync<object>(
            config, new RequestOptions { Method = HttpMethod.Get, Path = "/auth/generate-access-token", Auth = AuthKind.ApiKey },
            CancellationToken.None));

        Assert.Contains("apiKey", error.Message);
        Assert.Empty(server.Requests);
    }

    [Fact]
    public async Task Bearer_credential_is_attached_as_an_authorization_header()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", "true"));
        await HighHttpTransport.SendAsync<bool>(ConfigFor(server),
            new RequestOptions { Method = HttpMethod.Get, Path = "/orders/list", Auth = AuthKind.Bearer },
            CancellationToken.None);
        Assert.Equal("Bearer tok", server.Requests[0].Headers["authorization"]);
    }

    [Fact]
    public async Task ApiKey_credential_is_attached_as_x_api_key()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", "true"));
        await HighHttpTransport.SendAsync<bool>(ConfigFor(server),
            new RequestOptions { Method = HttpMethod.Get, Path = "/auth/generate-access-token", Auth = AuthKind.ApiKey },
            CancellationToken.None);
        Assert.Equal("key", server.Requests[0].Headers["x-api-key"]);
    }

    [Fact]
    public async Task Cancellation_during_the_request_is_surfaced_unchanged()
    {
        using var server = new TestServer(async (ctx, _) =>
        {
            await Task.Delay(2000);
            await TestServerResponses.EnvelopeAsync(ctx, 200, "r", "true");
        });

        using var cts = new CancellationTokenSource();
        var task = HighHttpTransport.SendAsync<bool>(ConfigFor(server, o => o.TimeoutMs = 10_000),
            new RequestOptions { Method = HttpMethod.Get, Path = "/orders/list", Auth = AuthKind.Bearer },
            cts.Token);
        cts.CancelAfter(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task Timeout_during_headers_surfaces_as_a_HighApiError_with_status_zero()
    {
        using var server = new TestServer(async (ctx, _) =>
        {
            await Task.Delay(3000);
            await TestServerResponses.EnvelopeAsync(ctx, 200, "r", "true");
        });

        var error = await Assert.ThrowsAsync<HighApiError>(() => HighHttpTransport.SendAsync<bool>(
            ConfigFor(server, o => { o.TimeoutMs = 150; o.MaxRetries = 0; }),
            new RequestOptions { Method = HttpMethod.Get, Path = "/orders/list", Auth = AuthKind.Bearer },
            CancellationToken.None));

        Assert.Equal(0, error.Status);
        Assert.Contains("timed out", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Timeout_covers_the_body_when_headers_arrive_but_the_body_stalls()
    {
        using var server = new TestServer(async (ctx, _) =>
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            ctx.Response.SendChunked = true;
            var head = System.Text.Encoding.UTF8.GetBytes("{\"requestId\":\"r\",\"da");
            await ctx.Response.OutputStream.WriteAsync(head);
            await ctx.Response.OutputStream.FlushAsync();
            await Task.Delay(3000);
            var tail = System.Text.Encoding.UTF8.GetBytes("ta\":true}");
            await ctx.Response.OutputStream.WriteAsync(tail);
            ctx.Response.OutputStream.Close();
        });

        var startedAt = DateTime.UtcNow;
        var error = await Assert.ThrowsAsync<HighApiError>(() => HighHttpTransport.SendAsync<bool>(
            ConfigFor(server, o => { o.TimeoutMs = 200; o.MaxRetries = 0; }),
            new RequestOptions { Method = HttpMethod.Get, Path = "/orders/list", Auth = AuthKind.Bearer },
            CancellationToken.None));

        Assert.Equal(0, error.Status);
        Assert.True(DateTime.UtcNow - startedAt < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Transport_failure_surfaces_as_a_HighApiError_with_status_zero()
    {
        // Nothing is listening on this port.
        var config = ConfigResolver.Resolve(
            new HighClientOptions { BaseUrl = "http://127.0.0.1:1", AccessToken = "tok", TimeoutMs = 2000, MaxRetries = 0 },
            _ => null);

        var error = await Assert.ThrowsAsync<HighApiError>(() => HighHttpTransport.SendAsync<bool>(
            config, new RequestOptions { Method = HttpMethod.Get, Path = "/orders/list", Auth = AuthKind.Bearer },
            CancellationToken.None));

        Assert.Equal(0, error.Status);
    }
}
