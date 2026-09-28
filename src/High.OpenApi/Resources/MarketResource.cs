// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using High.OpenApi.Http;
using High.OpenApi.Models;

namespace High.OpenApi.Resources;

public sealed class MarketResource
{
    private readonly ResolvedConfig _config;

    internal MarketResource(ResolvedConfig config) => _config = config;

    /// <summary>Session state per exchange for today.</summary>
    public Task<MarketStatus> Status(CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<MarketStatus>(_config,
            new RequestOptions { Method = HttpMethod.Get, Path = "/market/status", Auth = AuthKind.Bearer },
            cancellationToken);
}
