// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using High.OpenApi;
using Xunit;

namespace High.OpenApi.Tests.Resources;

public class AuthResourceTests
{
    [Fact]
    public async Task GenerateAccessToken_sends_clientId_and_tOtp_as_query_and_apiKey_as_header()
    {
        using var server = new TestServer((ctx, _) =>
            TestServerResponses.EnvelopeAsync(ctx, 200, "r",
                """{"accessToken":"eyJ...","expiresAt":"2026-09-28T06:30:56.123Z"}"""));
        using var client = new HighClient(new HighClientOptions { BaseUrl = server.BaseUrl, ApiKey = "the-api-key" });

        var token = await client.Auth.GenerateAccessToken("C1", "123456");

        Assert.Equal("eyJ...", token.Value);
        Assert.Equal("2026-09-28T06:30:56.123Z", token.ExpiresAt);
        Assert.Equal("the-api-key", server.Requests[0].Headers["x-api-key"]);
        Assert.Equal("/v1/auth/generate-access-token?clientId=C1&tOtp=123456", server.Requests[0].Url);
        Assert.False(server.Requests[0].Headers.ContainsKey("authorization"));
    }
}
