// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using High.OpenApi.Http;
using High.OpenApi.Models;

namespace High.OpenApi.Resources;

/// <summary>
/// TOTP authentication.
///
/// The redirect consent flow (<c>/auth/generate-consent</c>, <c>/auth/login</c>,
/// <c>/auth/consume-consent</c>) is deliberately not wrapped: it is built
/// around a browser page only a human can complete. Token introspection
/// (<c>/auth/validate-token</c>) is not wrapped either — a caller learns a
/// token is invalid from the next call's 401. See the README.
/// </summary>
public sealed class AuthResource
{
    private readonly ResolvedConfig _config;

    internal AuthResource(ResolvedConfig config) => _config = config;

    /// <summary>Exchanges the API key plus a TOTP for a 24-hour access token.</summary>
    public Task<AccessToken> GenerateAccessToken(string clientId, string tOtp, CancellationToken cancellationToken = default)
    {
        var options = new RequestOptions
        {
            Method = HttpMethod.Get,
            Path = "/auth/generate-access-token",
            Auth = AuthKind.ApiKey,
            Query = new Dictionary<string, string?> { ["clientId"] = clientId, ["tOtp"] = tOtp },
        };
        return HighHttpTransport.SendAsync<AccessToken>(_config, options, cancellationToken);
    }
}
