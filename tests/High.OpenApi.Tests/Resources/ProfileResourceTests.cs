// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using High.OpenApi;
using Xunit;

namespace High.OpenApi.Tests.Resources;

public class ProfileResourceTests
{
    private static HighClient ClientFor(TestServer server) =>
        new(new HighClientOptions { BaseUrl = server.BaseUrl, AccessToken = "tok" });

    [Fact]
    public async Task GetAsync_returns_the_masked_profile_from_GET_profile()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", """
            {"clientId":"HS1234","name":"Asha Verma","email":"asha.verma@example.com","mobile":"9876543210",
             "pan":"XXXXXX234F","aadhaar":"XXXXXXXX9012","depository":"CDSL","boCode":"1208160000123456",
             "dpCode":"12081600","isPOAApproved":true,"isNRI":false,"isMTFEnabled":true,
             "segmentsAllowed":["NSE_CASH","NSE_FNO"],
             "address":{"address":"12, MG Road","city":"Bengaluru","state":"Karnataka","pinCode":"560001"},
             "bankAccounts":[{"bankName":"HDFC Bank","accountNumber":"XXXXXX4321","ifscCode":"HDFC0000001",
              "branch":"MG Road","isPrimary":true}]}
            """));
        using var client = ClientFor(server);

        var profile = await client.Profile.GetAsync();
        Assert.Equal("HS1234", profile.ClientId);
        Assert.True(profile.IsMtfEnabled);
        Assert.False(profile.IsNri);
        Assert.Equal("560001", profile.Address.PinCode);
        Assert.Equal("XXXXXX4321", profile.BankAccounts[0].AccountNumber);
        Assert.True(profile.BankAccounts[0].IsPrimary);
        Assert.Equal("/v1/profile", server.Requests[0].Url);
    }
}
